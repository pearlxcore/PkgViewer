using OrbisPkgTool;
using OrbisPkgTool.Pkg;
using OrbisPkgTool.Sfo;
using PkgViewer.Core.Models;
using PkgViewer.Core.Trophies;
using PS4PKGTool.Utilities.PkgMeta;

namespace PkgViewer.Core.Backends;

/// <summary>PS4 .pkg reader built on OrbisPkgTool via the PS4PKGTool.Core metadata facade.</summary>
public sealed class Ps4PackageBackend : IPackageBackend
{
    public bool CanOpen(PackageFormat format) => format == PackageFormat.Ps4Pkg;

    public Task<IPackageSession> OpenAsync(string path, PackageFormat format,
        PackageOpenOptions options, CancellationToken cancellationToken) =>
        Task.Run<IPackageSession>(() => new Ps4PackageSession(path, options.Passcode), cancellationToken);
}

internal sealed class Ps4PackageSession : IPackageSession
{
    private readonly string _path;
    private readonly PkgMetadata _metadata;
    private readonly PkgReader _reader;
    private readonly List<PkgFileEntry> _fileEntries;
    private readonly List<string> _warnings = [];
    private string? _tempDirectory;
    private bool _disposed;

    public Ps4PackageSession(string path, string? passcode)
    {
        _path = Path.GetFullPath(path);
        _metadata = PkgMetadataReader.Read(_path);
        _reader = new PkgReader(_path, passcode ?? PkgReader.DefaultPasscode);

        try
        {
            _fileEntries = _reader.ListFiles();
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or NotSupportedException)
        {
            _fileEntries = [];
            _warnings.Add($"File listing failed: {ex.Message}");
        }

        var files = new List<PackageFileRecord>(_fileEntries.Count);
        long contentSize = 0;
        foreach (PkgFileEntry entry in _fileEntries)
        {
            files.Add(new PackageFileRecord(Normalize(entry.Path), entry.IsDirectory, entry.Size));
            if (!entry.IsDirectory) contentSize += entry.Size;
        }

        Files = files;
        Info = BuildInfo(contentSize);
        Artwork = BuildArtwork();
        HeaderFields = BuildHeaderFields();
        Internals = HeaderFields;
        BuildInfoFields = BuildBuildInfoFields();
        SfoEntries = BuildSfoEntries();
        EntryRecords = BuildEntryRecords();
        (Trophies, TrophyMessage) = Ps4TrophyReader.Read(_path, passcode);

        if (_metadata.PKGState != PkgBuildState.Fake)
            _warnings.Add("Official package: content protected by package keys may not be readable.");
    }

    public PackageInfo Info { get; }
    public PackageArtwork Artwork { get; }
    public IReadOnlyList<PackageFileRecord> Files { get; }
    public IReadOnlyList<PackageInfoRow> Internals { get; }
    public IReadOnlyList<PackageInfoRow> HeaderFields { get; }
    public IReadOnlyList<PackageInfoRow> BuildInfoFields { get; }
    public IReadOnlyList<PackageSfoEntry> SfoEntries { get; }
    public IReadOnlyList<PackageEntryRecord> EntryRecords { get; }
    public IReadOnlyList<PackageTrophy> Trophies { get; }
    public string TrophyMessage { get; }
    public IReadOnlyList<PackageDetailTab> DetailTabs => [];
    public IReadOnlyList<JsonTreeNode> ParameterJsonTree => [];
    public IReadOnlyList<string> Warnings => _warnings;

    // PS4 listing/trophies are computed eagerly (cheap), so the lazy getters are already satisfied.
    public Task<IReadOnlyList<PackageFileRecord>> GetFilesAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Files);

    public Task<IReadOnlyList<PackageTrophy>> GetTrophiesAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Trophies);

    public Task<IReadOnlyList<PackageDetailTab>> GetDetailTabsAsync(CancellationToken cancellationToken) =>
        Task.FromResult(DetailTabs);

    public Stream OpenFile(string relativePath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        string normalized = Normalize(relativePath);
        string destination = GetTempFilePath(normalized);
        _reader.ExtractFileTo(normalized, destination);
        return new FileStream(destination, FileMode.Open, FileAccess.Read, FileShare.Read,
            1024 * 1024, FileOptions.SequentialScan);
    }

    public Task ExtractFileAsync(string relativePath, string destinationPath,
        IProgress<long>? progress, CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            cancellationToken.ThrowIfCancellationRequested();
            PackageExtractionResult result = ExtractEntry(relativePath, destinationPath,
                PackageConflictPolicy.Replace, cancellationToken);
            if (result.Outcome == PackageExtractionOutcome.Failed)
                throw new IOException(result.Error ?? "Extraction failed.");
            ReportLength(progress, result.DestinationPath);
        }, cancellationToken);

    public Task<PackageExtractionResult> ExtractAsync(PackageExtractionRequest request,
        IProgress<long>? progress, CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            cancellationToken.ThrowIfCancellationRequested();
            string destination = PackagePath.ResolveInside(request.DestinationRoot, request.PackageRelativePath);
            PackageExtractionResult result = ExtractEntry(request.PackageRelativePath, destination,
                request.ConflictPolicy, cancellationToken);
            if (result.Outcome is PackageExtractionOutcome.Extracted
                or PackageExtractionOutcome.Replaced or PackageExtractionOutcome.KeptBoth)
                ReportLength(progress, result.DestinationPath);
            return result;
        }, cancellationToken);

    public Task<PackageExtractSummary> ExtractAllAsync(string destinationDirectory, PackageConflictPolicy conflictPolicy,
        IProgress<PackageExtractProgress>? progress, CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            Directory.CreateDirectory(destinationDirectory);
            PackageFileRecord[] files = Files.Where(file => !file.IsDirectory).ToArray();
            int done = 0, extracted = 0, skipped = 0, failed = 0;
            var errors = new List<string>();
            foreach (PackageFileRecord file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new PackageExtractProgress(done, files.Length, file.Path));
                PackageExtractionResult result;
                try
                {
                    string destination = PackagePath.ResolveInside(destinationDirectory, file.Path);
                    result = ExtractEntry(file.Path, destination, conflictPolicy, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    result = new PackageExtractionResult(PackageExtractionOutcome.Failed, file.Path, ex.Message);
                }

                switch (result.Outcome)
                {
                    case PackageExtractionOutcome.Extracted:
                    case PackageExtractionOutcome.Replaced:
                    case PackageExtractionOutcome.KeptBoth:
                        extracted++;
                        break;
                    case PackageExtractionOutcome.Skipped:
                        skipped++;
                        break;
                    default:
                        failed++;
                        errors.Add($"{file.Path}: {result.Error}");
                        break;
                }
                done++;
            }
            progress?.Report(new PackageExtractProgress(done, files.Length, string.Empty));
            return new PackageExtractSummary(extracted, skipped, failed, errors);
        }, cancellationToken);

    /// <summary>Writes one entry to a temp file and moves it into place, honoring the conflict policy.</summary>
    private PackageExtractionResult ExtractEntry(string relativePath, string destinationPath,
        PackageConflictPolicy policy, CancellationToken cancellationToken)
    {
        if (!PackagePath.TryNormalize(relativePath, out string normalized, out string? reason))
            return new PackageExtractionResult(PackageExtractionOutcome.Failed, destinationPath, $"unsafe path ({reason})");

        bool existed = File.Exists(destinationPath) || Directory.Exists(destinationPath);
        string target = destinationPath;
        PackageExtractionOutcome outcome;
        switch (policy)
        {
            case PackageConflictPolicy.Fail when existed:
                return new PackageExtractionResult(PackageExtractionOutcome.Failed, destinationPath, "the destination already exists");
            case PackageConflictPolicy.Skip when existed:
                return new PackageExtractionResult(PackageExtractionOutcome.Skipped, destinationPath);
            case PackageConflictPolicy.KeepBoth when existed:
                target = PackagePath.MakeUnique(destinationPath);
                outcome = PackageExtractionOutcome.KeptBoth;
                break;
            default:
                outcome = existed ? PackageExtractionOutcome.Replaced : PackageExtractionOutcome.Extracted;
                break;
        }

        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            string? parent = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
            string temp = target + ".partial-" + Guid.NewGuid().ToString("N");
            try
            {
                _reader.ExtractFileTo(normalized, temp);
                File.Move(temp, target, overwrite: true);
            }
            catch
            {
                TryDeleteFile(temp);
                throw;
            }
            return new PackageExtractionResult(outcome, target);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or NotSupportedException)
        {
            return new PackageExtractionResult(PackageExtractionOutcome.Failed, target, ex.Message);
        }
    }

    private static void ReportLength(IProgress<long>? progress, string path)
    {
        try { progress?.Report(new FileInfo(path).Length); }
        catch (IOException) { }
    }

    private static void TryDeleteFile(string path)
    {
        try { File.Delete(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _reader.Dispose();
        if (_tempDirectory is not null)
        {
            try { Directory.Delete(_tempDirectory, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private PackageInfo BuildInfo(long contentSize)
    {
        PkgInfo info = _reader.GetInfo();
        var extras = new List<PackageInfoRow>
        {
            new("Region", string.IsNullOrEmpty(_metadata.Region) ? "Unknown" : _metadata.Region),
            new("PKG kind", _metadata.Kind.ToString()),
            new("Header content type", $"0x{_metadata.Header.ContentType:X8}"),
            new("Header content flags", $"0x{_metadata.Header.ContentFlags:X8}")
        };

        return new PackageInfo
        {
            Platform = PkgPlatform.Ps4,
            Format = PackageFormat.Ps4Pkg,
            SourcePath = _path,
            FileName = Path.GetFileName(_path),
            FileSize = new FileInfo(_path).Length,
            Title = _metadata.PS4_Title,
            TitleId = _metadata.TITLEID,
            ContentId = _metadata.Content_ID,
            Version = _metadata.APP_VER,
            PackageVersion = _metadata.ParamSfo?.GetString("VERSION") ?? string.Empty,
            Category = _metadata.Category,
            BuildState = DescribeBuildState(_metadata.PKGState),
            RequiredFirmware = info.SystemVersion,
            FileCount = Files.Count(file => !file.IsDirectory),
            ContentSize = contentSize,
            ExtraRows = extras
        };
    }

    private PackageArtwork BuildArtwork() => new()
    {
        Icon = ToImage(_metadata.Icon),
        Pic0 = ToImage(_metadata.Pic0),
        Pic1 = ToImage(_metadata.Pic1)
    };

    private List<PackageInfoRow> BuildHeaderFields()
    {
        var rows = new List<PackageInfoRow>();
        var headerBytes = new byte[PkgHeaderDump.HeaderBytes];
        try
        {
            using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete, 1024 * 1024, FileOptions.RandomAccess);
            stream.ReadExactly(headerBytes);
            foreach ((string type, string value) in PkgHeaderDump.Rows(_reader.Header, headerBytes))
                rows.Add(new PackageInfoRow(type, value));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            rows.Add(new PackageInfoRow("Header", $"unavailable: {ex.Message}"));
        }
        return rows;
    }

    private IReadOnlyList<PackageSfoEntry> BuildSfoEntries()
    {
        if (_metadata.SfoTables is not { Count: > 0 } tables) return [];
        var list = new List<PackageSfoEntry>(tables.Count);
        foreach (SfoTable table in tables)
            list.Add(new PackageSfoEntry(table.Name, table.Value));
        return list;
    }

    private IReadOnlyList<PackageInfoRow> BuildBuildInfoFields() =>
        ParsePubToolInfo(_metadata.ParamSfo?.GetString("PUBTOOLINFO") ?? string.Empty);

    private static List<PackageInfoRow> ParsePubToolInfo(string value)
    {
        var rows = new List<PackageInfoRow>();
        if (string.IsNullOrWhiteSpace(value)) return rows;
        string[] tokens = value.Split(',');
        for (int i = tokens.Length - 1; i >= 0; i--)
        {
            string token = tokens[i];
            int separator = token.IndexOf('=');
            if (separator <= 0) continue;
            string key = token[..separator].Trim();
            string fieldValue = token[(separator + 1)..].Trim();
            if (key.Length == 0) continue;
            string label = key.ToLowerInvariant() switch
            {
                "c_date" => "Creation Date",
                "sdk_ver" => "PS4 SDK Version",
                "st_type" => "Storage Type",
                "c_time" => "Creation Time",
                _ => key
            };
            rows.Add(new PackageInfoRow(label, fieldValue));
        }
        return rows;
    }

    private IReadOnlyList<PackageEntryRecord> BuildEntryRecords()
    {
        var list = new List<PackageEntryRecord>(_reader.Entries.Count);
        foreach (PkgEntry entry in _reader.Entries)
        {
            string name = PkgEntryNames.TryGetName(entry.Id) ?? $"0x{entry.Id:X8}";
            list.Add(new PackageEntryRecord(
                name,
                $"0x{entry.DataOffset:X}",
                FormatByteSize(entry.DataSize),
                $"0x{entry.Flags1:X}",
                $"0x{entry.Flags2:X}",
                entry.IsEncrypted ? "1" : "0"));
        }
        return list;
    }

    private static string FormatByteSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} bytes";
        string[] units = ["KB", "MB", "GB", "TB"];
        double value = bytes;
        int unit = -1;
        do
        {
            value /= 1024;
            unit++;
        } while (value >= 1024 && unit < units.Length - 1);
        return $"{value:0.##} {units[unit]}";
    }

    private string GetTempFilePath(string normalizedPath)
    {
        _tempDirectory ??= Directory.CreateTempSubdirectory("PkgViewer").FullName;
        string relative = normalizedPath.Replace('/', Path.DirectorySeparatorChar);
        string fullPath = Path.GetFullPath(Path.Combine(_tempDirectory, relative));
        string prefix = _tempDirectory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new IOException("The package entry path leaves the temporary directory.");
        string? parent = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
        return fullPath;
    }

    private static PackageImage? ToImage(byte[]? bytes) =>
        bytes is { Length: > 0 } ? PackageImage.Png(bytes) : null;

    private static string Normalize(string path) => path.Replace('\\', '/').TrimStart('/');

    private static string DescribeBuildState(PkgBuildState state) => state switch
    {
        PkgBuildState.Fake => "Fake (FPKG)",
        PkgBuildState.Official => "Official",
        PkgBuildState.Official_DP => "Official (DP)",
        _ => "Unknown"
    };
}
