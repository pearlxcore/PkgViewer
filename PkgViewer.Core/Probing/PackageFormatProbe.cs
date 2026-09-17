using System.Buffers.Binary;
using System.Text;
using PkgViewer.Core.Models;

namespace PkgViewer.Core.Probing;

public enum ProbeConfidence
{
    None = 0,
    Low = 1,
    Medium = 2,
    High = 3
}

public sealed record PackageProbeResult(
    PackageFormat Format,
    PkgPlatform Platform,
    ProbeConfidence Confidence,
    string Reason)
{
    public static PackageProbeResult Unknown(string reason) =>
        new(PackageFormat.Unknown, PkgPlatform.Unknown, ProbeConfidence.None, reason);
}

/// <summary>
/// Identifies the top-level container format from file signatures, never from the file extension
/// alone. PS4 PKGs and PS5 CNT metadata containers share the same leading magic (7F 43 4E 54), so
/// that case is disambiguated with title-id and header-field signals; when the signals conflict the
/// result carries a low confidence and the open service retries the other platform.
/// </summary>
public static class PackageFormatProbe
{
    public const long PfsMagic = 20130315;
    public const uint Ufs2Magic = 0x19540119;
    public const long Ufs2MagicOffset = 0x1055C;

    private static ReadOnlySpan<byte> FihMagic => [0x7F, (byte)'F', (byte)'I', (byte)'H'];
    private static ReadOnlySpan<byte> LihMagic => [0x7F, (byte)'L', (byte)'I', (byte)'H'];
    private static ReadOnlySpan<byte> CntMagic => [0x7F, (byte)'C', (byte)'N', (byte)'T'];

    private static readonly HashSet<string> Ps4TitlePrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "BCAS", "BCES", "BCJS", "BCKS", "BCUS",
        "BLAS", "BLES", "BLJS", "BLKS", "BLUS",
        "CUSA", "PCAS", "PCJS", "PCKS", "PCSC", "PCSB", "PCSD", "PCSE", "PCSF", "PCSG", "PCSH",
        "PCSX", "PLAS"
    };

    private static readonly HashSet<string> Ps5TitlePrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "PPSA"
    };

    public static PackageProbeResult Probe(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = Path.GetFullPath(path);
        using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.RandomAccess);
        return Probe(stream, Path.GetExtension(fullPath));
    }

    public static PackageProbeResult Probe(Stream stream, string? extension = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead || !stream.CanSeek)
            return PackageProbeResult.Unknown("The stream is not readable and seekable.");

        long originalPosition = stream.Position;
        try
        {
            Span<byte> header = stackalloc byte[0x80];
            int read = ReadAt(stream, 0, header);
            if (read < 16)
                return PackageProbeResult.Unknown("The file is too small to contain a package header.");
            ReadOnlySpan<byte> h = header[..read];

            if (h[..4].SequenceEqual(FihMagic))
                return new(PackageFormat.Ps5Pkg, PkgPlatform.Ps5, ProbeConfidence.High, "PS5 FIH magic.");
            if (h[..4].SequenceEqual(LihMagic))
                return new(PackageFormat.Ps5Pkg, PkgPlatform.Ps5, ProbeConfidence.High, "PS5 LIH patch magic.");
            if (h[..4].SequenceEqual(CntMagic))
                return ProbeCnt(h);

            if (h[3] == (byte)'E' && h[4] == (byte)'X' && h[5] == (byte)'F' && h[6] == (byte)'A' && h[7] == (byte)'T')
                return new(PackageFormat.ExfatImage, PkgPlatform.Ps5, ProbeConfidence.High, "exFAT boot-sector signature.");

            if (BinaryPrimitives.ReadInt64LittleEndian(h.Slice(8, 8)) == PfsMagic)
                return new(PackageFormat.Ffpfsc, PkgPlatform.Ps5, ProbeConfidence.Medium,
                    "PFS header magic (FFPFSC container or raw PFS image).");

            if (TryReadUfs2Magic(stream))
                return new(PackageFormat.Ffpkg, PkgPlatform.Ps5, ProbeConfidence.High, "UFS2 superblock magic.");

            return extension?.ToLowerInvariant() switch
            {
                ".ffpkg" => new(PackageFormat.Ffpkg, PkgPlatform.Ps5, ProbeConfidence.Low, "Extension only: .ffpkg."),
                ".exfat" => new(PackageFormat.ExfatImage, PkgPlatform.Ps5, ProbeConfidence.Low, "Extension only: .exfat."),
                ".ffpfsc" => new(PackageFormat.Ffpfsc, PkgPlatform.Ps5, ProbeConfidence.Low, "Extension only: .ffpfsc."),
                _ => PackageProbeResult.Unknown("No known package signature found.")
            };
        }
        finally
        {
            stream.Position = originalPosition;
        }
    }

    private static PackageProbeResult ProbeCnt(ReadOnlySpan<byte> header)
    {
        string titleSegment = ExtractTitleSegment(ReadContentId(header));
        if (titleSegment.Length == 9)
        {
            string prefix = titleSegment[..4];
            if (Ps5TitlePrefixes.Contains(prefix))
                return new(PackageFormat.Ps5Pkg, PkgPlatform.Ps5, ProbeConfidence.High,
                    $"CNT magic with PS5 title ID {titleSegment}.");
            if (Ps4TitlePrefixes.Contains(prefix) || prefix.StartsWith("NP", StringComparison.OrdinalIgnoreCase))
                return new(PackageFormat.Ps4Pkg, PkgPlatform.Ps4, ProbeConfidence.High,
                    $"CNT magic with PS4 title ID {titleSegment}.");
        }

        // PS5 CNT header: u16 version at 0x04 (observed 2), u16 flags at 0x06.
        // PS4 PKG header: u32 flags at 0x04; finalized packages set bit 31 (first byte 0x80).
        bool ps5VersionField = header[4] == 0x00 && header[5] is >= 1 and <= 8;
        bool ps4FinalizedFlags = header[4] == 0x80;
        bool ps5Constant = BinaryPrimitives.ReadUInt32BigEndian(header.Slice(0x0C, 4)) == 0x0C;

        if (ps4FinalizedFlags && !ps5VersionField)
            return new(PackageFormat.Ps4Pkg, PkgPlatform.Ps4, ProbeConfidence.Medium,
                "CNT magic with PS4 finalized flags.");
        if (ps5VersionField)
            return new(PackageFormat.Ps5Pkg, PkgPlatform.Ps5,
                ps5Constant ? ProbeConfidence.High : ProbeConfidence.Medium,
                "CNT magic with PS5 header version field.");
        if (ps5Constant)
            return new(PackageFormat.Ps5Pkg, PkgPlatform.Ps5, ProbeConfidence.Low,
                "CNT magic with the PS5 constant field at 0x0C.");
        return new(PackageFormat.Ps4Pkg, PkgPlatform.Ps4, ProbeConfidence.Low,
            "CNT magic; no PS5 signal, PS4 assumed.");
    }

    private static string ReadContentId(ReadOnlySpan<byte> header)
    {
        if (header.Length < 0x40 + 0x24) return string.Empty;
        ReadOnlySpan<byte> field = header.Slice(0x40, 0x24);
        int end = field.IndexOf((byte)0);
        if (end >= 0) field = field[..end];
        return Encoding.ASCII.GetString(field).Trim();
    }

    private static string ExtractTitleSegment(string contentId)
    {
        int dash = contentId.IndexOf('-');
        if (dash < 0 || dash + 10 > contentId.Length) return string.Empty;
        string segment = contentId.Substring(dash + 1, 9);
        for (int i = 0; i < 4; i++)
            if (!char.IsAsciiLetterUpper(segment[i])) return string.Empty;
        for (int i = 4; i < 9; i++)
            if (!char.IsAsciiDigit(segment[i])) return string.Empty;
        return segment;
    }

    private static bool TryReadUfs2Magic(Stream stream)
    {
        if (stream.Length < Ufs2MagicOffset + 4) return false;
        Span<byte> magic = stackalloc byte[4];
        if (ReadAt(stream, Ufs2MagicOffset, magic) != magic.Length) return false;
        return BinaryPrimitives.ReadUInt32LittleEndian(magic) == Ufs2Magic;
    }

    private static int ReadAt(Stream stream, long offset, Span<byte> buffer)
    {
        stream.Position = offset;
        int total = 0;
        while (total < buffer.Length)
        {
            int read = stream.Read(buffer[total..]);
            if (read == 0) break;
            total += read;
        }
        return total;
    }
}
