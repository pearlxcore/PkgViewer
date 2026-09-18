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
    private const long MaximumDecodedPixels = 4096L * 4096L;

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
    private int _openGeneration;
    private bool _closing;
    private bool _closeAfterExtraction;
    private readonly System.Windows.Forms.Timer _fileSearchDebounce = new() { Interval = 200 };
    private TreeNode? _fileRootNode;
    private DataGridView? _contextGrid;
    private string _lastExtractionDirectory;
    private readonly List<Image> _trophyImages = [];
    private readonly List<DarkTabPage> _detailTabPages = [];

    public PackageViewerForm(string packagePath)
    {
        _currentPackagePath = Path.GetFullPath(packagePath);
        _lastExtractionDirectory = _settings.LastExtractionDirectory;
        AppIcon.Apply(this);

        InitializeComponent();

        // Runtime population of designer-created controls (image list images).
        FileIcons.Populate(_fileIcons);

        _fileSearchDebounce.Tick += (_, _) =>
        {
            _fileSearchDebounce.Stop();
            RefreshFileList();
        };

        _previewPaneMenuItem.Checked = _settings.PreviewPaneVisible;
        SetPreviewPaneVisible(_settings.PreviewPaneVisible);
        RestoreFileLayout();
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

    private async Task LoadPackageAsync(string path)
    {
        // Each open owns a generation; a superseded open's result is disposed rather than published.
        int generation = ++_openGeneration;
        SetLoadingState(true, "Reading package metadata...");
        _statusPath.Text = path;
        try
        {
            CancellationToken token = _lifetimeCancellation.Token;
            // Probe + backend open run off the UI thread (the probe reads file signatures, which can
            // block on a slow or network source).
            IPackageSession session = await Task.Run(
                () => _openService.OpenAsync(path, new PackageOpenOptions { Passcode = _passcode }, token), token);
            if (generation != _openGeneration || _closing)
            {
                session.Dispose();
                return;
            }

            IPackageSession? previous = _session;
            _session = session;
            _currentPackagePath = path;
            previous?.Dispose();

            Populate();
            SetLoadingState(false, "Ready");
            await OnTabSelectedAsync();
        }
        catch (OperationCanceledException)
        {
            if (generation == _openGeneration) SetLoadingState(false, "Ready");
        }
        catch (Exception ex)
        {
            if (generation != _openGeneration) return;
            Logger.Exception("Open package", ex);
            // PS5 reading has no credential-aware path, so a passcode prompt there would be misleading.
            if (_passcode is null && IsPasscodeFailure(ex) && !IsPs5Source(ex) &&
                PromptForPasscode(out string? passcode))
            {
                _passcode = passcode;
                await LoadPackageAsync(path);
                return;
            }
            SetLoadingState(false, "Unable to read package");
            _statusPath.Text = _currentPackagePath;
            DarkMessageBox.ShowError(
                "The supplied file could not be read as a PS4/PS5 package.\n\n" + ex.Message, "PkgViewer");
        }
    }

    private void Populate()
    {
        if (_session is null) return;
        PackageInfo info = _session.Info;
        string displayTitle = string.IsNullOrWhiteSpace(info.Title) ? info.FileName : info.Title;

        Text = "Pkg Viewer - " + displayTitle;
        _titleLabel.Text = displayTitle + TitleSuffix(info);
        _subtitleLabel.Text = string.Join("  •  ", new[]
        {
            info.TitleId,
            DescribeCategory(info),
            info.BuildState,
            string.IsNullOrWhiteSpace(info.Version) ? string.Empty : "v" + info.Version,
            SizeText(info),
            info.Region
        }.Where(part => !string.IsNullOrWhiteSpace(part)));
        _contentIdLabel.Text = info.ContentId;
        _toolTip.SetToolTip(_titleLabel, displayTitle);
        _toolTip.SetToolTip(_contentIdLabel, info.ContentId);
        SetImage(_iconBox, _session.Artwork.Icon);
        PopulateArtwork();

        _overviewSummary.Rows.Clear();
        SetOverviewValue("Title", info.Title);
        SetOverviewValue("Title ID", info.TitleId);
        SetOverviewValue("Content ID", info.ContentId);
        SetOverviewValue("Category", DescribeCategory(info));
        SetOverviewValue("Region", info.Region);
        SetOverviewValue("Package state", info.BuildState);
        SetOverviewValue("Application version", info.Version);
        SetOverviewValue("Package version", info.PackageVersion);
        SetOverviewValue("Required firmware", info.RequiredFirmware);
        SetOverviewValue("Package size", SizeText(info));
        AppendOverviewExtras(info.ExtraRows);

        _sfoGrid.Rows.Clear();
        foreach (PackageSfoEntry entry in _session.SfoEntries)
            _sfoGrid.Rows.Add(entry.Name, entry.Value);

        PopulateInspectionGrid(_headerGrid, _session.HeaderFields, "Header information");
        PopulateInspectionGrid(_buildGrid, _session.BuildInfoFields, "PUBTOOLINFO");

        _entryRecords = _session.EntryRecords.ToList();
        PopulateEntriesGrid();

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
        IPackageSession session = _session;
        int generation = _openGeneration;
        try
        {
            IReadOnlyList<PackageDetailTab> tabs = await session.GetDetailTabsAsync(_lifetimeCancellation.Token);
            if (IsDisposed || session != _session || generation != _openGeneration) return;
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
        IPackageSession session = _session;
        int generation = _openGeneration;
        _statusState.Text = "Loading package file list...";
        try
        {
            IReadOnlyList<PackageFileRecord> files = await session.GetFilesAsync(_lifetimeCancellation.Token);
            // Reject results if the source changed while the listing loaded.
            if (IsDisposed || session != _session || generation != _openGeneration) return;
            _allFiles = files;
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
        IPackageSession session = _session;
        int generation = _openGeneration;
        _trophyState.Text = "Loading trophy information...";
        _statusState.Text = "Loading trophy information...";
        try
        {
            IReadOnlyList<PackageTrophy> trophies = await session.GetTrophiesAsync(_lifetimeCancellation.Token);
            if (IsDisposed || session != _session || generation != _openGeneration) return;
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
        _toolTip.SetToolTip(_iconArtBox, DescribeArtwork("ICON", _iconArtBox, artwork.Icon));
        _toolTip.SetToolTip(_pic0Box, DescribeArtwork("PIC0", _pic0Box, artwork.Pic0));
        _toolTip.SetToolTip(_pic1Box, DescribeArtwork("PIC1", _pic1Box, artwork.Pic1));
        _toolTip.SetToolTip(_pic2Box, DescribeArtwork("PIC2", _pic2Box, artwork.Pic2));
    }

    private static string DescribeArtwork(string slot, PictureBox box, PackageImage? image)
    {
        if (image is null || image.IsEmpty || box.Image is null) return $"{slot}: not present";
        return $"{slot}: {box.Image.Width}x{box.Image.Height}";
    }

    private void SaveArtworkSlot(PictureBox box, string slot)
    {
        if (box.Image is null)
        {
            DarkMessageBox.ShowInformation($"The {slot} image is not present in this package.", "PkgViewer");
            return;
        }
        using var dialog = new SaveFileDialog
        {
            Title = $"Save {slot}",
            Filter = "PNG image (*.png)|*.png",
            FileName = Path.GetFileNameWithoutExtension(_currentPackagePath) + "_" + slot + ".png"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            box.Image.Save(dialog.FileName, ImageFormat.Png);
        }
        catch (Exception ex) when (ex is IOException or ExternalException or ArgumentException)
        {
            DarkMessageBox.ShowError(ex.Message, "Save image");
        }
    }

    private static void SetArtworkSlot(PictureBox box, DarkLabel empty, PackageImage? image)
    {
        SetImage(box, image);
        bool hasImage = box.Image is not null;
        box.Visible = hasImage;
        empty.Visible = !hasImage;
    }

    private void SetOverviewValue(string key, string value) =>
        _overviewSummary.Rows.Add(key, string.IsNullOrWhiteSpace(value) ? "Not available" : value);

    /// <summary>Adds the backend's format-specific fields beneath the fixed summary rows.</summary>
    private void AppendOverviewExtras(IReadOnlyList<PackageInfoRow> rows)
    {
        foreach (PackageInfoRow row in rows)
            _overviewSummary.Rows.Add(row.Label, row.Value);
    }

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

    private IReadOnlyList<PackageTrophy> _trophies = [];

    private void ResetTrophyState()
    {
        foreach (Image image in _trophyImages) image.Dispose();
        _trophyImages.Clear();
        _trophies = [];
        _trophyGrid.Rows.Clear();
        _trophyState.Text = "Trophy information loads when this page is selected.";
    }

    private void PopulateTrophies(IReadOnlyList<PackageTrophy> trophies)
    {
        _trophies = trophies;
        _trophyGrid.Rows.Clear();
        if (_session is null) return;

        if (trophies.Count == 0)
        {
            _trophyState.Text = string.IsNullOrWhiteSpace(_session.TrophyMessage)
                ? "No trophy data was found in this package."
                : _session.TrophyMessage;
            return;
        }

        _trophyState.Text = BuildTrophySummary(trophies);
        ApplyTrophyFilter();
    }

    /// <summary>Rebuilds the trophy rows for the current filter query.</summary>
    private void ApplyTrophyFilter()
    {
        foreach (Image image in _trophyImages) image.Dispose();
        _trophyImages.Clear();
        _trophyGrid.Rows.Clear();
        if (_trophies.Count == 0) return;

        string query = _trophyFilter.SearchText.Trim();
        int shown = 0;
        foreach (PackageTrophy trophy in _trophies)
        {
            if (query.Length > 0 && !MatchesTrophy(trophy, query)) continue;
            Image? icon = ToBitmap(trophy.Icon);
            if (icon is not null) _trophyImages.Add(icon);
            _trophyGrid.Rows.Add(new object?[]
            {
                icon, trophy.Id.ToString("000"), trophy.Name, trophy.Description, trophy.Grade,
                trophy.Hidden ? "Yes" : "No"
            });
            shown++;
        }

        if (query.Length > 0)
            _trophyState.Text = $"{shown:N0} of {_trophies.Count:N0} trophies match '{query}'.";
        else
            _trophyState.Text = BuildTrophySummary(_trophies);
    }

    private static bool MatchesTrophy(PackageTrophy trophy, string query) =>
        trophy.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        trophy.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        trophy.Grade.Contains(query, StringComparison.OrdinalIgnoreCase);

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
            // Setting Text can leave the caret at the end and scroll the first line out of view.
            _previewText.SelectionStart = 0;
            _previewText.SelectionLength = 0;
            _previewText.ScrollToCaret();
            _previewInfo.Text = result.Image is null && result.Text is not null
                ? $"{Path.GetFileName(path)} ({FormatByteSize(size)}) - preview"
                : path;
        }
    }

    /// <summary>Extensions rendered as text when they also pass the text heuristic. Everything else,
    /// including unknown extensions, defaults to a hex dump.</summary>
    private static readonly HashSet<string> TextPreviewExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".text", ".log", ".json", ".xml", ".sfo", ".ini", ".cfg", ".conf", ".csv", ".md",
        ".yaml", ".yml", ".toml", ".html", ".htm", ".css", ".js", ".ts", ".c", ".h", ".cpp", ".hpp",
        ".cs", ".py", ".lua", ".sh", ".bat", ".cmd", ".ps1", ".sql", ".resx", ".config", ".strings",
        ".po", ".srt", ".vtt", ".glsl", ".hlsl", ".frag", ".vert", ".diff", ".patch", ".gitignore"
    };

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
            if ((long)source.Width * source.Height > MaximumDecodedPixels)
                return new PreviewResult(null, null,
                    $"The image is too large to decode ({source.Width}x{source.Height}). Use Extract.");
            return new PreviewResult(new Bitmap(source), null, null);
        }

        if (extension == ".dds")
        {
            (byte[] rgba, int width, int height) = Ps5ImageCodec.DecodeDdsToRgba(stream);
            if ((long)width * height > MaximumDecodedPixels)
                return new PreviewResult(null, null,
                    $"The image is too large to decode ({width}x{height}). Use Extract.");
            return new PreviewResult(BitmapFromRgba(rgba, width, height), null, null);
        }

        // Only known text extensions are rendered as text (and only when they also look like text);
        // every other extension, including unknown ones, defaults to a hex dump. A small sample is
        // read first so a binary file is not read up to the full text budget for a 16 KiB hex view.
        byte[] sample = ReadAtMost(stream, MaximumHexBytes);
        token.ThrowIfCancellationRequested();
        if (TextPreviewExtensions.Contains(extension) && IsProbablyText(sample, out Encoding encoding))
        {
            byte[] buffer = ReadUpTo(stream, sample, MaximumTextBytes);
            string text = encoding.GetString(buffer);
            if (size > buffer.Length)
                text += Environment.NewLine +
                    $"... [truncated: showing the first {FormatByteSize(buffer.Length)} of {FormatByteSize(size)}]";
            return new PreviewResult(null, text, null);
        }

        string header = size > sample.Length
            ? $"[hex preview: first {FormatByteSize(sample.Length)} of {FormatByteSize(size)}]{Environment.NewLine}"
            : string.Empty;
        return new PreviewResult(null, header + BuildHexDump(sample), null);
    }

    /// <summary>Continues reading from <paramref name="stream"/> after an initial sample, up to a cap.</summary>
    private static byte[] ReadUpTo(Stream stream, byte[] initial, int maximumBytes)
    {
        if (initial.Length >= maximumBytes) return initial;
        byte[] buffer = new byte[maximumBytes];
        Array.Copy(initial, buffer, initial.Length);
        int total = initial.Length;
        while (total < maximumBytes)
        {
            int read = stream.Read(buffer, total, maximumBytes - total);
            if (read == 0) break;
            total += read;
        }
        return total == buffer.Length ? buffer : buffer[..total];
    }

    /// <summary>
    /// Best-effort text detection. Recognises a UTF-8/UTF-16 byte-order mark, then falls back to a
    /// null-byte distribution heuristic so UTF-16 text is not mistaken for binary.
    /// </summary>
    private static bool IsProbablyText(byte[] buffer, out Encoding encoding)
    {
        encoding = Encoding.UTF8;
        if (buffer.Length == 0) return true;
        if (buffer.Length >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF) return true;
        if (buffer.Length >= 2 && buffer[0] == 0xFF && buffer[1] == 0xFE) { encoding = Encoding.Unicode; return true; }
        if (buffer.Length >= 2 && buffer[0] == 0xFE && buffer[1] == 0xFF) { encoding = Encoding.BigEndianUnicode; return true; }

        int limit = Math.Min(buffer.Length, 4096);
        int nulls = 0, oddNulls = 0, evenNulls = 0, nonPrintable = 0;
        for (int index = 0; index < limit; index++)
        {
            byte value = buffer[index];
            if (value == 0)
            {
                nulls++;
                if ((index & 1) == 0) evenNulls++;
                else oddNulls++;
            }
            else if (value < 0x09 || (value > 0x0D && value < 0x20) || value == 0x7F)
            {
                nonPrintable++;
            }
        }

        // UTF-16 text: many null bytes alternating on one side.
        if (nulls > limit * 0.1 && (oddNulls > limit * 0.3 || evenNulls > limit * 0.3))
        {
            encoding = oddNulls >= evenNulls ? Encoding.Unicode : Encoding.BigEndianUnicode;
            return true;
        }

        // Otherwise treat it as text only when it has no nulls and few control characters.
        return nulls == 0 && nonPrintable <= limit * 0.1;
    }

    // ------------------------------------------------------------------
    // Extraction
    // ------------------------------------------------------------------

    private Task ExtractSelectedAsync() =>
        ExtractPathsAsync(SelectedListFilePaths(), "Select one or more files or folders to extract.");

    private Task ExtractTreeSelectionAsync() =>
        ExtractPathsAsync(SelectedTreeFilePaths(), "Select a file or folder in the tree to extract.");

    private List<string> SelectedListFilePaths()
    {
        var paths = new List<string>();
        foreach (ListViewItem item in _fileList.SelectedItems)
        {
            if (item.Tag is not TreeNode node || ReferenceEquals(node, _fileRootNode)) continue;
            if (node.Tag is not PackageFileNode model) continue;
            if (model.IsDirectory) paths.AddRange(CollectFilePaths(node));
            else paths.Add(model.FullPath);
        }
        return paths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private List<string> SelectedTreeFilePaths()
    {
        var paths = new List<string>();
        if (_fileTree.SelectedNode is { } node && node.Tag is PackageFileNode model)
        {
            if (model.IsDirectory) paths.AddRange(CollectFilePaths(node));
            else paths.Add(model.FullPath);
        }
        return paths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private async Task ExtractPathsAsync(IReadOnlyList<string> paths, string emptyMessage)
    {
        if (_busy || _session is null) return;
        if (paths.Count == 0)
        {
            DarkMessageBox.ShowWarning(emptyMessage, "PkgViewer");
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
        UpdateStatusSeparators();
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
        UpdateStatusSeparators();
        UpdateFileActionState();

        // A close requested during extraction waits until the extraction has unwound.
        if (_closeAfterExtraction)
        {
            _closeAfterExtraction = false;
            BeginInvoke(new Action(Close));
        }
    }

    private void StopExtraction()
    {
        _extractCancellation?.Cancel();
        _stopExtractButton.Enabled = false;
        _statusState.Text = "Stopping extraction (the current file completes, then it stops)...";
    }

    /// <summary>
    /// Drag-out extraction: the selected entries (or everything under a folder) are extracted to a
    /// temporary folder, then offered as a file drop so they can be dropped into Explorer.
    /// </summary>
    private async void OnFilesItemDrag(object? sender, ItemDragEventArgs e)
    {
        if (_busy || _session is null) return;
        List<string> paths = ReferenceEquals(sender, _fileTree) ? SelectedTreeFilePaths() : SelectedListFilePaths();
        if (paths.Count == 0) return;

        Control source = sender as Control ?? _fileList;
        string dragRoot = Path.Combine(Path.GetTempPath(), "PkgViewer", "Drag", Guid.NewGuid().ToString("N"));
        _dragRoots.Add(dragRoot);
        Directory.CreateDirectory(dragRoot);

        var extracted = new List<string>();
        _statusState.Text = $"Preparing {paths.Count:N0} file(s) for drag...";
        try
        {
            foreach (string relative in paths)
            {
                _lifetimeCancellation.Token.ThrowIfCancellationRequested();
                try
                {
                    PackageExtractionResult result = await _session.ExtractAsync(
                        new PackageExtractionRequest(relative, dragRoot, PackageConflictPolicy.Replace),
                        null, _lifetimeCancellation.Token);
                    if (result.Outcome != PackageExtractionOutcome.Failed)
                        extracted.Add(result.DestinationPath);
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or NotSupportedException)
                {
                    // Skip unreadable entries; the rest can still be dragged.
                }
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            if (!_closing) _statusState.Text = "Ready";
        }

        if (extracted.Count == 0 || _closing) return;
        try
        {
            source.DoDragDrop(new DataObject(DataFormats.FileDrop, extracted.ToArray()), DragDropEffects.Copy);
        }
        catch (InvalidOperationException)
        {
        }
    }

    private readonly List<string> _dragRoots = [];

    private void CleanupDragRoots()
    {
        foreach (string root in _dragRoots)
        {
            try
            {
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        _dragRoots.Clear();
    }

    // ------------------------------------------------------------------
    // Menu actions
    // ------------------------------------------------------------------

    private List<PackageEntryRecord> _entryRecords = [];
    private string _entriesSortColumn = string.Empty;
    private bool _entriesSortAscending = true;

    private void OnEntriesColumnHeaderClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.ColumnIndex < 0 || e.ColumnIndex >= _entriesGrid.Columns.Count) return;
        string column = _entriesGrid.Columns[e.ColumnIndex].HeaderText;
        if (string.Equals(_entriesSortColumn, column, StringComparison.Ordinal))
            _entriesSortAscending = !_entriesSortAscending;
        else
        {
            _entriesSortColumn = column;
            _entriesSortAscending = true;
        }
        PopulateEntriesGrid();
    }

    private void PopulateEntriesGrid()
    {
        IEnumerable<PackageEntryRecord> ordered = _entriesSortColumn switch
        {
            "Offset" => OrderBy(_entriesSortAscending, _entryRecords, entry => ParseOffsetValue(entry.Offset)),
            "Size" => OrderBy(_entriesSortAscending, _entryRecords, entry => ParseSizeText(entry.Size)),
            "Name" => OrderBy(_entriesSortAscending, _entryRecords, entry => entry.Name),
            _ => _entryRecords
        };

        _entriesGrid.Rows.Clear();
        foreach (PackageEntryRecord entry in ordered)
            _entriesGrid.Rows.Add(entry.Name, entry.Offset, entry.Size, entry.Flags1, entry.Flags2, entry.Encrypted);
    }

    private static IEnumerable<T> OrderBy<T, TKey>(bool ascending, IEnumerable<T> source, Func<T, TKey> selector) =>
        ascending ? source.OrderBy(selector) : source.OrderByDescending(selector);

    private static long ParseOffsetValue(string value)
    {
        string text = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value[2..] : value;
        return long.TryParse(text, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out long parsed)
            ? parsed
            : 0;
    }

    private static long ParseSizeText(string value)
    {
        string[] parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 ||
            !double.TryParse(parts[0], System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double amount))
            return 0;
        double scale = parts[1].ToUpperInvariant() switch
        {
            "BYTES" => 1d,
            "KB" => 1024d,
            "MB" => 1024d * 1024,
            "GB" => 1024d * 1024 * 1024,
            "TB" => 1024d * 1024 * 1024 * 1024,
            _ => 0d
        };
        return scale > 0 ? (long)(amount * scale) : 0;
    }

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
        OpenDroppedFile(dialog.FileName);
    }

    private void OpenDroppedFile(string path)
    {
        if (!File.Exists(path)) return;
        // The active path/session only change once the new source opens successfully.
        _passcode = null;
        _ = LoadPackageAsync(Path.GetFullPath(path));
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

    private void OnFileTreeNodeMouseClick(object? sender, TreeNodeMouseClickEventArgs e)
    {
        if (e.Button == MouseButtons.Right && e.Node is not null)
            _fileTree.SelectedNode = e.Node;
    }

    /// <summary>Double-clicking a file in the tree previews it; a folder opens in place.</summary>
    private void OnFileTreeNodeDoubleClick(object? sender, TreeNodeMouseClickEventArgs e)
    {
        if (e.Node?.Tag is not PackageFileNode model) return;
        _fileTree.SelectedNode = e.Node;
        if (model.IsDirectory)
        {
            e.Node.Expand();
            return;
        }
        _ = PreviewFileAsync(model.FullPath, model.Size);
    }

    private void PreviewTreeNode()
    {
        if (_fileTree.SelectedNode?.Tag is PackageFileNode { IsDirectory: false } model)
            _ = PreviewFileAsync(model.FullPath, model.Size);
    }

    private void CopySelectedTreePath()
    {
        if (_fileTree.SelectedNode?.Tag is PackageFileNode model) CopyToClipboard(model.FullPath, "Path");
    }

    private void CopySelectedTreeName()
    {
        if (_fileTree.SelectedNode is { } node && node != _fileRootNode) CopyToClipboard(node.Text, "Name");
    }

    private void TogglePreviewPane()
    {
        bool visible = _previewPaneMenuItem?.Checked ?? true;
        SetPreviewPaneVisible(visible);
        _settings.PreviewPaneVisible = visible;
        _settings.Save();
    }

    private int[]? _savedPreviewSplitSizes;

    /// <summary>
    /// Adds or removes the preview pane from the file split so disabling it leaves only the folder
    /// tree and the file list, rather than an empty third pane.
    /// </summary>
    private void SetPreviewPaneVisible(bool visible)
    {
        if (_filesSplit is null) return;
        bool present = _filesSplit.Panels.Contains(_filesSplitPane3);
        if (visible && !present)
        {
            _filesSplit.AddPanel(_filesSplitPane3);
            if (_savedPreviewSplitSizes is { Length: 3 }) _filesSplit.PanelSizes = _savedPreviewSplitSizes;
        }
        else if (!visible && present)
        {
            _savedPreviewSplitSizes = _filesSplit.PanelSizes;
            _filesSplit.RemovePanel(_filesSplitPane3);
        }
    }

    private void ResetLayout()
    {
        _settings.FilesSplitterSizes = string.Empty;
        _settings.FileColumnWidths = string.Empty;
        _settings.PreviewPaneVisible = true;
        if (_previewPaneMenuItem is not null) _previewPaneMenuItem.Checked = true;
        SetPreviewPaneVisible(true);
        _savedPreviewSplitSizes = null;
        if (_filesSplit is not null) _filesSplit.PanelSizes = [280, 420, 380];
        int[] widths = [220, 100, 260, 90];
        for (int index = 0; index < _fileList.Columns.Count && index < widths.Length; index++)
            _fileList.Columns[index].Width = widths[index];
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
        await LoadPackageAsync(_currentPackagePath);
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
        // The action loads its own file index, so it does not require a prior Files-tab visit.
        if (_extractAllMenuItem is not null) _extractAllMenuItem.Enabled = hasSession && !_busy;
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

    private const string KoFiUrl = "https://ko-fi.com/R6R524N7X";
    private const string PayPalUrl = "https://www.paypal.com/paypalme/pearlxcoree";

    private void OpenExternalUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or InvalidOperationException)
        {
            DarkMessageBox.ShowError(ex.Message, "Open link");
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

    /// <summary>True when the failed open was a PS5 source (no supported passcode path exists).</summary>
    private static bool IsPs5Source(Exception exception) =>
        exception is PackageOpenException { Probe.Platform: PkgPlatform.Ps5 };

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
        UpdateStatusSeparators();
    }

    /// <summary>
    /// The two status separators only make sense next to the progress bar / stop button, so they are
    /// hidden while those controls are hidden (previously they showed as a stray "||" when idle).
    /// </summary>
    private void UpdateStatusSeparators()
    {
        _statusSeparator1.Visible = _progressBar.Visible;
        _statusSeparator2.Visible = _stopExtractButton.Visible;
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_extractCancellation is not null && !_closeAfterExtraction)
        {
            e.Cancel = true;
            if (DarkMessageBox.ShowWarning(
                    "Extraction is in progress. Stop it and close?", "PkgViewer",
                    DarkDialogButton.YesNo) != DialogResult.Yes)
                return; // declining leaves the session fully usable

            // Cancel and wait for the extraction to unwind; EndExtractionUi closes the window once it
            // is safe (disposing while the reader is still running would race it).
            _closeAfterExtraction = true;
            _statusState.Text = "Stopping extraction...";
            _stopExtractButton.Enabled = false;
            _extractCancellation.Cancel();
            return;
        }

        _closing = true;
        _fileSearchDebounce.Stop();
        _fileSearchDebounce.Dispose();
        _lifetimeCancellation.Cancel();
        _previewCancellation?.Cancel();
        CaptureFileLayout();
        CleanupDragRoots();
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
        if (_filesSplit is not null &&
            TryParseLayout(_settings.FilesSplitterSizes, _filesSplit.Panels.Count, out int[] sizes))
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

    // ------------------------------------------------------------------
    // Designer event handlers
    // ------------------------------------------------------------------

    private async void OnFormShown(object? sender, EventArgs e)
    {
        if (_shown) return;
        _shown = true;
        await LoadPackageAsync(_currentPackagePath);
    }

    private async void OnTabsSelectedIndexChanged(object? sender, EventArgs e) => await OnTabSelectedAsync();

    private void OnTitleDoubleClick(object? sender, EventArgs e) => CopyToClipboard(_titleLabel.Text, "Title");

    /// <summary>Double-clicking a Package Summary row copies its value.</summary>
    private void OnOverviewSummaryCellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _overviewSummary.Rows.Count) return;
        DataGridViewRow row = _overviewSummary.Rows[e.RowIndex];
        string caption = row.Cells[0].Value?.ToString() ?? "Value";
        string value = row.Cells[1].Value?.ToString() ?? string.Empty;
        CopyToClipboard(value, caption);
    }

    private void OnStatusWarningsClick(object? sender, EventArgs e) => ShowWarnings();

    private void OnStopExtractClick(object? sender, EventArgs e) => StopExtraction();

    private void OnTrophyFilterChanged(object? sender, EventArgs e) => ApplyTrophyFilter();

    private void OnFileFilterChanged(object? sender, EventArgs e)
    {
        // Debounce so typing does not rebuild the result list on every keystroke.
        _fileSearchDebounce.Stop();
        _fileSearchDebounce.Start();
    }

    private void OnFileTreeAfterSelect(object? sender, TreeViewEventArgs e) => OnFileTreeNodeSelected();

    private void OnFileListItemActivate(object? sender, EventArgs e) => ActivateFileListItem();

    private void OnFileListSelectedIndexChanged(object? sender, EventArgs e) => UpdateFileActionState();

    private void OnUpClick(object? sender, EventArgs e) => NavigateUp();

    private void OnExtractSelectedClick(object? sender, EventArgs e) => _ = ExtractSelectedAsync();

    private void OnExtractAllClick(object? sender, EventArgs e) => _ = ExtractFullAsync();

    private void OnFileContextPreview(object? sender, EventArgs e) => ActivateFileListItem();
    private void OnFileContextExtract(object? sender, EventArgs e) => _ = ExtractSelectedAsync();
    private void OnFileContextOpenContaining(object? sender, EventArgs e) => OpenContainingFolder();
    private void OnFileContextCopyPath(object? sender, EventArgs e) => CopySelectedPath();
    private void OnFileContextCopyName(object? sender, EventArgs e) => CopySelectedName();

    private void OnTreeContextPreview(object? sender, EventArgs e) => PreviewTreeNode();
    private void OnTreeContextExtract(object? sender, EventArgs e) => _ = ExtractTreeSelectionAsync();
    private void OnTreeContextExpand(object? sender, EventArgs e) => _fileTree.SelectedNode?.Expand();
    private void OnTreeContextCollapse(object? sender, EventArgs e) => _fileTree.SelectedNode?.Collapse();
    private void OnTreeContextExpandAll(object? sender, EventArgs e) => _fileTree.ExpandAll();
    private void OnTreeContextCollapseAll(object? sender, EventArgs e) => _fileTree.CollapseAll();
    private void OnTreeContextCopyPath(object? sender, EventArgs e) => CopySelectedTreePath();
    private void OnTreeContextCopyName(object? sender, EventArgs e) => CopySelectedTreeName();

    private void OnFileTreeContextOpening(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        TreeNode? node = _fileTree.SelectedNode;
        bool isFile = node?.Tag is PackageFileNode { IsDirectory: false };
        bool hasFiles = node?.Tag is PackageFileNode model && (model.IsDirectory ? node.Nodes.Count > 0 : true);
        _treePreviewMenuItem.Enabled = isFile && !_busy;
        _treeExtractMenuItem.Enabled = hasFiles && !_busy;
    }

    private void OnGridContextMenuNeeded(object? sender, DataGridViewCellContextMenuStripNeededEventArgs e)
    {
        _contextGrid = sender as DataGridView;
        e.ContextMenuStrip = _gridContextMenu;
    }

    private void OnGridCopyValue(object? sender, EventArgs e)
    {
        if (_contextGrid?.CurrentCell is { } cell) CopyToClipboard(cell.Value?.ToString(), "Value");
    }

    private void OnGridCopyRow(object? sender, EventArgs e)
    {
        if (_contextGrid?.CurrentRow is not { } row) return;
        string text = string.Join("\t", row.Cells.Cast<DataGridViewCell>()
            .Select(cell => cell.Value?.ToString() ?? string.Empty));
        CopyToClipboard(text, "Row");
    }

    private void OnDragEnterPackage(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true) e.Effect = DragDropEffects.Copy;
    }

    private void OnDragDropPackage(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files)
            OpenDroppedFile(files[0]);
    }

    private void OnMenuOpen(object? sender, EventArgs e) => OpenAnotherPackage();
    private void OnMenuExtractAll(object? sender, EventArgs e) => _ = ExtractFullAsync();
    private void OnMenuSaveArtwork(object? sender, EventArgs e) => SaveArtwork();
    private void OnMenuExportMetadata(object? sender, EventArgs e) => ExportMetadata();
    private void OnMenuOpenSourceFolder(object? sender, EventArgs e) => OpenSourceFolder();
    private void OnMenuExit(object? sender, EventArgs e) => Close();
    private void OnMenuCopyTitle(object? sender, EventArgs e) => CopyInfo("title");
    private void OnMenuCopyTitleId(object? sender, EventArgs e) => CopyInfo("title_id");
    private void OnMenuCopyContentId(object? sender, EventArgs e) => CopyInfo("content_id");
    private void OnMenuCopySourcePath(object? sender, EventArgs e) => CopySourcePath();
    private void OnMenuFindFiles(object? sender, EventArgs e) => FocusFileSearch();
    private void OnMenuViewOverview(object? sender, EventArgs e) => SelectTab(_overviewTab);
    private void OnMenuViewFiles(object? sender, EventArgs e) => SelectTab(_filesTab);
    private void OnMenuViewArtwork(object? sender, EventArgs e) => SelectTab(_artworkTab);
    private void OnMenuViewTrophies(object? sender, EventArgs e) => SelectTab(_trophyTab);
    private void OnMenuViewInternals(object? sender, EventArgs e) => SelectTab(_packageTab);
    private void OnMenuPreviewPane(object? sender, EventArgs e) => TogglePreviewPane();
    private void OnMenuResetLayout(object? sender, EventArgs e) => ResetLayout();
    private void OnMenuRetryAccess(object? sender, EventArgs e) => _ = RetryContentAccessAsync();
    private void OnMenuFileAssociations(object? sender, EventArgs e) => ShowAssociations();
    private void OnMenuRemoveLegacy(object? sender, EventArgs e) => RemoveLegacyIntegration();
    private void OnMenuOpenLogFolder(object? sender, EventArgs e) => OpenLogFolder();
    private void OnMenuCopyDiagnostics(object? sender, EventArgs e) => CopyDiagnostics();
    private void OnMenuAbout(object? sender, EventArgs e) => new AboutForm(AppVersion()).ShowDialog(this);

    /// <summary>The assembly version shown on the About window (e.g. "1.0.0").</summary>
    private static string AppVersion()
    {
        Version? version = typeof(PackageViewerForm).Assembly.GetName().Version;
        if (version is null) return "1.0.0";
        return version.Build >= 0 ? $"{version.Major}.{version.Minor}.{version.Build}" : $"{version.Major}.{version.Minor}";
    }
    private void OnMenuKofi(object? sender, EventArgs e) => OpenExternalUrl(KoFiUrl);
    private void OnMenuPaypal(object? sender, EventArgs e) => OpenExternalUrl(PayPalUrl);



    private sealed record PreviewResult(Image? Image, string? Text, string? Message);
}
