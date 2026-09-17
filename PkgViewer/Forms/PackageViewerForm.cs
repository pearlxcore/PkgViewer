using System.Diagnostics;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using DarkUI.Controls;
using DarkUI.Forms;
using PkgViewer.Core;
using PkgViewer.Core.Backends;
using PkgViewer.Core.Models;
using PkgViewer.Infrastructure;
using PkgViewer.Shell;
using PS5PKGTool.Core.Services;

namespace PkgViewer.Forms;

/// <summary>
/// Full mirror of PS4PKGTool's Mini PKG Viewer layout: header bar, Overview (Package Summary +
/// PARAM.SFO), PKG Internals (Header / Build Info / Entries), Trophy, File Browser and Artwork,
/// with the same menus and status strip. Data comes from the platform-neutral package session so
/// both PS4 and PS5 packages populate the same UI.
/// </summary>
internal sealed partial class PackageViewerForm : DarkForm
{
    private const int MaximumPreviewBytes = 256 * 1024 * 1024;
    private const int MaximumTextBytes = 1024 * 1024;
    private const int MaximumHexBytes = 16 * 1024;
    private const int MaximumFileItems = 20000;

    private readonly string _packagePath;
    private readonly PackageOpenService _openService = new();
    private string _currentPackagePath;

    private IPackageSession? _session;
    private string? _passcode;
    private IReadOnlyList<PackageFileRecord> _allFiles = [];
    private int _fileMatchCount;
    private bool _busy;
    private bool _shown;
    private bool _filesLoaded;
    private bool _trophiesLoaded;
    private bool _detailTabsLoaded;
    private int _shownWarningCount;
    private CancellationTokenSource? _extractCancellation;
    private readonly List<Image> _trophyImages = [];
    private readonly List<DarkTabPage> _detailTabPages = [];

    public PackageViewerForm(string packagePath)
    {
        _packagePath = Path.GetFullPath(packagePath);
        _currentPackagePath = _packagePath;
        AppIcon.Apply(this);

        InitializeComponent();
    }

    private async Task OnTabSelectedAsync()
    {
        if (_session is null) return;
        if (_tabs.SelectedTab == _filesTab) await EnsureFilesAsync();
        else if (_tabs.SelectedTab == _trophyTab) await EnsureTrophiesAsync();
    }


    // ------------------------------------------------------------------
    // Loading
    // ------------------------------------------------------------------

    private async Task LoadPackageAsync()
    {
        SetLoadingState(true, "Reading package metadata...");
        try
        {
            IPackageSession session = await _openService.OpenAsync(
                _currentPackagePath, new PackageOpenOptions { Passcode = _passcode });
            _session?.Dispose();
            _session = session;
            Populate();
            SetLoadingState(false, "Ready");
            await OnTabSelectedAsync();
        }
        catch (OperationCanceledException)
        {
            SetLoadingState(false, "Ready");
        }
        catch (Exception ex)
        {
            Logger.Exception("Open package", ex);
            if (_passcode is null && IsPasscodeFailure(ex) && PromptForPasscode(out string? passcode))
            {
                _passcode = passcode;
                await LoadPackageAsync();
                return;
            }
            SetLoadingState(false, "Unable to read package");
            DarkMessageBox.ShowError(
                "The supplied file could not be read as a PS4/PS5 package.\n\n" + ex.Message, "PkgViewer");
        }
    }

    private void Populate()
    {
        if (_session is null) return;
        PackageInfo info = _session.Info;
        string displayTitle = string.IsNullOrWhiteSpace(info.Title) ? info.FileName : info.Title;

        Text = displayTitle + " - PkgViewer";
        _titleLabel.Text = displayTitle + TitleSuffix(info);
        _subtitleLabel.Text = string.Join("  •  ", new[]
        {
            info.TitleId,
            DescribeCategory(info),
            info.BuildState,
            string.IsNullOrWhiteSpace(info.Version) ? string.Empty : "v" + info.Version,
            SizeText(info)
        }.Where(part => !string.IsNullOrWhiteSpace(part)));
        _contentIdLabel.Text = info.ContentId;
        SetImage(_iconBox, _session.Artwork.Icon);
        PopulateArtwork();

        SetOverviewValue("Title", info.Title);
        SetOverviewValue("Title ID", info.TitleId);
        SetOverviewValue("Content ID", info.ContentId);
        SetOverviewValue("Category", DescribeCategory(info));
        SetOverviewValue("Package state", info.BuildState);
        SetOverviewValue("Application version", info.Version);
        SetOverviewValue("Package version", info.PackageVersion);
        SetOverviewValue("Required firmware", info.RequiredFirmware);
        SetOverviewValue("Package size", SizeText(info));

        _sfoGrid.Rows.Clear();
        foreach (PackageSfoEntry entry in _session.SfoEntries)
            _sfoGrid.Rows.Add(entry.Name, entry.Value);

        PopulateInspectionGrid(_headerGrid, _session.HeaderFields, "Header information");
        PopulateInspectionGrid(_buildGrid, _session.BuildInfoFields, "PUBTOOLINFO");

        _entriesGrid.Rows.Clear();
        foreach (PackageEntryRecord entry in _session.EntryRecords)
            _entriesGrid.Rows.Add(entry.Name, entry.Offset, entry.Size, entry.Flags1, entry.Flags2, entry.Encrypted);

        _allFiles = [];
        _filesLoaded = false;
        _trophiesLoaded = false;
        _fileTree.Nodes.Clear();
        _fileList.Items.Clear();
        ResetTrophyState();
        _shownWarningCount = _session.Warnings.Count;

        ApplyPlatformLayout(info.Platform);
    }

    /// <summary>Logs warnings added by lazy loads and reflects the count in the status bar.</summary>
    private void AppendNewWarnings()
    {
        if (_session is null) return;
        int count = _session.Warnings.Count;
        for (int index = _shownWarningCount; index < count; index++)
            Logger.Info("Warning: " + _session.Warnings[index]);
        if (count > _shownWarningCount)
            _statusState.Text = $"Ready ({count} warning(s)).";
        _shownWarningCount = count;
    }

    /// <summary>
    /// The tab set mirrors the PS4 Mini PKG Viewer for PS4 packages; PS5 packages instead get the
    /// platform-specific extra tabs exposed by the session (param.json, activities, executable...).
    /// </summary>
    private void ApplyPlatformLayout(PkgPlatform platform)
    {
        bool isPs4 = platform == PkgPlatform.Ps4;
        _packageTab.Text = isPs4 ? "PKG Internals" : "Package";

        // PS4 Overview shows the PARAM.SFO grid; PS5 shows the raw param.json instead.
        if (_overviewLayout is not null)
        {
            _overviewLayout.SuspendLayout();
            _overviewLayout.Controls.Remove(_sfoPanel);
            _overviewLayout.Controls.Remove(_paramJsonPanel);
            if (isPs4)
            {
                _overviewLayout.Controls.Add(_sfoPanel, 1, 0);
            }
            else
            {
                _paramJsonTree.BeginUpdate();
                _paramJsonTree.Nodes.Clear();
                if (_session is null || _session.ParameterJsonTree.Count == 0)
                {
                    _paramJsonTree.Nodes.Add(new TreeNode("param.json: Not available"));
                }
                else
                {
                    foreach (JsonTreeNode root in _session.ParameterJsonTree)
                        AddJsonTreeNode(_paramJsonTree.Nodes, root);
                    foreach (TreeNode root in _paramJsonTree.Nodes)
                        root.Expand();
                }
                _paramJsonTree.EndUpdate();
                _overviewLayout.Controls.Add(_paramJsonPanel, 1, 0);
            }
            _overviewLayout.ResumeLayout(true);
        }

        // PS4 artwork shows PIC0/PIC1; PS5 shows icon + PIC0/PIC1/PIC2 with placeholders.
        if (_artworkLayout is not null)
        {
            _artworkTab.Controls.Remove(_artworkLayout);
            _artworkLayout.Controls.Clear();
            _artworkLayout.Dispose();
            _artworkTab.Controls.Add(BuildArtworkLayout(isPs4));
        }

        RebuildDetailTabs();
    }

    private void RebuildDetailTabs()
    {
        foreach (DarkTabPage page in _detailTabPages)
        {
            _tabs.TabPages.Remove(page);
            page.Dispose();
        }
        _detailTabPages.Clear();
        _detailTabsLoaded = false;
        if (_session is null || _session.Info.Platform != PkgPlatform.Ps5) return;
        _ = LoadDetailTabsAsync();
    }

    private async Task LoadDetailTabsAsync()
    {
        if (_session is null || _detailTabsLoaded) return;
        _detailTabsLoaded = true;
        try
        {
            IReadOnlyList<PackageDetailTab> tabs = await _session.GetDetailTabsAsync(CancellationToken.None);
            if (_session is null || IsDisposed) return;
            Logger.Info("Detail tabs: " + (tabs.Count == 0 ? "(none)" : string.Join(", ", tabs.Select(tab => tab.Title))));
            ShowDetailTabs(tabs);
            AppendNewWarnings();
        }
        catch (OperationCanceledException)
        {
            _detailTabsLoaded = false;
        }
        catch (Exception ex)
        {
            Logger.Exception("Load detail tabs", ex);
            _detailTabsLoaded = false;
        }
    }

    private void ShowDetailTabs(IReadOnlyList<PackageDetailTab> tabs)
    {
        int index = _tabs.TabPages.IndexOf(_packageTab) + 1;
        foreach (PackageDetailTab tab in tabs)
        {
            var page = new DarkTabPage { Text = tab.Title, Padding = new Padding(12) };
            if (tab.Text is not null)
            {
                page.Controls.Add(new DarkTextBox
                {
                    Dock = DockStyle.Fill,
                    Multiline = true,
                    ReadOnly = true,
                    ScrollBars = ScrollBars.Both,
                    WordWrap = false,
                    Font = new Font("Consolas", 9F),
                    Text = tab.Text
                });
            }
            else
            {
                var grid = new DarkDataGridView();
                SetupGrid(grid);
                grid.Columns.Add(TextColumn("Field", 38F));
                grid.Columns.Add(TextColumn("Value", 62F));
                if (tab.Rows.Count == 0)
                    grid.Rows.Add(tab.Title, "Not available");
                else
                    foreach (PackageInfoRow row in tab.Rows)
                        grid.Rows.Add(row.Label, row.Value);
                page.Controls.Add(grid);
            }
            _tabs.TabPages.Insert(index++, page);
            _detailTabPages.Add(page);
        }
    }

    // ------------------------------------------------------------------
    // Lazy tab loading (PS5 file listing/trophies need the inner-image decode)
    // ------------------------------------------------------------------

    private async Task EnsureFilesAsync()
    {
        if (_session is null || _filesLoaded) return;
        _filesLoaded = true;
        _statusState.Text = "Loading package file list...";
        try
        {
            _allFiles = await _session.GetFilesAsync(CancellationToken.None);
            PopulateFileTree();
            _statusState.Text = "Ready";
        }
        catch (OperationCanceledException)
        {
            _filesLoaded = false;
        }
        catch (Exception ex)
        {
            Logger.Exception("Load file list", ex);
            _filesLoaded = false;
            _statusState.Text = "Ready (Files unavailable)";
        }
        AppendNewWarnings();
    }

    private async Task EnsureTrophiesAsync()
    {
        if (_session is null || _trophiesLoaded) return;
        _trophiesLoaded = true;
        _trophyState.Text = "Loading trophy information...";
        _statusState.Text = "Loading trophy information...";
        try
        {
            IReadOnlyList<PackageTrophy> trophies = await _session.GetTrophiesAsync(CancellationToken.None);
            PopulateTrophies(trophies);
            _statusState.Text = "Ready";
        }
        catch (OperationCanceledException)
        {
            _trophiesLoaded = false;
        }
        catch (Exception ex)
        {
            Logger.Exception("Load trophies", ex);
            _trophiesLoaded = false;
            _trophyState.Text = "Trophy information could not be read. " + ex.Message;
            _statusState.Text = "Ready (Trophy unavailable)";
        }
        AppendNewWarnings();
    }

    private void PopulateArtwork()
    {
        PackageArtwork artwork = _session?.Artwork ?? new PackageArtwork();
        SetArtworkSlot(_iconArtBox, _iconEmpty, artwork.Icon);
        SetArtworkSlot(_pic0Box, _pic0Empty, artwork.Pic0);
        SetArtworkSlot(_pic1Box, _pic1Empty, artwork.Pic1);
        SetArtworkSlot(_pic2Box, _pic2Empty, artwork.Pic2);
    }

    private static void SetArtworkSlot(PictureBox box, DarkLabel empty, PackageImage? image)
    {
        SetImage(box, image);
        bool hasImage = box.Image is not null;
        box.Visible = hasImage;
        empty.Visible = !hasImage;
    }

    private void SetOverviewValue(string key, string value) =>
        _overviewValues[key].Text = string.IsNullOrWhiteSpace(value) ? "Not available" : value;

    private static void PopulateInspectionGrid(DarkDataGridView grid, IReadOnlyList<PackageInfoRow> rows, string emptyLabel)
    {
        grid.Rows.Clear();
        if (rows.Count == 0)
        {
            grid.Rows.Add(emptyLabel, "Not available");
            return;
        }
        foreach (PackageInfoRow row in rows)
            grid.Rows.Add(row.Label, row.Value);
    }

    private void ResetTrophyState()
    {
        foreach (Image image in _trophyImages) image.Dispose();
        _trophyImages.Clear();
        _trophyGrid.Rows.Clear();
        _trophyState.Text = "Trophy information loads when this page is selected.";
    }

    private void PopulateTrophies(IReadOnlyList<PackageTrophy> trophies)
    {
        foreach (Image image in _trophyImages) image.Dispose();
        _trophyImages.Clear();
        _trophyGrid.Rows.Clear();
        if (_session is null) return;

        if (trophies.Count == 0)
        {
            _trophyState.Text = string.IsNullOrWhiteSpace(_session.TrophyMessage)
                ? "No trophy data was found in this package."
                : _session.TrophyMessage;
            return;
        }

        foreach (PackageTrophy trophy in trophies)
        {
            Image? icon = ToBitmap(trophy.Icon);
            if (icon is not null) _trophyImages.Add(icon);
            _trophyGrid.Rows.Add(new object?[]
            {
                icon, trophy.Id.ToString("000"), trophy.Name, trophy.Description, trophy.Grade,
                trophy.Hidden ? "Yes" : "No"
            });
        }
        _trophyState.Text = BuildTrophySummary(trophies);
    }

    private static string BuildTrophySummary(IReadOnlyList<PackageTrophy> trophies)
    {
        string counts = string.Join("  •  ", trophies
            .GroupBy(trophy => trophy.Grade)
            .OrderBy(group => TrophyGradeOrder(group.Key))
            .Select(group => $"{group.Count():N0} {group.Key}"));
        string summary = $"{trophies.Count:N0} trophies";
        if (!string.IsNullOrWhiteSpace(counts)) summary += "  •  " + counts;
        return summary;
    }

    private static int TrophyGradeOrder(string grade) => grade switch
    {
        "Bronze" => 0,
        "Silver" => 1,
        "Gold" => 2,
        "Platinum" => 3,
        _ => 4
    };

    // ------------------------------------------------------------------
    // File browser
    // ------------------------------------------------------------------

    private void PopulateFileTree()
    {
        _fileTree.BeginUpdate();
        try
        {
            _fileTree.Nodes.Clear();
            foreach (PackageFileNode root in PackageFileTree.Build(_allFiles))
                AddTreeNode(_fileTree.Nodes, root);
        }
        finally
        {
            _fileTree.EndUpdate();
        }

        if (_fileTree.Nodes.Count > 0)
            _fileTree.SelectedNode = _fileTree.Nodes[0];
        else
            RefreshFileList();
    }

    private static void AddTreeNode(TreeNodeCollection collection, PackageFileNode model)
    {
        var node = new TreeNode(model.Name) { Tag = model };
        int icon = model.IsDirectory ? FileIcons.Folder : FileIcons.IconFor(model.Name);
        node.ImageIndex = icon;
        node.SelectedImageIndex = icon;
        collection.Add(node);
        foreach (PackageFileNode child in model.Children)
            AddTreeNode(node.Nodes, child);
    }

    private static void AddJsonTreeNode(TreeNodeCollection collection, JsonTreeNode model)
    {
        string text = model.Value is null ? model.Name : $"{model.Name}: {model.Value}";
        var node = new TreeNode(text);
        collection.Add(node);
        foreach (JsonTreeNode child in model.Children)
            AddJsonTreeNode(node.Nodes, child);
    }


    private void OnFileListMouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right) return;
        if (_fileList.SelectedItems.Count == 0) return;
        if (_fileList.SelectedItems[0].Text == "...") return;
        _fileContextMenu.Show(_fileList, e.Location);
    }

    private void CopySelectedPath()
    {
        if (_fileList.SelectedItems.Count > 0 &&
            _fileList.SelectedItems[0].Tag is TreeNode { Tag: PackageFileNode model })
            Clipboard.SetText(model.FullPath);
    }

    private void CopySelectedName()
    {
        if (_fileList.SelectedItems.Count > 0)
            Clipboard.SetText(_fileList.SelectedItems[0].Text);
    }

    private void RefreshFileList()
    {
        if (_session is null) return;
        string query = _fileFilter.SearchText.Trim();
        bool filtering = query.Length > 0;
        int count;

        if (filtering)
        {
            _fileMatchCount = 0;
            _fileList.BeginUpdate();
            try
            {
                _fileList.Items.Clear();
                CollectFileMatches(_fileTree.Nodes, query);
            }
            finally { _fileList.EndUpdate(); }
            count = _fileMatchCount;
        }
        else if (_fileTree.SelectedNode is { } selected)
        {
            PopulateFileList(selected);
            count = _fileList.Items.Count;
        }
        else
        {
            _fileList.Items.Clear();
            count = 0;
        }

        if (!_busy)
            _statusState.Text = filtering ? $"{count:N0} match(es)." : $"{count:N0} item(s).";
    }

    private void PopulateFileList(TreeNode selected)
    {
        if (selected.Tag is not PackageFileNode) return;

        _fileList.BeginUpdate();
        try
        {
            _fileList.Items.Clear();
            if (selected.Parent is not null)
                _fileList.Items.Add(new ListViewItem(["...", string.Empty, string.Empty, string.Empty])
                {
                    Tag = selected.Parent,
                    ImageIndex = FileIcons.FolderOpen
                });

            foreach (TreeNode child in selected.Nodes)
            {
                if (child.Tag is not PackageFileNode model) continue;
                _fileList.Items.Add(BuildFileListItem(child, model));
            }
        }
        finally
        {
            _fileList.EndUpdate();
        }
    }

    private void CollectFileMatches(TreeNodeCollection nodes, string query)
    {
        foreach (TreeNode node in nodes)
        {
            if (node.Tag is PackageFileNode model)
            {
                bool matches = model.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || model.FullPath.Contains(query, StringComparison.OrdinalIgnoreCase);
                if (matches)
                {
                    if (_fileMatchCount >= MaximumFileItems)
                    {
                        _fileList.Items.Add(new ListViewItem([
                            "...", $"More than {MaximumFileItems:N0} matches - refine the filter", string.Empty, string.Empty
                        ]));
                        return;
                    }
                    _fileList.Items.Add(BuildFileListItem(node, model));
                    _fileMatchCount++;
                    ExpandFileAncestors(node);
                }
            }

            if (node.Nodes.Count > 0)
                CollectFileMatches(node.Nodes, query);
        }
    }

    private static ListViewItem BuildFileListItem(TreeNode node, PackageFileNode model)
    {
        int icon = model.IsDirectory ? FileIcons.Folder : FileIcons.IconFor(model.Name);
        return new ListViewItem([
            model.Name,
            FileTypeLabel(model),
            model.IsDirectory ? string.Empty : model.FullPath,
            model.IsDirectory ? string.Empty : FormatByteSize(model.Size)
        ])
        { Tag = node, ImageIndex = icon };
    }

    private static void ExpandFileAncestors(TreeNode node)
    {
        TreeNode? parent = node.Parent;
        while (parent is not null)
        {
            parent.Expand();
            parent = parent.Parent;
        }
    }

    private void ActivateFileListItem()
    {
        if (_fileList.SelectedItems.Count == 0) return;
        if (_fileList.SelectedItems[0].Tag is not TreeNode node) return;
        if (node.Tag is not PackageFileNode model) return;

        if (model.IsDirectory)
        {
            _fileTree.SelectedNode = node;
            node.EnsureVisible();
            return;
        }

        _ = PreviewFileAsync(model.FullPath, model.Size);
    }

    private static string FileTypeLabel(PackageFileNode model)
    {
        if (model.IsDirectory) return "Directory";
        string extension = Path.GetExtension(model.Name);
        return string.IsNullOrEmpty(extension) ? "File" : extension.TrimStart('.');
    }

    // ------------------------------------------------------------------
    // Preview
    // ------------------------------------------------------------------

    private async Task PreviewFileAsync(string path, long size)
    {
        if (_busy || _session is null) return;

        _previewInfo.Text = "Previewing " + Path.GetFileName(path) + "...";
        _previewText.Text = "Loading preview...";
        _previewText.Visible = true;
        _previewImage.Visible = false;
        _previewImage.Image?.Dispose();
        _previewImage.Image = null;

        try
        {
            PreviewResult result = await Task.Run(() => BuildPreview(path, size));
            if (result.Image is not null)
            {
                _previewImage.Image = result.Image;
                _previewImage.Visible = true;
                _previewText.Visible = false;
                _previewText.Text = string.Empty;
                _previewInfo.Text = path;
            }
            else
            {
                _previewImage.Visible = false;
                _previewText.Visible = true;
                _previewText.Text = result.Text ?? result.Message ?? string.Empty;
                _previewInfo.Text = result.Image is null && result.Text is not null
                    ? $"{Path.GetFileName(path)} ({FormatByteSize(size)}) - preview"
                    : path;
            }
        }
        catch (Exception ex)
        {
            _previewImage.Visible = false;
            _previewText.Visible = true;
            _previewText.Text = "Preview failed: " + ex.Message;
            _previewInfo.Text = path;
        }
    }

    private PreviewResult BuildPreview(string path, long size)
    {
        if (_session is null) return new PreviewResult(null, null, "No package is open.");
        if (size > MaximumPreviewBytes)
            return new PreviewResult(null, null, $"The file is too large to preview ({FormatByteSize(size)}). Use Extract.");

        string extension = Path.GetExtension(path).ToLowerInvariant();
        using Stream stream = _session.OpenFile(path);

        if (extension is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif")
        {
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            memory.Position = 0;
            using Image source = Image.FromStream(memory, useEmbeddedColorManagement: false, validateImageData: true);
            return new PreviewResult(new Bitmap(source), null, null);
        }

        if (extension == ".dds")
        {
            (byte[] rgba, int width, int height) = Ps5ImageCodec.DecodeDdsToRgba(stream);
            return new PreviewResult(BitmapFromRgba(rgba, width, height), null, null);
        }

        byte[] buffer = ReadAtMost(stream, MaximumTextBytes);
        if (!ContainsNullByte(buffer))
        {
            string text = Encoding.UTF8.GetString(buffer);
            if (size > buffer.Length)
                text += Environment.NewLine + $"... truncated ({FormatByteSize(size)} total)";
            return new PreviewResult(null, text, null);
        }

        int hexLength = Math.Min(buffer.Length, MaximumHexBytes);
        return new PreviewResult(null, BuildHexDump(buffer[..hexLength]), null);
    }

    // ------------------------------------------------------------------
    // Extraction
    // ------------------------------------------------------------------

    private async Task ExtractSelectedAsync(bool preserveStructure)
    {
        if (_busy || _session is null) return;
        if (_fileList.SelectedItems.Count == 0 ||
            _fileList.SelectedItems[0].Tag is not TreeNode node ||
            node.Tag is not PackageFileNode model)
        {
            DarkMessageBox.ShowWarning("Select a file or folder to extract.", "PkgViewer");
            return;
        }

        using var dialog = new FolderBrowserDialog { Description = "Select the extraction folder" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        BeginExtractionUi();
        _statusState.Text = "Extracting selected data...";
        try
        {
            _extractCancellation = new CancellationTokenSource();
            if (model.IsDirectory)
            {
                string[] children = CollectFilePaths(node);
                int done = 0;
                foreach (string child in children)
                {
                    _extractCancellation.Token.ThrowIfCancellationRequested();
                    _statusState.Text = $"Extracting {done + 1}/{children.Length}: {child}";
                    string relative = preserveStructure
                        ? child
                        : child.StartsWith(model.FullPath + "/", StringComparison.OrdinalIgnoreCase)
                            ? child[(model.FullPath.Length + 1)..]
                            : child;
                    string destination = Path.Combine(dialog.SelectedPath,
                        relative.Replace('/', Path.DirectorySeparatorChar));
                    await _session.ExtractFileAsync(child, destination, null, _extractCancellation.Token);
                    done++;
                }
            }
            else
            {
                string relative = preserveStructure ? model.FullPath : Path.GetFileName(model.FullPath);
                string destination = Path.Combine(dialog.SelectedPath,
                    relative.Replace('/', Path.DirectorySeparatorChar));
                await _session.ExtractFileAsync(model.FullPath, destination, null, _extractCancellation.Token);
            }
            DarkMessageBox.ShowInformation("Selected data extracted.", "PkgViewer");
        }
        catch (OperationCanceledException)
        {
            DarkMessageBox.ShowInformation("Extraction cancelled.", "PkgViewer");
        }
        catch (Exception ex)
        {
            Logger.Exception("Extract selected", ex);
            DarkMessageBox.ShowError(ex.Message, "PkgViewer");
        }
        finally
        {
            EndExtractionUi();
        }
    }

    private async Task ExtractFullAsync()
    {
        if (_busy || _session is null) return;
        using var dialog = new FolderBrowserDialog { Description = "Select a folder to extract the package into" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        string title = string.IsNullOrWhiteSpace(_session.Info.Title)
            ? Path.GetFileNameWithoutExtension(_currentPackagePath)
            : _session.Info.Title;
        string target = Path.Combine(dialog.SelectedPath, PackageFileName.Sanitize(title));

        BeginExtractionUi();
        _statusState.Text = "Preparing extraction...";
        try
        {
            _extractCancellation = new CancellationTokenSource();
            var progress = new Progress<PackageExtractProgress>(report =>
            {
                _progressBar.Maximum = Math.Max(1, report.FileCount);
                _progressBar.Value = Math.Min(report.FilesDone, _progressBar.Maximum);
                _statusState.Text = $"Extracting {report.FilesDone}/{report.FileCount}: {report.CurrentFile}";
            });
            await _session.ExtractAllAsync(target, progress, _extractCancellation.Token);
            DarkMessageBox.ShowInformation($"PKG extracted to:\n{target}", "PkgViewer");
        }
        catch (OperationCanceledException)
        {
            DarkMessageBox.ShowInformation("Extraction cancelled.", "PkgViewer");
        }
        catch (Exception ex)
        {
            Logger.Exception("Extract package", ex);
            DarkMessageBox.ShowError(ex.Message, "PkgViewer");
        }
        finally
        {
            EndExtractionUi();
        }
    }

    private static string[] CollectFilePaths(TreeNode node)
    {
        var paths = new List<string>();
        void Walk(TreeNode current)
        {
            foreach (TreeNode child in current.Nodes)
            {
                if (child.Tag is PackageFileNode model && !model.IsDirectory) paths.Add(model.FullPath);
                Walk(child);
            }
        }
        Walk(node);
        return [.. paths];
    }

    private void BeginExtractionUi()
    {
        _busy = true;
        _progressBar.Visible = true;
        _progressBar.Style = ProgressBarStyle.Continuous;
        _progressBar.Minimum = 0;
        _progressBar.Value = 0;
        _progressBar.Maximum = 1;
        _stopExtractButton.Visible = true;
        _stopExtractButton.Enabled = true;
        _tabs.Enabled = false;
    }

    private void EndExtractionUi()
    {
        _extractCancellation?.Dispose();
        _extractCancellation = null;
        _stopExtractButton.Visible = false;
        _progressBar.Visible = false;
        _tabs.Enabled = true;
        _busy = false;
        _statusState.Text = "Ready";
    }

    private void StopExtraction()
    {
        _extractCancellation?.Cancel();
        _stopExtractButton.Enabled = false;
        _statusState.Text = "Stopping extraction...";
    }

    // ------------------------------------------------------------------
    // Menu actions
    // ------------------------------------------------------------------

    private void CopyInfo(string kind)
    {
        if (_session is null) return;
        string value = kind switch
        {
            "title_id" => _session.Info.TitleId,
            "content_id" => _session.Info.ContentId,
            "title" => _session.Info.Title,
            "filename" => Path.GetFileName(_currentPackagePath),
            _ => string.Empty
        };
        string what = kind switch
        {
            "title_id" => "Title ID",
            "content_id" => "Content ID",
            "title" => "Title",
            _ => "Filename"
        };
        if (string.IsNullOrWhiteSpace(value))
        {
            DarkMessageBox.ShowWarning("Package information is not available yet.", "PkgViewer");
            return;
        }
        Clipboard.SetText(value);
        DarkMessageBox.ShowInformation(what + " copied to clipboard.", "PkgViewer");
    }

    /// <summary>Title suffix combining the platform and container format, e.g. " (PS4 · PKG)".</summary>
    private static string TitleSuffix(PackageInfo info)
    {
        string platform = info.Platform switch
        {
            PkgPlatform.Ps4 => "PS4",
            PkgPlatform.Ps5 => "PS5",
            _ => string.Empty
        };
        string format = info.FormatShort;

        if (platform.Length == 0 && format.Length == 0) return string.Empty;
        if (platform.Length == 0) return $" ({format})";
        if (format.Length == 0) return $" ({platform})";
        return $" ({platform} · {format})";
    }

    private void SaveArtwork()
    {
        if (_session is null) return;
        using var dialog = new FolderBrowserDialog { Description = "Select a folder to save artwork into" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        string name = Path.GetFileNameWithoutExtension(_currentPackagePath);
        var failures = new List<string>();
        SaveImage(_session.Artwork.Icon, dialog.SelectedPath, name + "_ICON.PNG", failures);
        SaveImage(_session.Artwork.Pic0, dialog.SelectedPath, name + "_PIC0.PNG", failures);
        SaveImage(_session.Artwork.Pic1, dialog.SelectedPath, name + "_PIC1.PNG", failures);

        if (failures.Count > 0)
            DarkMessageBox.ShowWarning("Artwork could not be saved: " + string.Join(" ", failures), "PkgViewer");
        else
            DarkMessageBox.ShowInformation("Artwork saved.", "PkgViewer");
    }

    private static void SaveImage(PackageImage? image, string folder, string fileName, List<string> failures)
    {
        if (image is null || image.IsEmpty) return;
        try
        {
            using Bitmap? bitmap = ToBitmap(image);
            if (bitmap is null) return;
            bitmap.Save(Path.Combine(folder, fileName), ImageFormat.Png);
        }
        catch (Exception ex) when (ex is IOException or ExternalException or ArgumentException)
        {
            failures.Add(fileName);
        }
    }

    private void AddIntegration()
    {
        try
        {
            PackageShellIntegration.Install();
            DarkMessageBox.ShowInformation(
                "PkgViewer integration added for .pkg, .ffpfsc, .ffpkg and .exfat.\n\n" +
                "Use Windows Default apps to choose PkgViewer.",
                "PkgViewer");
        }
        catch (Exception ex)
        {
            Logger.Exception("Add integration", ex);
            DarkMessageBox.ShowError(ex.Message, "PkgViewer");
        }
    }

    private void RemoveIntegration()
    {
        try
        {
            PackageShellIntegration.Uninstall();
            DarkMessageBox.ShowInformation(
                "PkgViewer integration removed and the package icon was reset.",
                "PkgViewer");
        }
        catch (Exception ex)
        {
            Logger.Exception("Remove integration", ex);
            DarkMessageBox.ShowError(ex.Message, "PkgViewer");
        }
    }

    private void RemoveLegacyIntegration()
    {
        try
        {
            PackageShellIntegration.RemoveLegacyAssociations();
            DarkMessageBox.ShowInformation(
                "Legacy PS4/PS5 PKG Viewer associations were removed and the .pkg icon was reset.",
                "PkgViewer");
        }
        catch (Exception ex)
        {
            Logger.Exception("Remove legacy integration", ex);
            DarkMessageBox.ShowError(ex.Message, "PkgViewer");
        }
    }

    private static void OpenCoffeeLink()
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://ko-fi.com/pearlxcore") { UseShellExecute = true });
        }
        catch (Exception)
        {
            // Opening the browser is best-effort.
        }
    }

    private bool PromptForPasscode(out string? passcode)
    {
        using var prompt = new PasscodePromptForm();
        if (prompt.ShowDialog(this) != DialogResult.OK)
        {
            passcode = null;
            return false;
        }
        if (prompt.IsNoPasscode)
        {
            passcode = "00000000000000000000000000000000";
            return true;
        }
        if (string.IsNullOrWhiteSpace(prompt.Passcode))
        {
            passcode = null;
            return false;
        }
        passcode = prompt.Passcode;
        return true;
    }

    private static bool IsPasscodeFailure(Exception exception) =>
        exception.Message.Contains("passcode", StringComparison.OrdinalIgnoreCase);

    // ------------------------------------------------------------------
    // State / lifecycle
    // ------------------------------------------------------------------

    private void SetLoadingState(bool loading, string state)
    {
        _busy = loading;
        _progressBar.Visible = loading;
        _progressBar.Style = loading ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
        _progressBar.MarqueeAnimationSpeed = loading ? 30 : 0;
        _tabs.Enabled = !loading;
        _statusPath.Text = _currentPackagePath;
        _statusState.Text = state;
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_busy && _extractCancellation is not null)
        {
            e.Cancel = true;
            DarkMessageBox.ShowInformation("Extraction in progress. Stop it before closing.", "PkgViewer");
            return;
        }
        ReleaseResources();
    }

    private void ReleaseResources()
    {
        _session?.Dispose();
        _session = null;
        foreach (Image image in _trophyImages) image.Dispose();
        _trophyImages.Clear();
        foreach (PictureBox box in new[] { _iconBox, _iconArtBox, _pic0Box, _pic1Box, _pic2Box, _previewImage })
        {
            Image? image = box.Image;
            box.Image = null;
            image?.Dispose();
        }
    }

    // ------------------------------------------------------------------
    // Small helpers
    // ------------------------------------------------------------------

    private static void SetImage(PictureBox box, PackageImage? image)
    {
        Image? previous = box.Image;
        box.Image = ToBitmap(image);
        previous?.Dispose();
    }

    private static Bitmap? ToBitmap(PackageImage? image)
    {
        if (image is null || image.IsEmpty) return null;
        try
        {
            if (image.IsRgba)
            {
                if (image.Width <= 0 || image.Height <= 0) return null;
                long expected = (long)image.Width * image.Height * 4;
                if (image.Bytes.Length < expected) return null;
                return BitmapFromRgba(image.Bytes, image.Width, image.Height);
            }
            using var stream = new MemoryStream(image.Bytes, writable: false);
            using Image source = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: true);
            return new Bitmap(source);
        }
        catch (Exception ex) when (ex is ArgumentException or ExternalException or OutOfMemoryException)
        {
            // A single unreadable texture must never blank the whole viewer.
            return null;
        }
    }

    // GDI+ 32bppArgb expects BGRA byte order, so decoded RGBA rows are swapped while copying.
    private static Bitmap BitmapFromRgba(byte[] rgba, int width, int height)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        BitmapData bits = bitmap.LockBits(new Rectangle(0, 0, width, height),
            ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            int rowBytes = width * 4;
            byte[] row = new byte[rowBytes];
            for (int y = 0; y < height; y++)
            {
                int source = y * rowBytes;
                for (int x = 0; x < rowBytes; x += 4)
                {
                    row[x] = rgba[source + x + 2];
                    row[x + 1] = rgba[source + x + 1];
                    row[x + 2] = rgba[source + x];
                    row[x + 3] = rgba[source + x + 3];
                }
                Marshal.Copy(row, 0, bits.Scan0 + y * bits.Stride, rowBytes);
            }
        }
        finally
        {
            bitmap.UnlockBits(bits);
        }
        return bitmap;
    }

    private static byte[] ReadAtMost(Stream stream, int maximumBytes)
    {
        byte[] buffer = new byte[maximumBytes];
        int total = 0;
        while (total < buffer.Length)
        {
            int read = stream.Read(buffer, total, buffer.Length - total);
            if (read == 0) break;
            total += read;
        }
        return total == buffer.Length ? buffer : buffer[..total];
    }

    private static bool ContainsNullByte(byte[] buffer)
    {
        int limit = Math.Min(buffer.Length, 4096);
        for (int i = 0; i < limit; i++)
            if (buffer[i] == 0) return true;
        return false;
    }

    private static string BuildHexDump(byte[] buffer)
    {
        if (buffer.Length == 0) return "(empty file)";
        var builder = new StringBuilder();
        for (int offset = 0; offset < buffer.Length; offset += 16)
        {
            builder.Append(offset.ToString("X8")).Append("  ");
            int lineEnd = Math.Min(offset + 16, buffer.Length);
            for (int i = offset; i < offset + 16; i++)
            {
                builder.Append(i < lineEnd ? buffer[i].ToString("X2") : "  ");
                builder.Append(' ');
            }
            builder.Append(' ');
            for (int i = offset; i < lineEnd; i++)
            {
                char c = (char)buffer[i];
                builder.Append(c is >= ' ' and <= '~' ? c : '.');
            }
            builder.AppendLine();
        }
        return builder.ToString();
    }

    private static string DescribeCategory(PackageInfo info) => PackageCategory.Describe(info.Category);

    private static string SizeText(PackageInfo info) =>
        info.FileSize > 0 ? FormatByteSize(info.FileSize) : string.Empty;

    private static string FormatByteSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes:N0} bytes";
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

    private sealed record PreviewResult(Image? Image, string? Text, string? Message);
}
