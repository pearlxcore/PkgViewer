using PkgViewer.Core.Models;
using PkgViewer.Core.Probing;

namespace PkgViewer.Core.Backends;

public sealed class PackageOpenOptions
{
    /// <summary>Package passcode for protected PS4 PKGs; null uses the standard all-zero passcode.</summary>
    public string? Passcode { get; init; }
}

public sealed record PackageExtractProgress(int FilesDone, int FileCount, string CurrentFile);

/// <summary>
/// An opened package. Metadata, artwork and the raw internals are available immediately; the file
/// listing, trophies and platform extra tabs are loaded lazily (the first PS5 FPKG read decodes the
/// inner image, so the fast path deliberately avoids it).
/// </summary>
public interface IPackageSession : IDisposable
{
    PackageInfo Info { get; }
    PackageArtwork Artwork { get; }
    IReadOnlyList<PackageInfoRow> Internals { get; }
    IReadOnlyList<PackageInfoRow> HeaderFields { get; }
    IReadOnlyList<PackageInfoRow> BuildInfoFields { get; }
    IReadOnlyList<PackageSfoEntry> SfoEntries { get; }
    IReadOnlyList<PackageEntryRecord> EntryRecords { get; }

    /// <summary>Explains why the trophy list is empty (empty when trophies loaded).</summary>
    string TrophyMessage { get; }

    /// <summary>param.json as a tree for PS5 packages (empty for PS4, which uses PARAM.SFO).</summary>
    IReadOnlyList<JsonTreeNode> ParameterJsonTree { get; }
    IReadOnlyList<string> Warnings { get; }

    /// <summary>Loads (once, cached) the package's file listing.</summary>
    Task<IReadOnlyList<PackageFileRecord>> GetFilesAsync(CancellationToken cancellationToken);

    /// <summary>Loads (once, cached) the package's trophies.</summary>
    Task<IReadOnlyList<PackageTrophy>> GetTrophiesAsync(CancellationToken cancellationToken);

    /// <summary>Loads (once, cached) platform-specific extra tabs (PS5 activities/executable).</summary>
    Task<IReadOnlyList<PackageDetailTab>> GetDetailTabsAsync(CancellationToken cancellationToken);

    /// <summary>Opens one package file for reading. The caller owns the returned stream.</summary>
    Stream OpenFile(string relativePath);

    Task ExtractFileAsync(string relativePath, string destinationPath,
        IProgress<long>? progress, CancellationToken cancellationToken);

    Task ExtractAllAsync(string destinationDirectory,
        IProgress<PackageExtractProgress>? progress, CancellationToken cancellationToken);
}

public interface IPackageBackend
{
    bool CanOpen(PackageFormat format);

    Task<IPackageSession> OpenAsync(string path, PackageFormat format,
        PackageOpenOptions options, CancellationToken cancellationToken);
}

/// <summary>Raised when no backend could open a file; carries the probe result and per-format errors.</summary>
public sealed class PackageOpenException : Exception
{
    public PackageOpenException(string path, PackageProbeResult probe, IReadOnlyList<string> failures)
        : base(BuildMessage(path, probe, failures))
    {
        PackagePath = path;
        Probe = probe;
        Failures = failures;
    }

    public string PackagePath { get; }
    public PackageProbeResult Probe { get; }
    public IReadOnlyList<string> Failures { get; }

    private static string BuildMessage(string path, PackageProbeResult probe, IReadOnlyList<string> failures)
    {
        string message = $"Could not open '{path}'. Detection: {probe.Reason}";
        if (failures.Count > 0)
            message += Environment.NewLine + string.Join(Environment.NewLine, failures);
        return message;
    }
}
