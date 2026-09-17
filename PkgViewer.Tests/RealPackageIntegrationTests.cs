using PkgViewer.Core;
using PkgViewer.Core.Backends;
using PkgViewer.Core.Models;
using PkgViewer.Core.Probing;
using Xunit;

namespace PkgViewer.Tests;

/// <summary>
/// Opt-in tests against real packages. Set PKGVIEWER_PS4_SAMPLE / PKGVIEWER_PS5_SAMPLE to the
/// file paths and PKGVIEWER_VOLUME_FIXTURES to the smoke-test fixture directory to run them;
/// otherwise they pass without doing anything.
/// </summary>
public sealed class RealPackageIntegrationTests
{
    [Fact]
    public async Task Ps4Sample_OpensAndListsFiles()
    {
        string? path = Environment.GetEnvironmentVariable("PKGVIEWER_PS4_SAMPLE");
        if (string.IsNullOrWhiteSpace(path)) return;
        Assert.True(File.Exists(path), $"PS4 sample not found: {path}");

        var service = new PackageOpenService();
        PackageProbeResult probe = service.Probe(path);
        Assert.Equal(PackageFormat.Ps4Pkg, probe.Format);

        using IPackageSession session = await service.OpenAsync(path);
        Assert.Equal(PkgPlatform.Ps4, session.Info.Platform);
        Assert.Equal("CUSA09105", session.Info.TitleId);
        Assert.Contains("CUSA09105", session.Info.ContentId);
        Assert.False(string.IsNullOrWhiteSpace(session.Info.Title));
        Assert.NotEmpty(await session.GetFilesAsync(CancellationToken.None));
        Assert.NotEmpty(session.Internals);
        if ((await session.GetTrophiesAsync(CancellationToken.None)).Count == 0)
            Assert.False(string.IsNullOrWhiteSpace(session.TrophyMessage));
    }

    [Fact]
    public async Task Ps5Sample_OpensAndListsFiles()
    {
        string? path = Environment.GetEnvironmentVariable("PKGVIEWER_PS5_SAMPLE");
        if (string.IsNullOrWhiteSpace(path)) return;
        Assert.True(File.Exists(path), $"PS5 sample not found: {path}");

        var service = new PackageOpenService();
        PackageProbeResult probe = service.Probe(path);
        Assert.Equal(PackageFormat.Ps5Pkg, probe.Format);
        Assert.Equal(PkgPlatform.Ps5, probe.Platform);

        using IPackageSession session = await service.OpenAsync(path);
        Assert.Equal(PkgPlatform.Ps5, session.Info.Platform);
        Assert.Equal("PPSA01280", session.Info.TitleId);
        Assert.Contains("PPSA01280", session.Info.ContentId);
        Assert.NotEmpty(session.Internals);
    }

    [Theory]
    [InlineData("game.exfat", PackageFormat.ExfatImage)]
    [InlineData("game.ffpkg", PackageFormat.Ffpkg)]
    [InlineData("game.ffpfsc", PackageFormat.Ffpfsc)]
    public async Task VolumeFixture_Opens(string fileName, PackageFormat expectedFormat)
    {
        string? directory = Environment.GetEnvironmentVariable("PKGVIEWER_VOLUME_FIXTURES");
        if (string.IsNullOrWhiteSpace(directory)) return;
        string path = Path.Combine(directory, fileName);
        Assert.True(File.Exists(path), $"Volume fixture not found: {path}");

        var service = new PackageOpenService();
        PackageProbeResult probe = service.Probe(path);
        Assert.Equal(expectedFormat, probe.Format);
        Assert.Equal(PkgPlatform.Ps5, probe.Platform);

        using IPackageSession session = await service.OpenAsync(path);
        Assert.Equal(PkgPlatform.Ps5, session.Info.Platform);
        Assert.Equal("PPSA55555", session.Info.TitleId);
        Assert.Equal("Volume Fixture", session.Info.Title);
        IReadOnlyList<PackageFileRecord> files = await session.GetFilesAsync(CancellationToken.None);
        Assert.Contains(files, file => file.Path == "sce_sys/param.json");

        string destination = Path.Combine(Path.GetTempPath(), "PkgViewerTests-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            PackageFileRecord param = files.First(file => file.Path == "sce_sys/param.json");
            await session.ExtractFileAsync(param.Path, destination, null, CancellationToken.None);
            Assert.Equal(param.Size, new FileInfo(destination).Length);
            Assert.Contains("PPSA55555", await File.ReadAllTextAsync(destination));
        }
        finally
        {
            File.Delete(destination);
        }
    }

    [Fact]
    public async Task Ps4Sample_ExtractsOneFile()
    {
        string? path = Environment.GetEnvironmentVariable("PKGVIEWER_PS4_SAMPLE");
        if (string.IsNullOrWhiteSpace(path)) return;
        Assert.True(File.Exists(path), $"PS4 sample not found: {path}");

        var service = new PackageOpenService();
        using IPackageSession session = await service.OpenAsync(path);
        IReadOnlyList<PackageFileRecord> files = await session.GetFilesAsync(CancellationToken.None);
        PackageFileRecord? file = files
            .Where(candidate => !candidate.IsDirectory)
            .OrderBy(candidate => candidate.Size)
            .FirstOrDefault(candidate => candidate.Size is > 0 and < 16_000_000);
        Assert.NotNull(file);

        string destination = Path.Combine(Path.GetTempPath(), "PkgViewerTests-" + Guid.NewGuid().ToString("N") + ".bin");
        try
        {
            await session.ExtractFileAsync(file!.Path, destination, null, CancellationToken.None);
            Assert.True(File.Exists(destination));
            Assert.Equal(file.Size, new FileInfo(destination).Length);
        }
        finally
        {
            File.Delete(destination);
        }
    }
}
