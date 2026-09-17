using PkgViewer.Core.Models;
using PS5PKGTool.Core.Models;
using PS5PKGTool.Core.Parsers;
using PS5PKGTool.Core.Services;
using PS5PKGTool.Ffpfsc;
using UFS2Tool;

namespace PkgViewer.Core.Backends;

/// <summary>
/// PS5 reader covering all supported container formats. .pkg/.ffpfsc go through the
/// PS5PKGTool.Core readers; .ffpkg (UFS2) and .exfat are read directly so detection never
/// depends on the file extension.
/// </summary>
public sealed class Ps5PackageBackend : IPackageBackend
{
    public bool CanOpen(PackageFormat format) => format is PackageFormat.Ps5Pkg
        or PackageFormat.Ffpfsc or PackageFormat.Ffpkg or PackageFormat.ExfatImage;

    public Task<IPackageSession> OpenAsync(string path, PackageFormat format,
        PackageOpenOptions options, CancellationToken cancellationToken) =>
        Task.Run<IPackageSession>(() =>
        {
            Ps5GameInfo game = format switch
            {
                PackageFormat.Ps5Pkg => new SonyPkgGameReader().Read(path),
                PackageFormat.Ffpfsc => new FfpfscGameReader().Read(path),
                PackageFormat.Ffpkg => ReadFfpkg(path),
                PackageFormat.ExfatImage => ReadExfatImage(path),
                _ => throw new NotSupportedException($"The PS5 backend does not handle {format}.")
            };

            // Metadata/artwork only; the inner-image decode happens lazily (see Ps5PackageSession).
            return new Ps5PackageSession(path, format, game);
        }, cancellationToken);

    // The PS5PKGTool.Core readers are deliberately extension-gated for .ffpkg/.exfat; the viewer
    // identifies those formats by signature, so the read paths are repeated here without the gate.
    private static Ps5GameInfo ReadFfpkg(string path)
    {
        string fullPath = Path.GetFullPath(path);
        using var volume = new Ufs2Volume(fullPath);
        (string paramPath, string virtualRoot) = ResolveParamEntry(
            volume.Entries.Select(entry => (entry.Path, entry.IsDirectory)), "The FFPKG");
        string raw = volume.ReadAllText(paramPath);
        var info = new FileInfo(fullPath);
        Ps5GameInfo game = new Ps5ParamReader().ReadJson(raw, paramPath, fullPath, info.LastWriteTimeUtc);
        game.SourceKind = Ps5SourceKind.Ffpkg;
        game.RootPath = fullPath;
        game.ParamPath = paramPath;
        game.VirtualRoot = virtualRoot;
        game.SourceSize = info.Length;
        game.ContainerInnerFileName = info.Name;
        game.ContainerLogicalSize = volume.Entries.Where(entry => !entry.IsDirectory).Sum(entry => entry.Size);
        game.ContainerStoredSize = info.Length;
        return game;
    }

    private static Ps5GameInfo ReadExfatImage(string path)
    {
        string fullPath = Path.GetFullPath(path);
        using var image = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read,
            1024 * 1024, FileOptions.RandomAccess);
        using var volume = new ExfatVolume(image, leaveOpen: true);
        (string paramPath, string virtualRoot) = ResolveParamEntry(
            volume.Entries.Select(entry => (entry.Path, entry.IsDirectory)), "The filesystem image");
        string raw = volume.ReadAllText(paramPath);
        var fileInfo = new FileInfo(fullPath);
        Ps5GameInfo game = new Ps5ParamReader().ReadJson(raw, paramPath, fullPath, fileInfo.LastWriteTimeUtc);
        game.SourceKind = Ps5SourceKind.FilesystemImage;
        game.RootPath = fullPath;
        game.ParamPath = paramPath;
        game.VirtualRoot = virtualRoot;
        game.SourceSize = fileInfo.Length;
        game.ContainerInnerFileName = fileInfo.Name;
        game.ContainerLogicalSize = fileInfo.Length;
        game.ContainerStoredSize = fileInfo.Length;
        return game;
    }

    private static (string ParamPath, string VirtualRoot) ResolveParamEntry(
        IEnumerable<(string Path, bool IsDirectory)> entries, string description)
    {
        const string suffix = "sce_sys/param.json";
        (string Path, bool IsDirectory)[] matches = entries
            .Where(entry => !entry.IsDirectory &&
                (entry.Path.Equals(suffix, StringComparison.OrdinalIgnoreCase) ||
                 entry.Path.EndsWith("/" + suffix, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        if (matches.Length == 0)
            throw new InvalidDataException($"{description} has no sce_sys/param.json.");
        if (matches.Length > 1)
            throw new InvalidDataException($"{description} contains multiple PS5 game roots.");

        string paramPath = matches[0].Path;
        string virtualRoot = paramPath.Length == suffix.Length
            ? string.Empty
            : paramPath[..^(suffix.Length + 1)];
        return (paramPath, virtualRoot);
    }
}

internal sealed class Ps5PackageSession : IPackageSession
{
    private const int MaximumImageBytes = 128 * 1024 * 1024;
    private const uint ParamSfoId = 0x1000;
    private const uint Icon0PngId = 0x1200;
    private const uint Icon0DdsId = 0x1280;
    private const uint Pic0PngId = 0x1220;
    private const uint Pic0DdsId = 0x12A0;
    private const uint Pic1DdsId = 0x12C0;
    private const uint Pic2PngId = 0x2040;
    private const uint Pic2DdsId = 0x2060;

    private readonly string _path;
    private readonly Ps5GameInfo _game;
    private readonly List<string> _warnings = [];
    private readonly object _gate = new();
    private IReadOnlyGameFileSystem? _fileSystem;
    private Task<IReadOnlyList<PackageFileRecord>>? _filesTask;
    private Task<IReadOnlyList<PackageTrophy>>? _trophiesTask;
    private Task<IReadOnlyList<PackageDetailTab>>? _detailTabsTask;
    private volatile string _trophyMessage = string.Empty;
    private bool _disposed;

    public Ps5PackageSession(string path, PackageFormat format, Ps5GameInfo game)
    {
        _path = Path.GetFullPath(path);
        _game = game;

        // Fast path: everything below comes from the header/CNT/param.json and never decodes the
        // inner image, so the Overview can render immediately.
        Info = BuildInfo(format);
        HeaderFields = BuildHeaderFields();
        Internals = HeaderFields;
        BuildInfoFields = BuildBuildInfoFields();
        SfoEntries = BuildSfoEntries();
        EntryRecords = BuildEntryRecords();
        ParameterJsonTree = JsonTree.Build(_game.RawParamJson);
        Artwork = BuildArtwork();

        _warnings.AddRange(game.DataWarnings);
    }

    public PackageInfo Info { get; }
    public PackageArtwork Artwork { get; }
    public IReadOnlyList<PackageInfoRow> Internals { get; }
    public IReadOnlyList<PackageInfoRow> HeaderFields { get; }
    public IReadOnlyList<PackageInfoRow> BuildInfoFields { get; }
    public IReadOnlyList<PackageSfoEntry> SfoEntries { get; }
    public IReadOnlyList<PackageEntryRecord> EntryRecords { get; }
    public IReadOnlyList<JsonTreeNode> ParameterJsonTree { get; }
    public string TrophyMessage => _trophyMessage;
    public IReadOnlyList<string> Warnings => _warnings;

    // A failed or cancelled load is not cached: the next visit retries with a fresh task instead of
    // replaying the same failure.
    public Task<IReadOnlyList<PackageFileRecord>> GetFilesAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_filesTask is null || _filesTask.IsFaulted || _filesTask.IsCanceled)
                _filesTask = Task.Run(LoadFiles, cancellationToken);
            return _filesTask;
        }
    }

    public Task<IReadOnlyList<PackageTrophy>> GetTrophiesAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_trophiesTask is null || _trophiesTask.IsFaulted || _trophiesTask.IsCanceled)
                _trophiesTask = Task.Run(LoadTrophies, cancellationToken);
            return _trophiesTask;
        }
    }

    public Task<IReadOnlyList<PackageDetailTab>> GetDetailTabsAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_detailTabsTask is null || _detailTabsTask.IsFaulted || _detailTabsTask.IsCanceled)
                _detailTabsTask = Task.Run(() => LoadDetailTabs(cancellationToken), cancellationToken);
            return _detailTabsTask;
        }
    }

    public Stream OpenFile(string relativePath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        string normalized = GameFileSystem.NormalizePath(relativePath);
        return EnsureFileSystem().OpenRead(normalized);
    }

    public async Task ExtractFileAsync(string relativePath, string destinationPath,
        IProgress<long>? progress, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!PackagePath.TryNormalize(relativePath, out _, out string? reason))
            throw new IOException($"The package entry path is not safe to extract ({reason}).");
        await ExtractEntryAsync(relativePath, destinationPath, PackageConflictPolicy.Replace, progress, cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<PackageExtractionResult> ExtractAsync(PackageExtractionRequest request,
        IProgress<long>? progress, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        string destination = PackagePath.ResolveInside(request.DestinationRoot, request.PackageRelativePath);
        return ExtractEntryAsync(request.PackageRelativePath, destination, request.ConflictPolicy, progress, cancellationToken);
    }

    public async Task<PackageExtractSummary> ExtractAllAsync(string destinationDirectory,
        PackageConflictPolicy conflictPolicy, IProgress<PackageExtractProgress>? progress, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Directory.CreateDirectory(destinationDirectory);
        IReadOnlyList<PackageFileRecord> all = await GetFilesAsync(cancellationToken).ConfigureAwait(false);
        PackageFileRecord[] files = all.Where(file => !file.IsDirectory).ToArray();
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
                result = await ExtractEntryAsync(file.Path, destination, conflictPolicy, null, cancellationToken)
                    .ConfigureAwait(false);
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
    }

    /// <summary>
    /// Writes one entry to a temporary file and only then moves it into place, so a cancelled or
    /// failed extraction never leaves a truncated replacement. The relative path is normalized and
    /// the destination is already contained by the caller.
    /// </summary>
    private async Task<PackageExtractionResult> ExtractEntryAsync(string relativePath, string destinationPath,
        PackageConflictPolicy policy, IProgress<long>? progress, CancellationToken cancellationToken)
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

        try
        {
            IReadOnlyGameFileSystem files = EnsureFileSystem();
            await using Stream input = files.OpenRead(normalized);
            string? parent = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
            string temp = target + ".partial-" + Guid.NewGuid().ToString("N");
            try
            {
                await using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write,
                    FileShare.None, 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    // Rent the copy buffer so many-small-file extraction does not allocate per entry.
                    byte[] buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(1024 * 1024);
                    try
                    {
                        long copied = 0;
                        while (true)
                        {
                            int read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                            if (read == 0) break;
                            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                            copied += read;
                            progress?.Report(copied);
                        }
                    }
                    finally
                    {
                        System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
                    }
                }
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
        catch (Exception ex)
        {
            return new PackageExtractionResult(PackageExtractionOutcome.Failed, target, ex.Message);
        }
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
        lock (_gate) _fileSystem?.Dispose();
    }

    private IReadOnlyGameFileSystem EnsureFileSystem()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_gate)
            return _fileSystem ??= GameFileSystem.Open(_game);
    }

    // ------------------------------------------------------------------
    // Lazy loading (first access decodes the inner image for PS5 FPKGs)
    // ------------------------------------------------------------------

    private IReadOnlyList<PackageFileRecord> LoadFiles()
    {
        IReadOnlyGameFileSystem files = EnsureFileSystem();
        return files.Files
            .Select(file => new PackageFileRecord(Normalize(file.RelativePath), false, file.Size))
            .ToArray();
    }

    private IReadOnlyList<PackageTrophy> LoadTrophies()
    {
        try
        {
            IReadOnlyGameFileSystem files = EnsureFileSystem();
            Ps5TrophySet? set = new Ps5TrophyReader().Read(files, _game.DefaultLanguage, CancellationToken.None);
            if (set is not { Trophies.Count: > 0 })
            {
                _trophyMessage = "No trophy data was found in this package.";
                return [];
            }
            _trophyMessage = string.Empty;
            return set.Trophies.Select(trophy => new PackageTrophy
            {
                Id = trophy.Id,
                Name = trophy.Name,
                Description = trophy.Description,
                Grade = trophy.Grade,
                Hidden = trophy.Hidden,
                Icon = ToImage(trophy.Icon)
            }).ToArray();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _trophyMessage = "Trophy information could not be read. " + ex.Message;
            return [];
        }
    }

    private IReadOnlyList<PackageDetailTab> LoadDetailTabs(CancellationToken cancellationToken)
    {
        var tabs = new List<PackageDetailTab>();
        IReadOnlyGameFileSystem files = EnsureFileSystem();
        try
        {
            Ps5UdsSummary? uds = new Ps5UdsReader().Read(files, cancellationToken);
            if (uds is not null) tabs.Add(new PackageDetailTab("Activities", BuildActivityRows(uds)));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _warnings.Add("Activities: " + ex.Message);
        }
        try
        {
            Ps5SelfInfo? executable = new Ps5SelfReader().Read(files, cancellationToken);
            if (executable is not null)
                tabs.Add(new PackageDetailTab("Executable", BuildExecutableRows(executable)));
            else if (_game.SourceKind == Ps5SourceKind.SonyPackage)
                _warnings.Add("Executable: eboot.bin is not available (the package's inner image could not be read).");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _warnings.Add("Executable: " + ex.Message);
        }
        return tabs;
    }

    // ------------------------------------------------------------------
    // Fast metadata
    // ------------------------------------------------------------------

    private PackageInfo BuildInfo(PackageFormat format)
    {
        var extras = new List<PackageInfoRow>();
        Add(extras, "Source", _game.SourceDescription);
        Add(extras, "Concept ID", _game.ConceptId);
        Add(extras, "Master version", _game.MasterVersion);
        Add(extras, "Target content version", _game.TargetContentVersion);
        Add(extras, "Origin content version", _game.OriginContentVersion);
        Add(extras, "DRM type", _game.DrmType);
        Add(extras, "Default language", _game.DefaultLanguage);
        Add(extras, "Creation date", _game.CreationDate);
        Add(extras, "Tool version", _game.ToolVersion);
        if (_game.DownloadDataSize > 0)
            extras.Add(new PackageInfoRow("Download data size", FormatBytes(_game.DownloadDataSize)));
        if (_game.LocalizedTitles.Count > 0)
            extras.Add(new PackageInfoRow("Localized titles", $"{_game.LocalizedTitles.Count} language(s)"));
        Add(extras, "Container inner file", _game.ContainerInnerFileName);
        if (_game.ContainerLogicalSize > 0)
            extras.Add(new PackageInfoRow("Container logical size", FormatBytes(_game.ContainerLogicalSize)));
        if (_game.ContainerStoredSize > 0)
            extras.Add(new PackageInfoRow("Container stored size", FormatBytes(_game.ContainerStoredSize)));
        if (_game.ContainerBlockCount > 0)
            extras.Add(new PackageInfoRow("Container blocks", _game.ContainerBlockCount.ToString("N0")));
        Add(extras, "Virtual root", _game.VirtualRoot);

        return new PackageInfo
        {
            Platform = PkgPlatform.Ps5,
            Format = format,
            SourcePath = _path,
            FileName = Path.GetFileName(_path),
            FileSize = _game.SourceSize > 0 ? _game.SourceSize : new FileInfo(_path).Length,
            Title = _game.Title,
            TitleId = _game.TitleId,
            ContentId = _game.ContentId,
            Version = _game.DisplayVersion,
            PackageVersion = _game.ContentVersion,
            Region = PackageRegion.FromId(!string.IsNullOrWhiteSpace(_game.ContentId) ? _game.ContentId : _game.TitleId),
            Category = PackageCategory.DescribePs5(_game.ApplicationCategory,
                _game.Package?.Kind is SonyPkgKind.FinalizedPatch),
            BuildState = _game.Package?.KindDisplayName ?? _game.SourceDescription,
            RequiredFirmware = _game.RequiredSystemSoftware,
            SdkVersion = _game.SdkVersion,
            ExtraRows = extras
        };
    }

    private List<PackageInfoRow> BuildHeaderFields()
    {
        var rows = new List<PackageInfoRow>();
        Add(rows, "Source kind", _game.SourceKind.ToString());
        Add(rows, "Content ID", _game.ContentId);
        Add(rows, "Title ID", _game.TitleId);
        Add(rows, "Content version", _game.ContentVersion);
        Add(rows, "Application category", PackageCategory.NormalizeNumeric(_game.ApplicationCategory));
        Add(rows, "Required system software", _game.RequiredSystemSoftware);
        Add(rows, "SDK version", _game.SdkVersion);
        Add(rows, "DRM type", _game.DrmType);
        Add(rows, "Creation date", _game.CreationDate);
        Add(rows, "Param path", _game.ParamPath);

        if (_game.Package is { } package)
        {
            Add(rows, "PKG kind", package.KindDisplayName);
            if (package.SignedByte is { } signedByte) Add(rows, "FIH signed byte", $"0x{signedByte:X2}");
            if (package.FormatVersion is { } formatVersion) Add(rows, "FIH format version", formatVersion.ToString());
            Add(rows, "PFS image offset", $"0x{package.PfsImageOffset:X}");
            Add(rows, "PFS image size", $"{package.PfsImageSize:N0}");
            if (package.PfsSuperblockOffset != 0) Add(rows, "PFS superblock offset", $"0x{package.PfsSuperblockOffset:X}");
            Add(rows, "Embedded CNT offset", $"0x{package.EmbeddedCntOffset:X}");
            Add(rows, "CNT header flags", $"0x{package.HeaderFlags:X}");
            Add(rows, "CNT body offset", $"0x{package.BodyOffset:X}");
            Add(rows, "CNT body size", $"{package.BodySize:N0}");
            Add(rows, "CNT content type", $"0x{package.ContentType:X8}");
            Add(rows, "CNT content flags", $"0x{package.ContentFlags:X8}");
            Add(rows, "Encrypted entries", package.EncryptedEntryCount.ToString("N0"));

            foreach (SonyPkgSegment segment in package.Segments)
                rows.Add(new PackageInfoRow($"Segment {segment.Name}", $"offset 0x{segment.Offset:X} size {segment.Size:N0}"));

            if (package.NestedPfs is { } nested)
            {
                Add(rows, "Nested PFS state", nested.AccessState.ToString());
                Add(rows, "Nested PFS status", nested.StatusMessage);
                if (nested.AccessState != SonyPfsAccessState.NotPresent)
                {
                    Add(rows, "Nested PFS offset", $"0x{nested.ImageOffset:X}");
                    Add(rows, "Nested PFS size", $"{nested.ImageSize:N0}");
                    Add(rows, "Nested PFS block size", $"{nested.BlockSize:N0}");
                    Add(rows, "Nested PFS inodes", $"{nested.InodeCount:N0}");
                    Add(rows, "Nested PFS data blocks", $"{nested.DataBlockCount:N0}");
                }
            }
        }

        return rows;
    }

    private IReadOnlyList<PackageInfoRow> BuildBuildInfoFields()
    {
        var rows = new List<PackageInfoRow>();
        Add(rows, "Creation Date", _game.CreationDate);
        Add(rows, "SDK Version", _game.SdkVersion);
        Add(rows, "Tool Version", _game.ToolVersion);
        Add(rows, "Application Category", PackageCategory.NormalizeNumeric(_game.ApplicationCategory));
        Add(rows, "DRM Type", _game.DrmType);
        Add(rows, "Content Badge Type", PackageCategory.NormalizeNumeric(_game.ContentBadgeType));
        Add(rows, "Master Version", _game.MasterVersion);
        Add(rows, "Target Content Version", _game.TargetContentVersion);
        Add(rows, "Origin Content Version", _game.OriginContentVersion);
        return rows;
    }

    private IReadOnlyList<PackageSfoEntry> BuildSfoEntries()
    {
        try
        {
            byte[]? data = _game.Package is { } package
                ? ReadPackageEntry(package, ParamSfoId)
                : ReadFileSystemBytes(EnsureFileSystem(), "sce_sys/param.sfo");
            if (data is null) return [];
            return Ps5SfoReader.Read(data)
                .Select(entry => new PackageSfoEntry(entry.Key, entry.Value))
                .ToArray();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException)
        {
            return [];
        }
    }

    private IReadOnlyList<PackageEntryRecord> BuildEntryRecords()
    {
        if (_game.Package is not { } package) return [];
        var list = new List<PackageEntryRecord>(package.Entries.Count);
        foreach (SonyPkgEntry entry in package.Entries)
        {
            list.Add(new PackageEntryRecord(
                entry.DisplayName,
                $"0x{entry.DataOffset:X}",
                FormatBytes(entry.DataSize),
                $"0x{entry.Flags1:X}",
                $"0x{entry.Flags2:X}",
                entry.IsEncrypted ? "1" : "0"));
        }
        return list;
    }

    private PackageArtwork BuildArtwork()
    {
        if (_game.Package is { } package) return BuildPackageArtwork(package);
        return BuildFileSystemArtwork();
    }

    // Artwork for a Sony package comes straight from the plaintext CNT entries, so the inner image
    // is never decoded just to show the icon/backgrounds.
    private PackageArtwork BuildPackageArtwork(SonyPkgSummary package)
    {
        try
        {
            return new PackageArtwork
            {
                Icon = ReadPackageImage(package, Icon0PngId, Icon0DdsId),
                Pic0 = ReadPackageImage(package, Pic0PngId, Pic0DdsId),
                Pic1 = ReadPackageImage(package, null, Pic1DdsId),
                Pic2 = ReadPackageImage(package, Pic2PngId, Pic2DdsId)
            };
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException)
        {
            _warnings.Add("Artwork: " + ex.Message);
            return new PackageArtwork();
        }
    }

    private PackageImage? ReadPackageImage(SonyPkgSummary package, uint? pngId, uint? ddsId)
    {
        try
        {
            var reader = new SonyPkgReader();
            if (pngId is { } pngCandidate)
            {
                SonyPkgEntry? png = package.Entries.FirstOrDefault(entry => entry.Id == pngCandidate && !entry.IsEncrypted);
                if (png is not null)
                    return PackageImage.Png(reader.ReadEntryBytes(_path, package, png, MaximumImageBytes));
            }
            if (ddsId is not { } ddsCandidate) return null;
            SonyPkgEntry? dds = package.Entries.FirstOrDefault(entry => entry.Id == ddsCandidate && !entry.IsEncrypted);
            if (dds is null) return null;
            byte[] bytes = reader.ReadEntryBytes(_path, package, dds, MaximumImageBytes);
            using var memory = new MemoryStream(bytes, writable: false);
            (byte[] rgba, int width, int height) = Ps5ImageCodec.DecodeDdsToRgba(memory);
            return width > 0 && height > 0 ? PackageImage.Rgba(rgba, width, height) : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A single unreadable texture is skipped; the remaining artwork still loads.
            return null;
        }
    }

    private byte[]? ReadPackageEntry(SonyPkgSummary package, uint id)
    {
        try
        {
            SonyPkgEntry? entry = package.Entries.FirstOrDefault(candidate =>
                candidate.Id == id && !candidate.IsEncrypted);
            return entry is null ? null : new SonyPkgReader().ReadEntryBytes(_path, package, entry, MaximumImageBytes);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException)
        {
            return null;
        }
    }

    private PackageArtwork BuildFileSystemArtwork()
    {
        try
        {
            IReadOnlyGameFileSystem files = EnsureFileSystem();
            return new PackageArtwork
            {
                Icon = ReadFileSystemImage(files, "sce_sys/icon0"),
                Pic0 = ReadFileSystemImage(files, "sce_sys/pic0"),
                Pic1 = ReadFileSystemImage(files, "sce_sys/pic1"),
                Pic2 = ReadFileSystemImage(files, "sce_sys/pic2")
            };
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException)
        {
            _warnings.Add("Artwork: " + ex.Message);
            return new PackageArtwork();
        }
    }

    private static PackageImage? ReadFileSystemImage(IReadOnlyGameFileSystem files, string basePath)
    {
        byte[]? png = ReadFileSystemBytes(files, basePath + ".png");
        if (png is not null) return PackageImage.Png(png);
        if (!files.FileExists(basePath + ".dds")) return null;
        try
        {
            using Stream input = files.OpenRead(basePath + ".dds");
            (byte[] rgba, int width, int height) = Ps5ImageCodec.DecodeDdsToRgba(input);
            return width > 0 && height > 0 ? PackageImage.Rgba(rgba, width, height) : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    private static byte[]? ReadFileSystemBytes(IReadOnlyGameFileSystem files, string path)
    {
        try
        {
            if (!files.FileExists(path)) return null;
            using Stream input = files.OpenRead(path);
            if (input.Length > MaximumImageBytes || input.Length > int.MaxValue) return null;
            byte[] result = new byte[(int)input.Length];
            input.ReadExactly(result);
            return result;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static List<PackageInfoRow> BuildActivityRows(Ps5UdsSummary uds)
    {
        var rows = new List<PackageInfoRow>();
        Add(rows, "NP Communication ID", uds.NpCommunicationId);
        rows.Add(new PackageInfoRow("Enum groups", uds.EnumGroupCount.ToString("N0")));
        rows.Add(new PackageInfoRow("Events", uds.EventCount.ToString("N0")));
        rows.Add(new PackageInfoRow("Stats", uds.StatCount.ToString("N0")));
        rows.Add(new PackageInfoRow("Extraction rules", uds.ExtractionRuleCount.ToString("N0")));
        rows.Add(new PackageInfoRow("Integrity valid", uds.IntegrityValid ? "Yes" : "No"));
        foreach (Ps5UdsEvent item in uds.Events.Take(200))
            rows.Add(new PackageInfoRow($"Event {item.Name}", $"{item.Type} · {item.PropertyCount} property(ies)"));
        foreach (Ps5UdsStat stat in uds.Stats.Take(200))
            rows.Add(new PackageInfoRow($"Stat {stat.StatId:000} {stat.Name}", $"{stat.DataType} · {stat.Aggregation}"));
        return rows;
    }

    private static List<PackageInfoRow> BuildExecutableRows(Ps5SelfInfo executable)
    {
        var rows = new List<PackageInfoRow>();
        Add(rows, "SELF magic", executable.SelfMagic);
        if (executable.FileSize > 0)
            rows.Add(new PackageInfoRow("File size", FormatBytes(executable.FileSize)));
        rows.Add(new PackageInfoRow("ELF class", executable.ElfClass.ToString()));
        rows.Add(new PackageInfoRow("Endianness", executable.Endianness.ToString()));
        rows.Add(new PackageInfoRow("ELF type", $"0x{executable.ElfType:X}"));
        rows.Add(new PackageInfoRow("Machine", $"0x{executable.Machine:X}"));
        rows.Add(new PackageInfoRow("Entry point", $"0x{executable.EntryPoint:X}"));
        rows.Add(new PackageInfoRow("Program headers", executable.ProgramHeaderCount.ToString("N0")));
        rows.Add(new PackageInfoRow("Section headers", executable.SectionHeaderCount.ToString("N0")));
        if (executable.SelfVersion != 0)
            rows.Add(new PackageInfoRow("SELF version", executable.SelfVersion.ToString()));
        if (executable.SelfProgramType != 0)
            rows.Add(new PackageInfoRow("SELF program type", $"0x{executable.SelfProgramType:X}"));
        if (executable.SelfSegmentCount != 0)
            rows.Add(new PackageInfoRow("SELF segments", executable.SelfSegmentCount.ToString("N0")));
        foreach (Ps5ModuleInfo module in executable.Modules.Take(200))
            rows.Add(new PackageInfoRow($"Module {module.Name}", $"{module.Kind} · {FormatBytes(module.Size)}"));
        return rows;
    }

    private static void Add(List<PackageInfoRow> rows, string label, string value)
    {
        if (!string.IsNullOrWhiteSpace(value)) rows.Add(new PackageInfoRow(label, value));
    }

    private static PackageImage? ToImage(Ps5ImageData? data)
    {
        if (data is null || data.IsEmpty) return null;
        return data.IsRgba
            ? PackageImage.Rgba(data.Bytes, data.Width, data.Height)
            : PackageImage.Png(data.Bytes);
    }

    private static string Normalize(string path) => path.Replace('\\', '/').TrimStart('/');

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = Math.Max(0, bytes);
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return unit == 0 ? $"{value:N0} {units[unit]}" : $"{value:N2} {units[unit]}";
    }
}
