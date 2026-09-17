using System.Drawing;
using System.Windows.Forms;
using DarkUI.Controls;
using DarkUI.Forms;
using PkgViewer.Core.Models;

namespace PkgViewer.Forms;

// UI construction for PackageViewerForm: control declarations, InitializeComponent and layout.
partial class PackageViewerForm
{
    private System.ComponentModel.IContainer? components;

    // Header bar
    private readonly DarkHeaderBar _headerPanel = new() { ShowThemeSelector = false };
    private readonly PictureBox _iconBox = new();
    private readonly DarkLabel _titleLabel = new();
    private readonly DarkLabel _subtitleLabel = new();
    private readonly DarkLabel _contentIdLabel = new();

    // Menu / status
    private readonly DarkMenuStrip _menu = new();
    private readonly DarkStatusStrip _statusStrip = new();
    private readonly ToolStripStatusLabel _statusPath = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly ToolStripStatusLabel _statusState = new();
    private readonly ToolStripStatusLabel _statusWarnings = new() { IsLink = true, Visible = false };
    private readonly DarkToolStripProgressBar _progressBar = new() { Visible = false, Style = ProgressBarStyle.Continuous };
    private readonly DarkToolStripButton _stopExtractButton = new("Stop Extract") { Visible = false };

    // Menu items whose enabled/checked state is updated from the session.
    private ToolStripMenuItem? _extractAllMenuItem;
    private ToolStripMenuItem? _retryAccessMenuItem;
    private ToolStripMenuItem? _previewPaneMenuItem;
    private ToolStripMenuItem? _exportMetadataMenuItem;

    // Tabs
    private readonly DarkTabControl _tabs = new();
    private readonly DarkTabPage _overviewTab = new() { Text = "Overview", Padding = new Padding(12) };
    private readonly DarkTabPage _packageTab = new() { Text = "PKG Internals", Padding = new Padding(12) };
    private readonly DarkTabPage _trophyTab = new() { Text = "Trophies", Padding = new Padding(12) };
    private readonly DarkTabPage _filesTab = new() { Text = "File Browser", Padding = new Padding(12) };
    private readonly DarkTabPage _artworkTab = new() { Text = "Artwork", Padding = new Padding(12) };

    // Overview
    private readonly DarkSectionPanel _overviewPanel = new() { SectionHeader = "Package Summary" };
    private readonly DarkSectionPanel _sfoPanel = new() { SectionHeader = "PARAM.SFO" };
    private readonly DarkSectionPanel _paramJsonPanel = new() { SectionHeader = "param.json" };
    private readonly DarkTreeView _paramJsonTree = new();
    private readonly Dictionary<string, DarkLabel> _overviewValues = new();
    private readonly DarkDataGridView _sfoGrid = new();
    private TableLayoutPanel? _overviewLayout;
    private TableLayoutPanel? _overviewSummaryTable;
    private readonly ToolTip _toolTip = new();

    // PKG Internals
    private readonly DarkTabControl _packageTabs = new();
    private readonly DarkTabPage _headerTab = new() { Text = "Header" };
    private readonly DarkTabPage _buildTab = new() { Text = "Build Info" };
    private readonly DarkTabPage _entriesTab = new() { Text = "Entries" };
    private readonly DarkDataGridView _headerGrid = new();
    private readonly DarkDataGridView _buildGrid = new();
    private readonly DarkDataGridView _entriesGrid = new();

    // Trophy
    private readonly DarkDataGridView _trophyGrid = new();
    private readonly DarkLabel _trophyState = new() { Text = "Trophy information loads when this page is selected." };
    private readonly DarkSearchBox _trophyFilter = new() { Placeholder = "Filter trophies by name, description or type" };

    // File browser
    private readonly DarkSearchBox _fileFilter = new() { Placeholder = "Filter filename here" };
    private readonly DarkTreeView _fileTree = new();
    private readonly DarkListView _fileList = new();
    private readonly ImageList _fileIcons = FileIcons.Create();
    private readonly DarkContextMenu _fileContextMenu = new();
    private readonly DarkLabel _fileBreadcrumb = new() { Text = "Package root" };
    private readonly DarkButton _upButton = new() { Text = "Up" };
    private readonly DarkButton _extractSelectedButton = new() { Text = "Extract Selected..." };
    private readonly DarkButton _extractAllButton = new() { Text = "Extract All..." };
    private DarkSplitContainer? _filesSplit;
    private readonly DarkSectionPanel _previewPanel = new() { SectionHeader = "File Preview" };
    private readonly DarkLabel _previewInfo = new() { Text = "Double-click a file to preview it." };
    private readonly Panel _previewBody = new() { BackColor = Color.FromArgb(20, 20, 20), Padding = new Padding(1) };
    private readonly PictureBox _previewImage = new()
    {
        SizeMode = PictureBoxSizeMode.Zoom,
        Visible = false,
        BackColor = Color.FromArgb(20, 20, 20)
    };
    private readonly DarkTextBox _previewText = new()
    {
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Both,
        WordWrap = false,
        BorderStyle = BorderStyle.None,
        Font = new Font("Consolas", 9F),
        Visible = false
    };

    // Artwork
    private readonly DarkSectionPanel _iconPanel = new() { SectionHeader = "ICON" };
    private readonly DarkSectionPanel _pic0Panel = new() { SectionHeader = "PIC0" };
    private readonly DarkSectionPanel _pic1Panel = new() { SectionHeader = "PIC1" };
    private readonly DarkSectionPanel _pic2Panel = new() { SectionHeader = "PIC2" };
    private readonly DarkLabel _iconEmpty = new() { Text = "No icon image in this package." };
    private readonly DarkLabel _pic0Empty = new() { Text = "No PIC0 image in this package." };
    private readonly DarkLabel _pic1Empty = new() { Text = "No PIC1 image in this package." };
    private readonly DarkLabel _pic2Empty = new() { Text = "No PIC2 image in this package." };
    private readonly PictureBox _iconArtBox = new() { SizeMode = PictureBoxSizeMode.Zoom, Visible = false };
    private readonly PictureBox _pic0Box = new() { SizeMode = PictureBoxSizeMode.Zoom, Visible = false };
    private readonly PictureBox _pic1Box = new() { SizeMode = PictureBoxSizeMode.Zoom, Visible = false };
    private readonly PictureBox _pic2Box = new() { SizeMode = PictureBoxSizeMode.Zoom, Visible = false };
    private TableLayoutPanel? _artworkLayout;

    private void InitializeComponent()
    {
        Text = "PkgViewer";
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = true;
        MinimumSize = new Size(860, 560);
        ClientSize = new Size(1100, 680);
        StartPosition = FormStartPosition.CenterScreen;

        BuildMenu();
        BuildHeader();
        BuildTabs();
        BuildStatus();
        EnableDragDrop();

        Controls.Add(_tabs);
        Controls.Add(_headerPanel);
        Controls.Add(_menu);
        Controls.Add(_statusStrip);

        Shown += async (_, _) =>
        {
            if (_shown) return;
            _shown = true;
            await LoadPackageAsync();
        };
        FormClosing += OnFormClosing;
        _tabs.SelectedIndexChanged += async (_, _) => await OnTabSelectedAsync();
    }

    // ------------------------------------------------------------------
    // Chrome (menu, header, status)
    // ------------------------------------------------------------------

    private void BuildMenu()
    {
        _menu.Dock = DockStyle.Top;
        _menu.Font = new Font("Segoe UI", 9F);

        var fileMenu = new ToolStripMenuItem("File");
        fileMenu.DropDownItems.Add(MenuItem("Open...", OpenAnotherPackage, Keys.Control | Keys.O));
        fileMenu.DropDownItems.Add(MenuItem("Close", Close));
        fileMenu.DropDownItems.Add(new DarkToolStripSeparator());
        _extractAllMenuItem = MenuItem("Extract All...", () => _ = ExtractFullAsync(), Keys.Control | Keys.Shift | Keys.E);
        fileMenu.DropDownItems.Add(_extractAllMenuItem);
        fileMenu.DropDownItems.Add(MenuItem("Save Artwork...", SaveArtwork));
        _exportMetadataMenuItem = MenuItem("Export Metadata...", ExportMetadata);
        fileMenu.DropDownItems.Add(_exportMetadataMenuItem);
        fileMenu.DropDownItems.Add(new DarkToolStripSeparator());
        fileMenu.DropDownItems.Add(MenuItem("Open Source Folder", OpenSourceFolder));
        fileMenu.DropDownItems.Add(MenuItem("Exit", Close));

        var editMenu = new ToolStripMenuItem("Edit");
        editMenu.DropDownItems.Add(MenuItem("Copy Title", () => CopyInfo("title")));
        editMenu.DropDownItems.Add(MenuItem("Copy Title ID", () => CopyInfo("title_id")));
        editMenu.DropDownItems.Add(MenuItem("Copy Content ID", () => CopyInfo("content_id")));
        editMenu.DropDownItems.Add(MenuItem("Copy Source Path", CopySourcePath));
        editMenu.DropDownItems.Add(new DarkToolStripSeparator());
        editMenu.DropDownItems.Add(MenuItem("Find Files", FocusFileSearch, Keys.Control | Keys.F));

        var viewMenu = new ToolStripMenuItem("View");
        viewMenu.DropDownItems.Add(MenuItem("Overview", () => SelectTab(_overviewTab)));
        viewMenu.DropDownItems.Add(MenuItem("Files", () => SelectTab(_filesTab)));
        viewMenu.DropDownItems.Add(MenuItem("Artwork", () => SelectTab(_artworkTab)));
        viewMenu.DropDownItems.Add(MenuItem("Trophies", () => SelectTab(_trophyTab)));
        viewMenu.DropDownItems.Add(MenuItem("Internals", () => SelectTab(_packageTab)));
        viewMenu.DropDownItems.Add(new DarkToolStripSeparator());
        _previewPaneMenuItem = MenuItem("Preview Pane", TogglePreviewPane);
        _previewPaneMenuItem.CheckOnClick = true;
        _previewPaneMenuItem.Checked = true;
        viewMenu.DropDownItems.Add(_previewPaneMenuItem);
        viewMenu.DropDownItems.Add(MenuItem("Reset Layout", ResetLayout));

        var toolsMenu = new ToolStripMenuItem("Tools");
        _retryAccessMenuItem = MenuItem("Retry Content Access...", () => _ = RetryContentAccessAsync());
        toolsMenu.DropDownItems.Add(_retryAccessMenuItem);
        toolsMenu.DropDownItems.Add(new DarkToolStripSeparator());
        toolsMenu.DropDownItems.Add(MenuItem("File Associations...", ShowAssociations));
        var advancedMenu = new ToolStripMenuItem("Advanced");
        advancedMenu.DropDownItems.Add(MenuItem("Remove legacy PS4/PS5 associations...", RemoveLegacyIntegration));
        toolsMenu.DropDownItems.Add(advancedMenu);

        var helpMenu = new ToolStripMenuItem("Help");
        helpMenu.DropDownItems.Add(MenuItem("Open Log Folder", OpenLogFolder));
        helpMenu.DropDownItems.Add(MenuItem("Copy Diagnostic Summary", CopyDiagnostics));
        helpMenu.DropDownItems.Add(new DarkToolStripSeparator());
        helpMenu.DropDownItems.Add(MenuItem("About", () => new AboutForm().ShowDialog(this)));
        helpMenu.DropDownItems.Add(MenuItem("Support development", OpenCoffeeLink));

        _menu.Items.Add(fileMenu);
        _menu.Items.Add(editMenu);
        _menu.Items.Add(viewMenu);
        _menu.Items.Add(toolsMenu);
        _menu.Items.Add(helpMenu);
        MainMenuStrip = _menu;
    }

    private void EnableDragDrop()
    {
        AllowDrop = true;
        _tabs.AllowDrop = true;
        DragEnter += OnDragEnterPackage;
        DragDrop += OnDragDropPackage;
        _tabs.DragEnter += OnDragEnterPackage;
        _tabs.DragDrop += OnDragDropPackage;
    }

    private void OnDragEnterPackage(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            e.Effect = DragDropEffects.Copy;
    }

    private void OnDragDropPackage(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files)
            OpenDroppedFile(files[0]);
    }

    private static ToolStripMenuItem MenuItem(string text, Action action, Keys shortcut = Keys.None)
    {
        var item = new ToolStripMenuItem(text) { ForeColor = Color.FromArgb(220, 220, 220) };
        if (shortcut != Keys.None)
        {
            item.ShortcutKeys = shortcut;
            item.ShowShortcutKeys = true;
        }
        item.Click += (_, _) => action();
        return item;
    }

    private void BuildHeader()
    {
        _headerPanel.Dock = DockStyle.Top;
        _headerPanel.Height = 104;
        _headerPanel.Padding = new Padding(16, 12, 16, 12);

        _iconBox.Location = new Point(16, 10);
        _iconBox.Size = new Size(84, 84);
        _iconBox.SizeMode = PictureBoxSizeMode.Zoom;
        _iconBox.TabStop = false;

        _titleLabel.Text = "PKG Viewer";
        _titleLabel.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
        _titleLabel.AutoEllipsis = true;
        _titleLabel.Cursor = Cursors.Hand;
        _titleLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _titleLabel.Location = new Point(118, 12);
        _titleLabel.Size = new Size(680, 27);
        _titleLabel.DoubleClick += (_, _) => CopyToClipboard(_titleLabel.Text, "Title");

        _subtitleLabel.Text = "Reading package metadata...";
        _subtitleLabel.AutoEllipsis = true;
        _subtitleLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _subtitleLabel.Location = new Point(118, 45);
        _subtitleLabel.Size = new Size(680, 17);

        _contentIdLabel.AutoEllipsis = true;
        _contentIdLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _contentIdLabel.Location = new Point(118, 66);
        _contentIdLabel.Size = new Size(680, 17);

        _headerPanel.Controls.Add(_contentIdLabel);
        _headerPanel.Controls.Add(_subtitleLabel);
        _headerPanel.Controls.Add(_titleLabel);
        _headerPanel.Controls.Add(_iconBox);
    }

    private void BuildStatus()
    {
        _statusStrip.Font = new Font("Segoe UI", 8.25F);
        _statusPath.Text = _currentPackagePath;
        _statusState.Text = "Starting...";
        _stopExtractButton.Click += (_, _) => StopExtraction();

        _statusWarnings.Click += (_, _) => ShowWarnings();
        _statusStrip.Items.Add(_statusPath);
        _statusStrip.Items.Add(_statusState);
        _statusStrip.Items.Add(_statusWarnings);
        _statusStrip.Items.Add(new DarkToolStripSeparator());
        _statusStrip.Items.Add(_progressBar);
        _statusStrip.Items.Add(new DarkToolStripSeparator());
        _statusStrip.Items.Add(_stopExtractButton);
    }

    private void BuildTabs()
    {
        _tabs.Dock = DockStyle.Fill;
        _tabs.ItemSize = new Size(108, 28);
        _tabs.Padding = new Point(0, 0);

        BuildOverviewTab();
        BuildPackageTab();
        BuildTrophyTab();
        BuildFilesTab();
        BuildArtworkTab();

        _tabs.TabPages.Add(_overviewTab);
        _tabs.TabPages.Add(_packageTab);
        _tabs.TabPages.Add(_trophyTab);
        _tabs.TabPages.Add(_filesTab);
        _tabs.TabPages.Add(_artworkTab);
    }

    // ------------------------------------------------------------------
    // Overview
    // ------------------------------------------------------------------

    private void BuildOverviewTab()
    {
        string[] captions =
        [
            "Title", "Title ID", "Content ID", "Category", "Package state",
            "Application version", "Package version", "Required firmware", "Package size"
        ];

        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 9 };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        for (int row = 0; row < 9; row++)
        {
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 11.11111F));

            var caption = new DarkLabel
            {
                Text = captions[row],
                Dock = DockStyle.Fill,
                Margin = new Padding(3),
                Font = new Font("Segoe UI", 9F),
                TextAlign = ContentAlignment.MiddleLeft
            };
            var value = new DarkLabel
            {
                Text = "Not available",
                Dock = DockStyle.Fill,
                Margin = new Padding(3),
                Font = row == 0 ? new Font("Segoe UI", 9F, FontStyle.Bold) : new Font("Segoe UI", 9F),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Cursor = Cursors.Hand
            };
            value.DoubleClick += (_, _) => CopyToClipboard(value.Text, "Value");
            _overviewValues[captions[row]] = value;
            table.Controls.Add(caption, 0, row);
            table.Controls.Add(value, 1, row);
        }
        _overviewSummaryTable = table;

        _overviewPanel.Dock = DockStyle.Fill;
        _overviewPanel.Margin = new Padding(0, 0, 6, 0);
        _overviewPanel.Padding = new Padding(16, 12, 16, 16);
        _overviewPanel.Controls.Add(table);

        SetupGrid(_sfoGrid);
        _sfoGrid.Columns.Add(TextColumn("Key", 35F));
        _sfoGrid.Columns.Add(TextColumn("Value", 65F));
        _sfoPanel.Dock = DockStyle.Fill;
        _sfoPanel.Margin = new Padding(6, 0, 0, 0);
        _sfoPanel.Controls.Add(_sfoGrid);

        _paramJsonPanel.Dock = DockStyle.Fill;
        _paramJsonPanel.Margin = new Padding(6, 0, 0, 0);
        _paramJsonTree.Dock = DockStyle.Fill;
        _paramJsonTree.ShowLines = true;
        _paramJsonTree.ShowPlusMinus = true;
        _paramJsonTree.ShowRootLines = true;
        _paramJsonPanel.Controls.Add(_paramJsonTree);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        layout.Controls.Add(_overviewPanel, 0, 0);
        layout.Controls.Add(_sfoPanel, 1, 0);
        _overviewLayout = layout;
        _overviewTab.Controls.Add(layout);
    }

    // ------------------------------------------------------------------
    // PKG Internals
    // ------------------------------------------------------------------

    private void BuildPackageTab()
    {
        _packageTabs.Dock = DockStyle.Fill;
        _packageTabs.ItemSize = new Size(88, 28);
        _packageTabs.Padding = new Point(0, 0);

        SetupGrid(_headerGrid);
        _headerGrid.Columns.Add(TextColumn("Field", 38F));
        _headerGrid.Columns.Add(TextColumn("Value", 62F));
        _headerTab.Controls.Add(_headerGrid);

        SetupGrid(_buildGrid);
        _buildGrid.Columns.Add(TextColumn("Field", 38F));
        _buildGrid.Columns.Add(TextColumn("Value", 62F));
        _buildTab.Controls.Add(_buildGrid);

        SetupGrid(_entriesGrid);
        _entriesGrid.Columns.Add(TextColumn("Name", 24F));
        _entriesGrid.Columns.Add(TextColumn("Offset", 17F));
        _entriesGrid.Columns.Add(TextColumn("Size", 17F));
        _entriesGrid.Columns.Add(TextColumn("Flags 1", 14F));
        _entriesGrid.Columns.Add(TextColumn("Flags 2", 14F));
        _entriesGrid.Columns.Add(TextColumn("Encrypted?", 14F));
        _entriesGrid.Columns["Offset"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        _entriesGrid.Columns["Size"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        _entriesGrid.ColumnHeaderMouseClick += OnEntriesColumnHeaderClick;
        AttachGridCopyMenu(_entriesGrid);
        AttachGridCopyMenu(_headerGrid);
        AttachGridCopyMenu(_buildGrid);
        AttachGridCopyMenu(_sfoGrid);
        AttachGridCopyMenu(_trophyGrid);
        _entriesTab.Controls.Add(_entriesGrid);

        _packageTabs.TabPages.Add(_headerTab);
        _packageTabs.TabPages.Add(_buildTab);
        _packageTabs.TabPages.Add(_entriesTab);
        _packageTab.Controls.Add(_packageTabs);
    }

    // ------------------------------------------------------------------
    // Trophy
    // ------------------------------------------------------------------

    private void BuildTrophyTab()
    {
        SetupGrid(_trophyGrid);
        _trophyGrid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        _trophyGrid.RowTemplate.Height = 52;
        _trophyGrid.Columns.Add(new DataGridViewImageColumn
        {
            HeaderText = "Icon",
            FillWeight = 10F,
            ImageLayout = DataGridViewImageCellLayout.Zoom,
            ReadOnly = true,
            Resizable = DataGridViewTriState.True
        });
        _trophyGrid.Columns.Add(TextColumn("ID", 8F));
        _trophyGrid.Columns.Add(TextColumn("Name", 23F));
        _trophyGrid.Columns.Add(TextColumn("Description", 39F));
        _trophyGrid.Columns.Add(TextColumn("Type", 12F));
        _trophyGrid.Columns.Add(TextColumn("Hidden", 8F));

        _trophyState.Dock = DockStyle.Top;
        _trophyState.Height = 30;
        _trophyState.Font = new Font("Segoe UI", 8.5F, FontStyle.Italic);
        _trophyState.Padding = new Padding(10, 7, 10, 7);
        _trophyState.TextAlign = ContentAlignment.MiddleLeft;

        _trophyFilter.Dock = DockStyle.Top;
        _trophyFilter.Height = 30;
        _trophyFilter.SearchTextChanged += (_, _) => ApplyTrophyFilter();

        _trophyTab.Controls.Add(_trophyGrid);
        _trophyTab.Controls.Add(_trophyState);
        _trophyTab.Controls.Add(_trophyFilter);
        _trophyState.BringToFront();
        _trophyFilter.BringToFront();
    }

    // ------------------------------------------------------------------
    // File Browser
    // ------------------------------------------------------------------

    private void BuildFilesTab()
    {
        _fileTree.CheckBoxes = false;
        _fileTree.FullRowSelect = false;
        _fileTree.HotTracking = false;
        _fileTree.LabelEdit = false;
        _fileTree.Indent = 19;
        _fileTree.ItemHeight = 24;
        _fileTree.ShowLines = true;
        _fileTree.ShowPlusMinus = true;
        _fileTree.ShowRootLines = true;
        _fileTree.AfterSelect += (_, _) => OnFileTreeNodeSelected();

        _fileList.View = View.Details;
        _fileList.FullRowSelect = true;
        _fileList.MultiSelect = true;
        _fileList.Columns.Add("Name", 220);
        _fileList.Columns.Add("Type", 100);
        _fileList.Columns.Add("Path", 260);
        _fileList.Columns.Add("Size", 90);
        _fileList.ItemActivate += (_, _) => ActivateFileListItem();
        _fileList.MouseClick += OnFileListMouseClick;
        _fileList.SelectedIndexChanged += (_, _) => UpdateFileActionState();
        _fileList.KeyDown += OnFileListKeyDown;

        _fileTree.ImageList = _fileIcons;
        _fileList.SmallImageList = _fileIcons;
        BuildFileContextMenu();

        _previewInfo.Dock = DockStyle.Top;
        _previewInfo.AutoEllipsis = true;
        _previewInfo.Height = 34;
        _previewInfo.Padding = new Padding(8, 0, 8, 0);
        _previewInfo.TextAlign = ContentAlignment.MiddleLeft;
        _previewImage.Dock = DockStyle.Fill;
        _previewText.Dock = DockStyle.Fill;
        _previewBody.Dock = DockStyle.Fill;
        _previewBody.Controls.Add(_previewImage);
        _previewBody.Controls.Add(_previewText);
        _previewPanel.Controls.Add(_previewBody);
        _previewPanel.Controls.Add(_previewInfo);
        _previewInfo.BringToFront();

        // Draggable three-pane split: folder tree | content list | file preview.
        _filesSplit = new DarkSplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = DarkSplitContainer.DarkSplitOrientation.Vertical,
            SplitterWidth = 6
        };
        _filesSplit.AddPanel(_fileTree);
        _filesSplit.AddPanel(_fileList);
        _filesSplit.AddPanel(_previewPanel);
        _filesSplit.PanelSizes = [280, 420, 380];

        _fileBreadcrumb.Dock = DockStyle.Fill;
        _fileBreadcrumb.AutoEllipsis = true;
        _fileBreadcrumb.TextAlign = ContentAlignment.MiddleLeft;
        _fileBreadcrumb.Padding = new Padding(6, 0, 6, 0);
        _upButton.Dock = DockStyle.Fill;
        _extractSelectedButton.Dock = DockStyle.Fill;
        _extractAllButton.Dock = DockStyle.Fill;
        _upButton.Margin = new Padding(2);
        _extractSelectedButton.Margin = new Padding(2);
        _extractAllButton.Margin = new Padding(2);
        _upButton.Click += (_, _) => NavigateUp();
        _extractSelectedButton.Click += (_, _) => _ = ExtractSelectedAsync();
        _extractAllButton.Click += (_, _) => _ = ExtractFullAsync();

        var toolbar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, Margin = new Padding(0) };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 56F));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 156F));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F));
        toolbar.Controls.Add(_fileBreadcrumb, 0, 0);
        toolbar.Controls.Add(_upButton, 1, 0);
        toolbar.Controls.Add(_extractSelectedButton, 2, 0);
        toolbar.Controls.Add(_extractAllButton, 3, 0);

        _fileFilter.Dock = DockStyle.Fill;
        _fileFilter.SearchTextChanged += (_, _) => RefreshFileList();

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.Controls.Add(toolbar, 0, 0);
        layout.Controls.Add(_fileFilter, 0, 1);
        layout.Controls.Add(_filesSplit, 0, 2);
        _filesTab.Controls.Add(layout);
    }

    // ------------------------------------------------------------------
    // Artwork
    // ------------------------------------------------------------------

    private void BuildArtworkTab()
    {
        BuildArtworkPanel(_iconPanel, _iconEmpty, _iconArtBox);
        BuildArtworkPanel(_pic0Panel, _pic0Empty, _pic0Box);
        BuildArtworkPanel(_pic1Panel, _pic1Empty, _pic1Box);
        BuildArtworkPanel(_pic2Panel, _pic2Empty, _pic2Box);
        AttachArtworkMenu(_iconArtBox, "ICON");
        AttachArtworkMenu(_pic0Box, "PIC0");
        AttachArtworkMenu(_pic1Box, "PIC1");
        AttachArtworkMenu(_pic2Box, "PIC2");
        _artworkTab.Controls.Add(BuildArtworkLayout(isPs4: true));
    }

    private void AttachArtworkMenu(PictureBox box, string slot)
    {
        var menu = new DarkContextMenu();
        menu.Items.Add(MenuItem("Save image...", () => SaveArtworkSlot(box, slot)));
        box.ContextMenuStrip = menu;
    }

    /// <summary>PS4 shows PIC0/PIC1; PS5 additionally shows the icon and PIC2 (all with placeholders).</summary>
    private TableLayoutPanel BuildArtworkLayout(bool isPs4)
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = isPs4 ? 1 : 2
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

        // Margins are assigned per layout: the reused panels must not keep the other layout's margins.
        if (isPs4)
        {
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            _pic0Panel.Margin = new Padding(0, 0, 8, 0);
            _pic1Panel.Margin = new Padding(8, 0, 0, 0);
            table.Controls.Add(_pic0Panel, 0, 0);
            table.Controls.Add(_pic1Panel, 1, 0);
        }
        else
        {
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            _iconPanel.Margin = new Padding(0, 0, 8, 8);
            _pic0Panel.Margin = new Padding(8, 0, 0, 8);
            _pic1Panel.Margin = new Padding(0, 8, 8, 0);
            _pic2Panel.Margin = new Padding(8, 8, 0, 0);
            table.Controls.Add(_iconPanel, 0, 0);
            table.Controls.Add(_pic0Panel, 1, 0);
            table.Controls.Add(_pic1Panel, 0, 1);
            table.Controls.Add(_pic2Panel, 1, 1);
        }

        _artworkLayout = table;
        return table;
    }

    private static void BuildArtworkPanel(DarkSectionPanel panel, DarkLabel empty, PictureBox box)
    {
        panel.Dock = DockStyle.Fill;
        panel.Padding = new Padding(12, 10, 12, 12);
        empty.Dock = DockStyle.Fill;
        empty.Font = new Font("Segoe UI", 9F, FontStyle.Italic);
        empty.TextAlign = ContentAlignment.MiddleCenter;
        box.Dock = DockStyle.Fill;
        box.TabStop = false;
        panel.Controls.Add(empty);
        panel.Controls.Add(box);
    }

    // ------------------------------------------------------------------
    // Grid helpers
    // ------------------------------------------------------------------

    private static void SetupGrid(DarkDataGridView grid)
    {
        grid.Dock = DockStyle.Fill;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToOrderColumns = true;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.MultiSelect = false;
        grid.ReadOnly = true;
        grid.RowHeadersVisible = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.BackgroundColor = Color.FromArgb(20, 20, 20);
    }

    private static DataGridViewTextBoxColumn TextColumn(string header, float fillWeight) => new()
    {
        Name = header,
        HeaderText = header,
        FillWeight = fillWeight,
        ReadOnly = true,
        Resizable = DataGridViewTriState.True
    };

    private void BuildFileContextMenu()
    {
        _fileContextMenu.Items.Add(MenuItem("Preview", ActivateFileListItem));
        _fileContextMenu.Items.Add(new DarkToolStripSeparator());
        _fileContextMenu.Items.Add(MenuItem("Extract...", () => _ = ExtractSelectedAsync()));
        _fileContextMenu.Items.Add(new DarkToolStripSeparator());
        _fileContextMenu.Items.Add(MenuItem("Open containing folder", OpenContainingFolder));
        _fileContextMenu.Items.Add(MenuItem("Copy path", CopySelectedPath));
        _fileContextMenu.Items.Add(MenuItem("Copy filename", CopySelectedName));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            components?.Dispose();
        }
        base.Dispose(disposing);
    }
}
