using System.Buffers.Binary;
using System.Text;
using PkgViewer.Core.Models;
using PkgViewer.Core.Probing;
using Xunit;

namespace PkgViewer.Tests;

public sealed class PackageFormatProbeTests : IDisposable
{
    private static readonly byte[] CntMagic = [0x7F, (byte)'C', (byte)'N', (byte)'T'];

    private readonly List<string> _tempFiles = [];

    public void Dispose()
    {
        foreach (string path in _tempFiles)
        {
            try { File.Delete(path); }
            catch (IOException) { }
        }
    }

    [Fact]
    public void FihMagic_IsDetectedAsPs5Pkg()
    {
        PackageProbeResult result = Probe([0x7F, (byte)'F', (byte)'I', (byte)'H', .. new byte[0x7C]]);

        Assert.Equal(PackageFormat.Ps5Pkg, result.Format);
        Assert.Equal(PkgPlatform.Ps5, result.Platform);
        Assert.Equal(ProbeConfidence.High, result.Confidence);
    }

    [Fact]
    public void LihMagic_IsDetectedAsPs5Pkg()
    {
        PackageProbeResult result = Probe([0x7F, (byte)'L', (byte)'I', (byte)'H', .. new byte[0x7C]]);

        Assert.Equal(PackageFormat.Ps5Pkg, result.Format);
        Assert.Equal(PkgPlatform.Ps5, result.Platform);
        Assert.Equal(ProbeConfidence.High, result.Confidence);
    }

    [Fact]
    public void Ps4Cnt_WithCusaTitle_IsDetectedAsPs4()
    {
        byte[] header = BuildCntHeader("EP9000-CUSA07410_00-0000000000000000", version: 0x8000, flags: 0);

        PackageProbeResult result = Probe(header);

        Assert.Equal(PackageFormat.Ps4Pkg, result.Format);
        Assert.Equal(PkgPlatform.Ps4, result.Platform);
        Assert.Equal(ProbeConfidence.High, result.Confidence);
    }

    [Fact]
    public void Ps5Cnt_WithPpsaTitle_IsDetectedAsPs5()
    {
        byte[] header = BuildCntHeader("EP1011-PPSA01284_00-0000000000000000", version: 2, flags: 1);

        PackageProbeResult result = Probe(header);

        Assert.Equal(PackageFormat.Ps5Pkg, result.Format);
        Assert.Equal(PkgPlatform.Ps5, result.Platform);
        Assert.Equal(ProbeConfidence.High, result.Confidence);
    }

    [Fact]
    public void Ps4TitleWins_WhenHeaderFieldsLookLikePs5()
    {
        byte[] header = BuildCntHeader("EP9000-CUSA07410_00-0000000000000000", version: 2, flags: 1);

        PackageProbeResult result = Probe(header);

        Assert.Equal(PackageFormat.Ps4Pkg, result.Format);
        Assert.Equal(ProbeConfidence.High, result.Confidence);
    }

    [Fact]
    public void UnknownCnt_WithPs5VersionField_IsDetectedAsPs5()
    {
        byte[] header = BuildCntHeader("UNKNOWN-CONTENT-ID", version: 2, flags: 1);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(0x0C, 4), 0x0C);

        PackageProbeResult result = Probe(header);

        Assert.Equal(PackageFormat.Ps5Pkg, result.Format);
        Assert.True(result.Confidence >= ProbeConfidence.Medium);
    }

    [Fact]
    public void UnknownCnt_WithPs4FinalizedFlags_IsDetectedAsPs4()
    {
        byte[] header = BuildCntHeader("UNKNOWN-CONTENT-ID", version: 0x8000, flags: 0);

        PackageProbeResult result = Probe(header);

        Assert.Equal(PackageFormat.Ps4Pkg, result.Format);
        Assert.Equal(ProbeConfidence.Medium, result.Confidence);
    }

    [Fact]
    public void ExfatBootSector_IsDetected()
    {
        byte[] header = new byte[0x80];
        Encoding.ASCII.GetBytes("EXFAT").CopyTo(header, 3);

        PackageProbeResult result = Probe(header);

        Assert.Equal(PackageFormat.ExfatImage, result.Format);
        Assert.Equal(PkgPlatform.Ps5, result.Platform);
        Assert.Equal(ProbeConfidence.High, result.Confidence);
    }

    [Fact]
    public void PfsHeaderMagic_IsDetectedAsFfpfsc()
    {
        byte[] header = new byte[0x80];
        BinaryPrimitives.WriteInt64LittleEndian(header.AsSpan(8, 8), PackageFormatProbe.PfsMagic);

        PackageProbeResult result = Probe(header);

        Assert.Equal(PackageFormat.Ffpfsc, result.Format);
        Assert.Equal(PkgPlatform.Ps5, result.Platform);
        Assert.Equal(ProbeConfidence.Medium, result.Confidence);
    }

    [Fact]
    public void Ufs2SuperblockMagic_IsDetectedAsFfpkg()
    {
        byte[] image = new byte[PackageFormatProbe.Ufs2MagicOffset + 16];
        BinaryPrimitives.WriteUInt32LittleEndian(
            image.AsSpan((int)PackageFormatProbe.Ufs2MagicOffset, 4), PackageFormatProbe.Ufs2Magic);

        PackageProbeResult result = Probe(image);

        Assert.Equal(PackageFormat.Ffpkg, result.Format);
        Assert.Equal(PkgPlatform.Ps5, result.Platform);
        Assert.Equal(ProbeConfidence.High, result.Confidence);
    }

    [Fact]
    public void TinyFile_IsUnknown()
    {
        PackageProbeResult result = Probe(new byte[8]);

        Assert.Equal(PackageFormat.Unknown, result.Format);
        Assert.Equal(ProbeConfidence.None, result.Confidence);
    }

    [Fact]
    public void ExtensionFallback_IsLowConfidence()
    {
        byte[] random = new byte[0x200];
        random[0x10] = 0x42;
        string path = WriteTempFile(random, ".ffpkg");

        PackageProbeResult result = PackageFormatProbe.Probe(path);

        Assert.Equal(PackageFormat.Ffpkg, result.Format);
        Assert.Equal(ProbeConfidence.Low, result.Confidence);
    }

    private PackageProbeResult Probe(byte[] bytes) => PackageFormatProbe.Probe(WriteTempFile(bytes));

    private static byte[] BuildCntHeader(string contentId, ushort version, ushort flags)
    {
        byte[] header = new byte[0x80];
        CntMagic.CopyTo(header, 0);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(0x04, 2), version);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(0x06, 2), flags);
        byte[] contentBytes = Encoding.ASCII.GetBytes(contentId);
        Array.Copy(contentBytes, 0, header, 0x40, Math.Min(contentBytes.Length, 0x24));
        return header;
    }

    private string WriteTempFile(byte[] bytes, string extension = ".pkg")
    {
        string path = Path.Combine(Path.GetTempPath(), "PkgViewerTests-" + Guid.NewGuid().ToString("N") + extension);
        File.WriteAllBytes(path, bytes);
        _tempFiles.Add(path);
        return path;
    }
}
