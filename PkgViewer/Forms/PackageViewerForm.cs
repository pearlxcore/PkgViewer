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

    private readonly AppSettings _settings = AppSettings.Load();
    private readonly PackageOpenService _openService = new();
    private string _currentPackagePath;

    private IPackageSession? _session;
    private string? _passcode;
    private IReadOnlyList<PackageFileRecord> _allFiles = [];
    private int _fileMatchCount;
    private bool _fileLimitHit;
    private bool _busy;
    private bool _shown;
    private bool _filesLoaded;
    private bool _trophiesLoaded;
    private bool _detailTabsLoaded;
    private int _shownWarningCount;
    private CancellationTokenSource? _extractCancellation;
    private CancellationTokenSource? _previewCancellation;
    private CancellationTokenSource _lifetimeCancellation = new();
    private int _previewVersion;
    private bool _closing;
    private TreeNode? _fileRootNode;
    private string _lastExtractionDirectory;
    private readonly List<Image> _trophyImages = [];
    private readonly List<DarkTabPage> _detailTabPages = [];

    public PackageViewerForm(string packagePath)
    {
        _currentPackagePath = Path.GetFullPath(packagePath);
        _lastExtractionDirectory = _settings.LastExtractionDirectory;
        AppIcon.Apply(this);
        ApplyWindowBounds();

        InitializeComponent();

        if (_previewPaneMenuItem is not null) _previewPaneMenuItem.Checked = _settings.PreviewPaneVisible;
        SetPreviewPaneVisible(_settings.PreviewPaneVisible);
        RestoreFileLayout();
    }

    private void ApplyWindowBounds()
    {
        if (_settings.WindowWidth >= 800 && _settings.WindowHeight >= 560)
            ClientSize = new Size(_settings.WindowWidth, _settings.WindowHeight);
        if (_settings.WindowMaximized) WindowState = FormWindowState.Maximized;
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
        _fileRootNode = null;
        _fileBreadcrumb.Text = "Package root";
        ResetTrophyState();

        // Surface warnings that already exist at open (protected/unreadable content) instead of only
        // counting them; otherwise such a package looks like an empty file list.
        _shownWarningCount = 0;
        AppendNewWarnings();
        UpdateMenuStates();

        ApplyPlatformLayout(info.Platform);
    }

    /// <summary>Logs warnings added by lazy loads and reflects the count in the status bar.</summary>
    private void AppendNewWarnings()
    {
        if (_session is null) return;
        int count = _session.Warnings.Count;
        for (int index = _shownWarningCount; index < count; index++)
            Logger.Info("Warning: " + _session.Warnings[index]);
        _shownWarningCount = count;
        UpdateWarningIndicator();
    }

    private void UpdateWarningIndicator()
    {
        int count = _session?.Warnings.Count ?? 0;
        if (count == 0)
        {
            _statusWarnings.Visible = false;
            _statusWarnings.Text = string.Empty;
            return;
        }
        _statusWarnings.Visible = true;
        _statusWarnings.Text = $"⚠ {count} warning(s)";
        _statusWarnings.ToolTipText = "Click to view the warning details.";
    }

    private void ShowWarnings()
    {
        IReadOnlyList<string> warnings = _session?.Warnings ?? [];
        using var dialog = new WarningsForm(warnings);
        dialog.ShowDialog(this);
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
            IReadOnlyList<PackageDetailTab> tabs = await _session.GetDetailTabsAsync(_lifetimeCancellation.Token);
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
            _allFiles = await _session.GetFilesAsync(_lifetimeCancellation.Token);
            if (IsDisposed) return;
            PopulateFileTree();
            _statusState.Text = "Ready";
            UpdateMenuStates();
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
            IReadOnlyList<PackageTrophy> trophies = await _session.GetTrophiesAsync(_lifetimeCancellation.Token);
            if (IsDisposed) return;
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
            // A synthetic root presents top-level files and folders together.
            var root = new TreeNode("Package root") { Tag = null, ImageIndex = FileIcons.Folder, SelectedImageIndex = FileIcons.FolderOpen };
            foreach (PackageFileNode model in PackageFileTree.Build(_allFiles))
                AddTreeNode(root.Nodes, model);
            root.Expand();
            _fileRootNode = root;
            _fileTree.Nodes.Add(root);
        }
        finally
        {
            _fileTree.EndUpdate();
        }

        if (_fileRootNode is not null)
            _fileTree.SelectedNode = _fileRootNode;
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
            _fileLimitHit = false;
            _fileList.BeginUpdate();
            try
            {
                _fileList.Items.Clear();
                CollectFileMatches(_fileRootNode?.Nodes ?? _fileTree.Nodes, query);
            }
            finally { _fileList.EndUpdate(); }
            count = _fileMatchCount;
        }
        else
        {
            TreeNode? folder = CurrentFolderNode();
            if (folder is not null)
            {
                PopulateFileList(folder);
                count = _fileList.Items.Count;
            }
            else
            {
                _fileList.Items.Clear();
                count = 0;
            }
        }

        UpdateBreadcrumb();
        UpdateFileActionState();
        if (!_busy)
            _statusState.Text = filtering
                ? (_fileLimitHit ? $"First {count:N0} matches shown - refine the filter." : $"{count:N0} match(es).")
                : $"{count:N0} item(s).";
    }

    /// <summary>The folder whose contents the list shows: the selected folder, or a file's parent.</summary>
    private TreeNode? CurrentFolderNode()
    {
        TreeNode? node = _fileTree.SelectedNode;
        if (node is null) return _fileRootNode;
        if (node == _fileRootNode) return node;
        if (node.Tag is PackageFileNode { IsDirectory: true }) return node;
        return node.Parent;
    }

    private void OnFileTreeNodeSelected() => RefreshFileList();

    private void PopulateFileList(TreeNode folder)
    {
        _fileList.BeginUpdate();
        try
        {
            _fileList.Items.Clear();
            TreeNode? parent = folder == _fileRootNode ? null : folder.Parent;
            if (parent is not null)
                _fileList.Items.Add(new ListViewItem(["...", "Up", string.Empty, string.Empty])
                {
                    Tag = parent,
                    ImageIndex = FileIcons.FolderOpen
                });

            foreach (TreeNode child in folder.Nodes)
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
            if (_fileLimitHit) return;
            if (node.Tag is PackageFileNode model)
            {
                bool matches = model.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || model.FullPath.Contains(query, StringComparison.OrdinalIgnoreCase);
                if (matches)
                {
                    if (_fileMatchCount >= MaximumFileItems)
                    {
                        _fileLimitHit = true;
                        return;
                    }
                    _fileList.Items.Add(BuildFileListItem(node, model));
                    _fileMatchCount++;
                }
            }

            if (node.Nodes.Count > 0)
                CollectFileMatches(node.Nodes, query);
        }
    }

    private void NavigateUp()
    {
        TreeNode? folder = CurrentFolderNode();
        TreeNode? parent = folder?.Parent;
        if (parent is null) return;
        if (!string.IsNullOrEmpty(_fileFilter.SearchText))
            _fileFilter.SearchText = string.Empty; // also refreshes
        _fileTree.SelectedNode = parent;
        parent.EnsureVisible();
    }

    private void UpdateBreadcrumb()
    {
        TreeNode? folder = CurrentFolderNode();
        if (folder is null || folder == _fileRootNode)
        {
            _fileBreadcrumb.Text = "Package root";
            return;
        }
        var parts = new List<string>();
        TreeNode? current = folder;
        while (current is not null && current != _fileRootNode)
        {
            if (current.Tag is PackageFileNode model) parts.Insert(0, model.Name);
            current = current.Parent;
        }
        _fileBreadcrumb.Text = parts.Count == 0 ? "Package root" : "Package root / " + string.Join(" / ", parts);
    }

    private void UpdateFileActionState()
    {
        bool hasFiles = _session is not null && _filesLoaded;
        _extractAllButton.Enabled = hasFiles && !_busy;
        _extractSelectedButton.Enabled = hasFiles && !_busy && _fileList.SelectedItems.Count > 0;
        _upButton.Enabled = !_busy && CurrentFolderNode() is { } folder && folder != _fileRootNode;
        if (_extractAllMenuItem is not null) _extractAllMenuItem.Enabled = hasFiles && !_busy;
    }

    private void OnFileListKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Apps || (e.Shift && e.KeyCode == Keys.F10))
        {
            e.Handled = true;
            ShowFileContextMenu();
        }
    }

    private void ShowFileContextMenu()
    {
        if (_fileList.SelectedItems.Count == 0) return;
        if (_fileList.SelectedItems[0].Text == "...") return;
        _fileContextMenu.Show(_fileList, _fileList.PointToClient(Cursor.Position));
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

    private void ActivateFileListItem()
    {
        if (_fileList.SelectedItems.Count == 0) return;
        if (_fileList.SelectedItems[0].Tag is not TreeNode node) return;

        if (node == _fileRootNode)
        {
            _fileTree.SelectedNode = _fileRootNode;
            return;
        }
        if (node.Tag is not PackageFileNode model) return;

        if (model.IsDirectory)
        {
            // Navigating into a folder must not keep re-applying the global filter.
            if (!string.IsNullOrEmpty(_fileFilter.SearchText))
                _fileFilter.SearchText = string.Empty;
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

        // Latest preview wins: cancel the previous request and ignore any late result.
        _previewCancellation?.Cancel();
        _previewCancellation?.Dispose();
        _previewCancellation = new CancellationTokenSource();
        CancellationToken token = _previewCancellation.Token;
        int version = ++_previewVersion;

        _previewInfo.Text = "Previewing " + Path.GetFileName(path) + "...";
        _previewText.Text = "Loading preview...";
        _previewText.Visible = true;
        _previewImage.Visible = false;
        _previewImage.Image?.Dispose();
        _previewImage.Image = null;

        PreviewResult result;
        try
        {
            result = await Task.Run(() => BuildPreview(path, size, token), token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            if (version != _previewVersion || IsDisposed) return;
            _previewImage.Visible = false;
            _previewText.Visible = true;
            _previewText.Text = "Preview failed: " + ex.Message;
            _previewInfo.Text = path;
            return;
        }

        if (version != _previewVersion || token.IsCancellationRequested || IsDisposed)
        {
            result.Image?.Dispose();
            return;
        }

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

    private PreviewResult BuildPreview(string path, long size, CancellationToken token)
    {
        if (_session is null) return new PreviewResult(null, null, "No package is open.");
        if (size > MaximumPreviewBytes)
            return new PreviewResult(null, null, $"The file is too large to preview ({FormatByteSize(size)}). Use Extract.");

        token.ThrowIfCancellationRequested();
        string extension = Path.GetExtension(path).ToLowerInvariant();
        using Stream stream = _session.OpenFile(path);
        token.ThrowIfCancellationRequested();

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
        token.ThrowIfCancellationRequested();
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

    private async Task ExtractSelectedAsync()
    {
        if (_busy || _session is null) return;

        var paths = new List<string>();
        foreach (ListViewItem item in _fileList.SelectedItems)
        {
            if (item.Tag is not TreeNode node || ReferenceEquals(node, _fileRootNode)) continue;
            if (node.Tag is not PackageFileNode model) continue;
            if (model.IsDirectory) paths.AddRange(CollectFilePaths(node));
            else paths.Add(model.FullPath);
        }
        paths = paths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (paths.Count == 0)
        {
            DarkMessageBox.ShowWarning("Select one or more files or folders to extract.", "PkgViewer");
            return;
        }

        if (!PromptExtractionFolder(out string destination)) return;
        if (DarkMessageBox.ShowWarning(
                $"Extract {paths.Count:N0} file(s) into:\n{destination}\n\n" +
                "Each file is written under its package-relative folder.",
                "Extract", DarkDialogButton.YesNo) != DialogResult.Yes)
            return;

        BeginExtractionUi(indeterminate: true);
        _statusState.Text = "Extracting selected data...";
        int done = 0, extracted = 0, skipped = 0, failed = 0;
        var errors = new List<string>();
        try
        {
            _extractCancellation = new CancellationTokenSource();
            CancellationToken token = _extractCancellation.Token;
            PackageConflictPolicy? applyToAll = null;
            foreach (string path in paths)
            {
                token.ThrowIfCancellationRequested();
                _statusState.Text = $"Extracting {done + 1}/{paths.Count}: {path}";
                try
                {
                    string full = PackagePath.ResolveInside(destination, path);
                    bool conflict = File.Exists(full) || Directory.Exists(full);
                    PackageConflictPolicy policy;
                    if (!conflict)
                    {
                        policy = PackageConflictPolicy.Replace;
                    }
                    else if (applyToAll is { } chosen)
                    {
                        policy = chosen;
                    }
                    else
                    {
                        using var conflictForm = new ExtractionConflictForm(Path.GetFileName(full), allowApplyToAll: true);
                        conflictForm.ShowDialog(this);
                        if (conflictForm.Choice == ExtractionConflictChoice.Cancel)
                            throw new OperationCanceledException();
                        policy = conflictForm.Choice switch
                        {
                            ExtractionConflictChoice.Skip => PackageConflictPolicy.Skip,
                            ExtractionConflictChoice.KeepBoth => PackageConflictPolicy.KeepBoth,
                            _ => PackageConflictPolicy.Replace
                        };
                        if (conflictForm.ApplyToAll) applyToAll = policy;
                    }

                    PackageExtractionResult result = await _session.ExtractAsync(
                        new PackageExtractionRequest(path, destination, policy), null, token);
                    switch (result.Outcome)
                    {
                        case PackageExtractionOutcome.Skipped:
                            skipped++;
                            break;
                        case PackageExtractionOutcome.Failed:
                            failed++;
                            errors.Add($"{path}: {result.Error}");
                            break;
                        default:
                            extracted++;
                            break;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failed++;
                    errors.Add($"{path}: {ex.Message}");
                }
                done++;
            }
            ReportExtraction(paths.Count, extracted, skipped, failed, errors, destination);
        }
        catch (OperationCanceledException)
        {
            DarkMessageBox.ShowInformation("Extraction cancelled. Files already extracted are kept.", "PkgViewer");
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
        if (!_filesLoaded) await EnsureFilesAsync();
        if (_session is null || _busy) return;

        string title = string.IsNullOrWhiteSpace(_session.Info.Title)
            ? Path.GetFileNameWithoutExtension(_currentPackagePath)
            : _session.Info.Title;
        if (!PromptExtractionFolder(out string root)) return;
        string target = Path.Combine(root, PackageFileName.Sanitize(title));

        using (var conflictForm = new ExtractionConflictForm(target, allowApplyToAll: false))
        {
            conflictForm.ShowDialog(this);
            if (conflictForm.Choice == ExtractionConflictChoice.Cancel) return;
            _fullExtractPolicy = conflictForm.Choice switch
            {
                ExtractionConflictChoice.Skip => PackageConflictPolicy.Skip,
                ExtractionConflictChoice.KeepBoth => PackageConflictPolicy.KeepBoth,
                _ => PackageConflictPolicy.Replace
            };
        }

        BeginExtractionUi(indeterminate: false);
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
            PackageExtractSummary summary = await _session.ExtractAllAsync(
                target, _fullExtractPolicy, progress, _extractCancellation.Token);
            if (summary.HasFailures)
            {
                using var report = new WarningsForm(
                    [$"{summary.Extracted:N0} extracted, {summary.Skipped:N0} skipped, {summary.Failed:N0} failed.", .. summary.Errors]);
                report.Text = "Extraction completed with errors";
                report.ShowDialog(this);
            }
            else
            {
                DarkMessageBox.ShowInformation(
                    $"Extracted {summary.Extracted:N0} file(s) to:\n{target}" +
                    (summary.Skipped > 0 ? $"\n\n{summary.Skipped:N0} existing file(s) were skipped." : string.Empty),
                    "PkgViewer");
            }
        }
        catch (OperationCanceledException)
        {
            DarkMessageBox.ShowInformation("Extraction cancelled. Files already extracted are kept.", "PkgViewer");
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

    private PackageConflictPolicy _fullExtractPolicy = PackageConflictPolicy.Replace;

    private void ReportExtraction(int total, int extracted, int skipped, int failed,
        IReadOnlyList<string> errors, string destination)
    {
        if (failed > 0)
        {
            using var report = new WarningsForm(
                [$"{extracted:N0} extracted, {skipped:N0} skipped, {failed:N0} failed.", .. errors]);
            report.Text = "Extraction completed with errors";
            report.ShowDialog(this);
        }
        else
        {
            DarkMessageBox.ShowInformation(
                $"Extracted {extracted:N0} of {total:N0} file(s) to:\n{destination}" +
                (skipped > 0 ? $"\n\n{skipped:N0} existing file(s) were skipped." : string.Empty),
                "PkgViewer");
        }
    }

    private bool PromptExtractionFolder(out string folder)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select the extraction folder",
            SelectedPath = Directory.Exists(_lastExtractionDirectory) ? _lastExtractionDirectory : string.Empty
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            folder = string.Empty;
            return false;
        }
        folder = dialog.SelectedPath;
        _lastExtractionDirectory = folder;
        _settings.LastExtractionDirectory = folder;
        _settings.Save();
        return true;
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

    private void BeginExtractionUi(bool indeterminate)
    {
        _busy = true;
        _progressBar.Visible = true;
        _progressBar.Style = indeterminate ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
        _progressBar.MarqueeAnimationSpeed = indeterminate ? 30 : 0;
        _progressBar.Minimum = 0;
        _progressBar.Value = 0;
        _progressBar.Maximum = 1;
        _stopExtractButton.Visible = true;
        _stopExtractButton.Enabled = true;
        _tabs.Enabled = false;
        UpdateFileActionState();
    }

    private void EndExtractionUi()
    {
        _extractCancellation?.Dispose();
        _extractCancellation = null;
        if (_closing || IsDisposed) return;
        _stopExtractButton.Visible = false;
        _progressBar.Visible = false;
        _progressBar.Style = ProgressBarStyle.Continuous;
        _progressBar.MarqueeAnimationSpeed = 0;
        _tabs.Enabled = true;
        _busy = false;
        _statusState.Text = "Ready";
        UpdateFileActionState();
    }

    private void StopExtraction()
    {
        _extractCancellation?.Cancel();
        _stopExtractButton.Enabled = false;
        _statusState.Text = "Stopping extraction (the current file completes, then it stops)...";
    }

    // ------------------------------------------------------------------
    // Menu actions
    // ------------------------------------------------------------------

    private void SelectTab(DarkTabPage page)
    {
        if (_tabs.TabPages.Contains(page)) _tabs.SelectedTab = page;
    }

    private void FocusFileSearch()
    {
        SelectTab(_filesTab);
        _fileFilter.Focus();
    }

    private void OpenAnotherPackage()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Open PS4/PS5 package",
            Filter = "Package files|*.pkg;*.ffpfsc;*.ffpkg;*.exfat|All files|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _currentPackagePath = Path.GetFullPath(dialog.FileName);
        _passcode = null;
        _statusPath.Text = _currentPackagePath;
        _ = LoadPackageAsync();
    }

    private void OpenSourceFolder()
    {
        try
        {
            string? folder = Directory.Exists(_currentPackagePath)
                ? _currentPackagePath
                : Path.GetDirectoryName(_currentPackagePath);
            if (string.IsNullOrEmpty(folder)) return;
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{_currentPackagePath}\"")
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException)
        {
            DarkMessageBox.ShowError(ex.Message, "Open source folder");
        }
    }

    private void CopySourcePath() => CopyToClipboard(_currentPackagePath, "Source path");

    private static void CopyToClipboard(string? value, string what)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            DarkMessageBox.ShowWarning("There is nothing to copy yet.", "PkgViewer");
            return;
        }
        try
        {
            Clipboard.SetText(value);
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
        }
    }

    private void OpenContainingFolder()
    {
        if (_fileList.SelectedItems.Count == 0 || _fileList.SelectedItems[0].Tag is not TreeNode node) return;
        TreeNode? folder = node.Tag is PackageFileNode { IsDirectory: true } ? node : node.Parent;
        if (folder is null) return;
        if (!string.IsNullOrEmpty(_fileFilter.SearchText)) _fileFilter.SearchText = string.Empty;
        _fileTree.SelectedNode = folder;
        folder.EnsureVisible();
    }

    private void TogglePreviewPane()
    {
        bool visible = _previewPaneMenuItem?.Checked ?? true;
        SetPreviewPaneVisible(visible);
        _settings.PreviewPaneVisible = visible;
        _settings.Save();
    }

    private void SetPreviewPaneVisible(bool visible) => _previewPanel.Visible = visible;

    private void ResetLayout()
    {
        _settings.FilesSplitterSizes = string.Empty;
        _settings.FileColumnWidths = string.Empty;
        _settings.PreviewPaneVisible = true;
        if (_filesSplit is not null) _filesSplit.PanelSizes = [280, 420, 380];
        int[] widths = [220, 100, 260, 90];
        for (int index = 0; index < _fileList.Columns.Count && index < widths.Length; index++)
            _fileList.Columns[index].Width = widths[index];
        if (_previewPaneMenuItem is not null) _previewPaneMenuItem.Checked = true;
        SetPreviewPaneVisible(true);
        _settings.Save();
        statusOnly("Layout reset.");
    }

    private async Task RetryContentAccessAsync()
    {
        if (_session is null)
        {
            DarkMessageBox.ShowInformation("Open a package first.", "PkgViewer");
            return;
        }
        if (!PromptForPasscode(out string? passcode)) return;
        _passcode = passcode;
        await LoadPackageAsync();
    }

    private void ShowAssociations() => new FileAssociationsForm().ShowDialog(this);

    private void OpenLogFolder()
    {
        try
        {
            string directory = Path.GetDirectoryName(Logger.LogPath) ?? ".";
            Directory.CreateDirectory(directory);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{directory}\"") { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException)
        {
            DarkMessageBox.ShowError($"Could not open the log folder:\n\n{ex.Message}", "PkgViewer");
        }
    }

    private void CopyDiagnostics()
    {
        var builder = new StringBuilder();
        builder.AppendLine("PkgViewer diagnostic summary");
        builder.AppendLine("Source: " + _currentPackagePath);
        if (_session is { } session)
        {
            builder.AppendLine($"Platform: {session.Info.PlatformDisplay} ({session.Info.FormatDisplay})");
            builder.AppendLine($"Title: {session.Info.Title}");
            builder.AppendLine($"Title ID: {session.Info.TitleId}");
            builder.AppendLine($"Content ID: {session.Info.ContentId}");
            builder.AppendLine($"Warnings: {session.Warnings.Count}");
            foreach (string warning in session.Warnings) builder.AppendLine("  - " + warning);
        }
        else
        {
            builder.AppendLine("No package is open.");
        }
        string summary = builder.ToString();
        try
        {
            Clipboard.SetText(summary);
            DarkMessageBox.ShowInformation("Diagnostic summary copied to the clipboard.", "PkgViewer");
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            using var report = new WarningsForm([summary]);
            report.Text = "Diagnostic summary";
            report.ShowDialog(this);
        }
    }

    private void ExportMetadata()
    {
        if (_session is null) return;
        using var dialog = new SaveFileDialog
        {
            Title = "Export package metadata",
            Filter = "Text report (*.txt)|*.txt|All files|*.*",
            FileName = Path.GetFileNameWithoutExtension(_currentPackagePath) + "-metadata.txt"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            File.WriteAllText(dialog.FileName, BuildMetadataReport(_session));
            DarkMessageBox.ShowInformation("Metadata exported.", "PkgViewer");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            DarkMessageBox.ShowError(ex.Message, "Export metadata");
        }
    }

    private static string BuildMetadataReport(IPackageSession session)
    {
        var builder = new StringBuilder();
        builder.AppendLine("PkgViewer metadata export");
        builder.AppendLine("Generated: " + DateTime.Now.ToString("u"));
        builder.AppendLine();
        PackageInfo info = session.Info;
        builder.AppendLine("[Overview]");
        builder.AppendLine("Source: " + info.SourcePath);
        builder.AppendLine("Platform: " + info.PlatformDisplay + " (" + info.FormatDisplay + ")");
        builder.AppendLine("Title: " + info.Title);
        builder.AppendLine("Title ID: " + info.TitleId);
        builder.AppendLine("Content ID: " + info.ContentId);
        builder.AppendLine("Category: " + PackageCategory.Describe(info.Category));
        builder.AppendLine("Version: " + info.Version);
        builder.AppendLine("Required firmware: " + info.RequiredFirmware);
        foreach (PackageInfoRow row in info.ExtraRows)
            builder.AppendLine($"{row.Label}: {row.Value}");

        AppendRows(builder, "Header", session.HeaderFields);
        AppendRows(builder, "Build info", session.BuildInfoFields);
        AppendRows(builder, "PARAM.SFO", session.SfoEntries.Select(entry => new PackageInfoRow(entry.Name, entry.Value)));
        AppendRows(builder, "Entries", session.EntryRecords.Select(entry =>
            new PackageInfoRow(entry.Name, $"offset {entry.Offset} size {entry.Size} flags {entry.Flags1}/{entry.Flags2} encrypted {entry.Encrypted}")));

        builder.AppendLine();
        builder.AppendLine("[Warnings]");
        if (session.Warnings.Count == 0) builder.AppendLine("(none)");
        else foreach (string warning in session.Warnings) builder.AppendLine("- " + warning);
        return builder.ToString();
    }

    private static void AppendRows(StringBuilder builder, string title, IEnumerable<PackageInfoRow> rows)
    {
        builder.AppendLine();
        builder.AppendLine("[" + title + "]");
        bool any = false;
        foreach (PackageInfoRow row in rows)
        {
            builder.AppendLine($"{row.Label}: {row.Value}");
            any = true;
        }
        if (!any) builder.AppendLine("(none)");
    }

    private void UpdateMenuStates()
    {
        bool hasSession = _session is not null;
        if (_retryAccessMenuItem is not null) _retryAccessMenuItem.Enabled = hasSession;
        if (_exportMetadataMenuItem is not null) _exportMetadataMenuItem.Enabled = hasSession;
        if (_extractAllMenuItem is not null) _extractAllMenuItem.Enabled = hasSession && _filesLoaded && !_busy;
    }

    private void statusOnly(string text) => _statusState.Text = text;

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
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select a folder to save artwork into",
            SelectedPath = Directory.Exists(_lastExtractionDirectory) ? _lastExtractionDirectory : string.Empty
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        string name = Path.GetFileNameWithoutExtension(_currentPackagePath);
        var saved = new List<string>();
        var failures = new List<string>();
        SaveImage(_session.Artwork.Icon, dialog.SelectedPath, name + "_ICON.PNG", saved, failures);
        SaveImage(_session.Artwork.Pic0, dialog.SelectedPath, name + "_PIC0.PNG", saved, failures);
        SaveImage(_session.Artwork.Pic1, dialog.SelectedPath, name + "_PIC1.PNG", saved, failures);
        SaveImage(_session.Artwork.Pic2, dialog.SelectedPath, name + "_PIC2.PNG", saved, failures);

        if (failures.Count > 0)
            DarkMessageBox.ShowWarning(
                $"Saved {saved.Count:N0} image(s); could not save: {string.Join(", ", failures)}", "PkgViewer");
        else if (saved.Count == 0)
            DarkMessageBox.ShowInformation("No artwork was available to save.", "PkgViewer");
        else
            DarkMessageBox.ShowInformation(
                $"Saved {saved.Count:N0} image(s) to:\n{dialog.SelectedPath}\n\n{string.Join(", ", saved)}", "PkgViewer");
    }

    private static void SaveImage(PackageImage? image, string folder, string fileName,
        List<string> saved, List<string> failures)
    {
        if (image is null || image.IsEmpty) return;
        try
        {
            using Bitmap? bitmap = ToBitmap(image);
            if (bitmap is null) return;
            bitmap.Save(Path.Combine(folder, fileName), ImageFormat.Png);
            saved.Add(fileName);
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
        if (_extractCancellation is not null)
        {
            e.Cancel = true;
            if (DarkMessageBox.ShowWarning(
                    "Extraction is in progress. Stop it and close?", "PkgViewer",
                    DarkDialogButton.YesNo) != DialogResult.Yes)
                return;
            _extractCancellation.Cancel();
        }

        _closing = true;
        _lifetimeCancellation.Cancel();
        _previewCancellation?.Cancel();
        CaptureFileLayout();
        ReleaseResources();
    }

    private void CaptureFileLayout()
    {
        try
        {
            if (_filesSplit is not null) _settings.FilesSplitterSizes = string.Join(",", _filesSplit.PanelSizes);
            _settings.FileColumnWidths = string.Join(",", _fileList.Columns.Cast<ColumnHeader>().Select(column => column.Width));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException) { }

        Rectangle bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
        if (bounds.Width >= 800 && bounds.Height >= 560)
        {
            _settings.WindowWidth = bounds.Width;
            _settings.WindowHeight = bounds.Height;
        }
        _settings.WindowMaximized = WindowState == FormWindowState.Maximized;
        _settings.Save();
    }

    private void RestoreFileLayout()
    {
        if (_filesSplit is not null && TryParseLayout(_settings.FilesSplitterSizes, 3, out int[] sizes))
            _filesSplit.PanelSizes = sizes;
        if (TryParseLayout(_settings.FileColumnWidths, _fileList.Columns.Count, out int[] widths))
            for (int index = 0; index < widths.Length; index++) _fileList.Columns[index].Width = widths[index];
    }

    private static bool TryParseLayout(string text, int expected, out int[] values)
    {
        values = [];
        if (string.IsNullOrWhiteSpace(text)) return false;
        string[] parts = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != expected) return false;
        var parsed = new int[expected];
        for (int index = 0; index < expected; index++)
            if (!int.TryParse(parts[index], out parsed[index]) || parsed[index] <= 0) return false;
        values = parsed;
        return true;
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
