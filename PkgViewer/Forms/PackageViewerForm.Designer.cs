using System.Drawing;
using System.Windows.Forms;
using DarkUI.Controls;
using DarkUI.Forms;

namespace PkgViewer.Forms;

// VS-designer-compatible UI construction: every control is declared as a field and created/added
// directly inside InitializeComponent, with named event handlers defined in PackageViewerForm.cs.
partial class PackageViewerForm
{
    private System.ComponentModel.IContainer? components;

    // Header
    private DarkHeaderBar _headerPanel = null!;
    private PictureBox _iconBox = null!;
    private DarkLabel _titleLabel = null!;
    private DarkLabel _subtitleLabel = null!;
    private DarkLabel _contentIdLabel = null!;

    // Menu
    private DarkMenuStrip _menu = null!;
    private ToolStripMenuItem _fileMenu = null!;
    private ToolStripMenuItem _editMenu = null!;
    private ToolStripMenuItem _viewMenu = null!;
    private ToolStripMenuItem _toolsMenu = null!;
    private ToolStripMenuItem _helpMenu = null!;
    private ToolStripMenuItem _openMenuItem = null!;
    private ToolStripMenuItem _extractAllMenuItem = null!;
    private ToolStripMenuItem _saveArtworkMenuItem = null!;
    private ToolStripMenuItem _exportMetadataMenuItem = null!;
    private ToolStripMenuItem _openSourceFolderMenuItem = null!;
    private ToolStripMenuItem _exitMenuItem = null!;
    private ToolStripMenuItem _copyTitleMenuItem = null!;
    private ToolStripMenuItem _copyTitleIdMenuItem = null!;
    private ToolStripMenuItem _copyContentIdMenuItem = null!;
    private ToolStripMenuItem _copySourcePathMenuItem = null!;
    private ToolStripMenuItem _findFilesMenuItem = null!;
    private ToolStripMenuItem _viewOverviewMenuItem = null!;
    private ToolStripMenuItem _viewFilesMenuItem = null!;
    private ToolStripMenuItem _viewArtworkMenuItem = null!;
    private ToolStripMenuItem _viewTrophiesMenuItem = null!;
    private ToolStripMenuItem _viewInternalsMenuItem = null!;
    private ToolStripMenuItem _previewPaneMenuItem = null!;
    private ToolStripMenuItem _resetLayoutMenuItem = null!;
    private ToolStripMenuItem _retryAccessMenuItem = null!;
    private ToolStripMenuItem _fileAssociationsMenuItem = null!;
    private ToolStripMenuItem _advancedMenu = null!;
    private ToolStripMenuItem _removeLegacyMenuItem = null!;
    private ToolStripMenuItem _openLogFolderMenuItem = null!;
    private ToolStripMenuItem _copyDiagnosticsMenuItem = null!;
    private ToolStripMenuItem _aboutMenuItem = null!;
    private ToolStripMenuItem _kofiMenuItem = null!;
    private ToolStripMenuItem _paypalMenuItem = null!;
    private DarkToolStripSeparator _fileSeparator1 = null!;
    private DarkToolStripSeparator _fileSeparator2 = null!;
    private DarkToolStripSeparator _editSeparator = null!;
    private DarkToolStripSeparator _viewSeparator = null!;
    private DarkToolStripSeparator _toolsSeparator = null!;
    private DarkToolStripSeparator _helpSeparator = null!;

    // Status
    private DarkStatusStrip _statusStrip = null!;
    private DarkToolStripStatusLabel _statusPath = null!;
    private DarkToolStripStatusLabel _statusState = null!;
    private DarkToolStripStatusLabel _statusWarnings = null!;
    private DarkToolStripSeparator _statusSeparator1 = null!;
    private DarkToolStripSeparator _statusSeparator2 = null!;
    private DarkToolStripProgressBar _progressBar = null!;
    private DarkToolStripButton _stopExtractButton = null!;

    // Tabs
    private DarkTabControl _tabs = null!;
    private DarkTabPage _overviewTab = null!;
    private DarkTabPage _packageTab = null!;
    private DarkTabPage _trophyTab = null!;
    private DarkTabPage _filesTab = null!;
    private DarkTabPage _artworkTab = null!;

    // Overview
    private TableLayoutPanel _overviewLayout = null!;
    private DarkSectionPanel _overviewPanel = null!;
    private Panel _overviewSummaryHost = null!;
    private TableLayoutPanel _overviewSummaryTable = null!;
    private DarkSectionPanel _sfoPanel = null!;
    private DarkDataGridView _sfoGrid = null!;
    private DataGridViewTextBoxColumn _sfoKeyColumn = null!;
    private DataGridViewTextBoxColumn _sfoValueColumn = null!;
    private DarkSectionPanel _paramJsonPanel = null!;
    private DarkTreeView _paramJsonTree = null!;
    private Dictionary<string, DarkLabel> _overviewValues = null!;
    private ToolTip _toolTip = null!;

    // PKG Internals
    private DarkTabControl _packageTabs = null!;
    private DarkTabPage _headerTab = null!;
    private DarkTabPage _buildTab = null!;
    private DarkTabPage _entriesTab = null!;
    private DarkDataGridView _headerGrid = null!;
    private DataGridViewTextBoxColumn _headerFieldColumn = null!;
    private DataGridViewTextBoxColumn _headerValueColumn = null!;
    private DarkDataGridView _buildGrid = null!;
    private DataGridViewTextBoxColumn _buildFieldColumn = null!;
    private DataGridViewTextBoxColumn _buildValueColumn = null!;
    private DarkDataGridView _entriesGrid = null!;
    private DataGridViewTextBoxColumn _entriesNameColumn = null!;
    private DataGridViewTextBoxColumn _entriesOffsetColumn = null!;
    private DataGridViewTextBoxColumn _entriesSizeColumn = null!;
    private DataGridViewTextBoxColumn _entriesFlags1Column = null!;
    private DataGridViewTextBoxColumn _entriesFlags2Column = null!;
    private DataGridViewTextBoxColumn _entriesEncryptedColumn = null!;

    // Trophy
    private DarkSearchBox _trophyFilter = null!;
    private DarkLabel _trophyState = null!;
    private DarkDataGridView _trophyGrid = null!;
    private DataGridViewImageColumn _trophyIconColumn = null!;
    private DataGridViewTextBoxColumn _trophyIdColumn = null!;
    private DataGridViewTextBoxColumn _trophyNameColumn = null!;
    private DataGridViewTextBoxColumn _trophyDescriptionColumn = null!;
    private DataGridViewTextBoxColumn _trophyTypeColumn = null!;
    private DataGridViewTextBoxColumn _trophyHiddenColumn = null!;

    // File browser
    private TableLayoutPanel _filesLayout = null!;
    private TableLayoutPanel _filesToolbar = null!;
    private DarkLabel _fileBreadcrumb = null!;
    private DarkButton _upButton = null!;
    private DarkButton _extractSelectedButton = null!;
    private DarkButton _extractAllButton = null!;
    private DarkSearchBox _fileFilter = null!;
    private DarkSplitContainer _filesSplit = null!;
    private DarkSplitPane _filesSplitPane1 = null!;
    private DarkSplitPane _filesSplitPane2 = null!;
    private DarkSplitPane _filesSplitPane3 = null!;
    private DarkTreeView _fileTree = null!;
    private DarkListView _fileList = null!;
    private ImageList _fileIcons = null!;
    private DarkContextMenu _fileContextMenu = null!;
    private ToolStripMenuItem _filePreviewMenuItem = null!;
    private ToolStripMenuItem _fileExtractMenuItem = null!;
    private ToolStripMenuItem _fileOpenContainingMenuItem = null!;
    private ToolStripMenuItem _fileCopyPathMenuItem = null!;
    private ToolStripMenuItem _fileCopyNameMenuItem = null!;
    private DarkContextMenu _fileTreeContextMenu = null!;
    private ToolStripMenuItem _treePreviewMenuItem = null!;
    private ToolStripMenuItem _treeExtractMenuItem = null!;
    private ToolStripMenuItem _treeExpandMenuItem = null!;
    private ToolStripMenuItem _treeCollapseMenuItem = null!;
    private ToolStripMenuItem _treeExpandAllMenuItem = null!;
    private ToolStripMenuItem _treeCollapseAllMenuItem = null!;
    private ToolStripMenuItem _treeCopyPathMenuItem = null!;
    private ToolStripMenuItem _treeCopyNameMenuItem = null!;
    private DarkContextMenu _gridContextMenu = null!;
    private ToolStripMenuItem _gridCopyValueMenuItem = null!;
    private ToolStripMenuItem _gridCopyRowMenuItem = null!;
    private DarkSectionPanel _previewPanel = null!;
    private DarkLabel _previewInfo = null!;
    private Panel _previewBody = null!;
    private PictureBox _previewImage = null!;
    private DarkTextBox _previewText = null!;

    // Artwork
    private TableLayoutPanel _artworkLayout = null!;
    private DarkSectionPanel _iconPanel = null!;
    private DarkSectionPanel _pic0Panel = null!;
    private DarkSectionPanel _pic1Panel = null!;
    private DarkSectionPanel _pic2Panel = null!;
    private DarkLabel _iconEmpty = null!;
    private DarkLabel _pic0Empty = null!;
    private DarkLabel _pic1Empty = null!;
    private DarkLabel _pic2Empty = null!;
    private PictureBox _iconArtBox = null!;
    private PictureBox _pic0Box = null!;
    private PictureBox _pic1Box = null!;
    private PictureBox _pic2Box = null!;
    private DarkContextMenu _iconArtMenu = null!;
    private DarkContextMenu _pic0ArtMenu = null!;
    private DarkContextMenu _pic1ArtMenu = null!;
    private DarkContextMenu _pic2ArtMenu = null!;
    private ToolStripMenuItem _iconArtSaveMenuItem = null!;
    private ToolStripMenuItem _pic0ArtSaveMenuItem = null!;
    private ToolStripMenuItem _pic1ArtSaveMenuItem = null!;
    private ToolStripMenuItem _pic2ArtSaveMenuItem = null!;

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        _toolTip = new ToolTip(components);

        // ---------------- Header ----------------
        _headerPanel = new DarkHeaderBar();
        _iconBox = new PictureBox();
        _titleLabel = new DarkLabel();
        _subtitleLabel = new DarkLabel();
        _contentIdLabel = new DarkLabel();
        _headerPanel.SuspendLayout();
        SuspendLayout();

        _headerPanel.ShowThemeSelector = false;
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
        _titleLabel.DoubleClick += OnTitleDoubleClick;

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

        // ---------------- Menu ----------------
        _menu = new DarkMenuStrip();
        _fileMenu = new ToolStripMenuItem();
        _openMenuItem = new ToolStripMenuItem();
        _fileSeparator1 = new DarkToolStripSeparator();
        _extractAllMenuItem = new ToolStripMenuItem();
        _saveArtworkMenuItem = new ToolStripMenuItem();
        _exportMetadataMenuItem = new ToolStripMenuItem();
        _fileSeparator2 = new DarkToolStripSeparator();
        _openSourceFolderMenuItem = new ToolStripMenuItem();
        _exitMenuItem = new ToolStripMenuItem();
        _editMenu = new ToolStripMenuItem();
        _copyTitleMenuItem = new ToolStripMenuItem();
        _copyTitleIdMenuItem = new ToolStripMenuItem();
        _copyContentIdMenuItem = new ToolStripMenuItem();
        _copySourcePathMenuItem = new ToolStripMenuItem();
        _editSeparator = new DarkToolStripSeparator();
        _findFilesMenuItem = new ToolStripMenuItem();
        _viewMenu = new ToolStripMenuItem();
        _viewOverviewMenuItem = new ToolStripMenuItem();
        _viewFilesMenuItem = new ToolStripMenuItem();
        _viewArtworkMenuItem = new ToolStripMenuItem();
        _viewTrophiesMenuItem = new ToolStripMenuItem();
        _viewInternalsMenuItem = new ToolStripMenuItem();
        _viewSeparator = new DarkToolStripSeparator();
        _previewPaneMenuItem = new ToolStripMenuItem();
        _resetLayoutMenuItem = new ToolStripMenuItem();
        _toolsMenu = new ToolStripMenuItem();
        _retryAccessMenuItem = new ToolStripMenuItem();
        _toolsSeparator = new DarkToolStripSeparator();
        _fileAssociationsMenuItem = new ToolStripMenuItem();
        _advancedMenu = new ToolStripMenuItem();
        _removeLegacyMenuItem = new ToolStripMenuItem();
        _helpMenu = new ToolStripMenuItem();
        _openLogFolderMenuItem = new ToolStripMenuItem();
        _copyDiagnosticsMenuItem = new ToolStripMenuItem();
        _helpSeparator = new DarkToolStripSeparator();
        _aboutMenuItem = new ToolStripMenuItem();
        _kofiMenuItem = new ToolStripMenuItem();
        _paypalMenuItem = new ToolStripMenuItem();
        _menu.SuspendLayout();

        _menu.Dock = DockStyle.Top;
        _menu.Font = new Font("Segoe UI", 9F);

        _fileMenu.Text = "File";
        ConfigureMenuItem(_openMenuItem, "Open...", Keys.Control | Keys.O, OnMenuOpen);
        ConfigureMenuItem(_extractAllMenuItem, "Extract All...", Keys.Control | Keys.Shift | Keys.E, OnMenuExtractAll);
        ConfigureMenuItem(_saveArtworkMenuItem, "Save Artwork...", Keys.None, OnMenuSaveArtwork);
        ConfigureMenuItem(_exportMetadataMenuItem, "Export Metadata...", Keys.None, OnMenuExportMetadata);
        ConfigureMenuItem(_openSourceFolderMenuItem, "Open Source Folder", Keys.None, OnMenuOpenSourceFolder);
        ConfigureMenuItem(_exitMenuItem, "Exit", Keys.None, OnMenuExit);
        _fileMenu.DropDownItems.AddRange(new ToolStripItem[]
        {
            _openMenuItem, _fileSeparator1, _extractAllMenuItem, _saveArtworkMenuItem,
            _exportMetadataMenuItem, _fileSeparator2, _openSourceFolderMenuItem, _exitMenuItem
        });

        _editMenu.Text = "Edit";
        ConfigureMenuItem(_copyTitleMenuItem, "Copy Title", Keys.None, OnMenuCopyTitle);
        ConfigureMenuItem(_copyTitleIdMenuItem, "Copy Title ID", Keys.None, OnMenuCopyTitleId);
        ConfigureMenuItem(_copyContentIdMenuItem, "Copy Content ID", Keys.None, OnMenuCopyContentId);
        ConfigureMenuItem(_copySourcePathMenuItem, "Copy Source Path", Keys.None, OnMenuCopySourcePath);
        ConfigureMenuItem(_findFilesMenuItem, "Find Files", Keys.Control | Keys.F, OnMenuFindFiles);
        _editMenu.DropDownItems.AddRange(new ToolStripItem[]
        {
            _copyTitleMenuItem, _copyTitleIdMenuItem, _copyContentIdMenuItem, _copySourcePathMenuItem,
            _editSeparator, _findFilesMenuItem
        });

        _viewMenu.Text = "View";
        ConfigureMenuItem(_viewOverviewMenuItem, "Overview", Keys.None, OnMenuViewOverview);
        ConfigureMenuItem(_viewFilesMenuItem, "Files", Keys.None, OnMenuViewFiles);
        ConfigureMenuItem(_viewArtworkMenuItem, "Artwork", Keys.None, OnMenuViewArtwork);
        ConfigureMenuItem(_viewTrophiesMenuItem, "Trophies", Keys.None, OnMenuViewTrophies);
        ConfigureMenuItem(_viewInternalsMenuItem, "Internals", Keys.None, OnMenuViewInternals);
        ConfigureMenuItem(_previewPaneMenuItem, "Preview Pane", Keys.None, OnMenuPreviewPane);
        _previewPaneMenuItem.CheckOnClick = true;
        _previewPaneMenuItem.Checked = true;
        ConfigureMenuItem(_resetLayoutMenuItem, "Reset Layout", Keys.None, OnMenuResetLayout);
        _viewMenu.DropDownItems.AddRange(new ToolStripItem[]
        {
            _viewOverviewMenuItem, _viewFilesMenuItem, _viewArtworkMenuItem, _viewTrophiesMenuItem,
            _viewInternalsMenuItem, _viewSeparator, _previewPaneMenuItem, _resetLayoutMenuItem
        });

        _toolsMenu.Text = "Tools";
        ConfigureMenuItem(_retryAccessMenuItem, "Retry Content Access...", Keys.None, OnMenuRetryAccess);
        ConfigureMenuItem(_fileAssociationsMenuItem, "File Associations...", Keys.None, OnMenuFileAssociations);
        _advancedMenu.Text = "Advanced";
        ConfigureMenuItem(_removeLegacyMenuItem, "Remove legacy PS4/PS5 associations...", Keys.None, OnMenuRemoveLegacy);
        _advancedMenu.DropDownItems.Add(_removeLegacyMenuItem);
        _toolsMenu.DropDownItems.AddRange(new ToolStripItem[]
        {
            _retryAccessMenuItem, _toolsSeparator, _fileAssociationsMenuItem, _advancedMenu
        });

        _helpMenu.Text = "Help";
        ConfigureMenuItem(_openLogFolderMenuItem, "Open Log Folder", Keys.None, OnMenuOpenLogFolder);
        ConfigureMenuItem(_copyDiagnosticsMenuItem, "Copy Diagnostic Summary", Keys.None, OnMenuCopyDiagnostics);
        ConfigureMenuItem(_aboutMenuItem, "About", Keys.None, OnMenuAbout);
        ConfigureMenuItem(_kofiMenuItem, "Buy me a Ko-fi", Keys.None, OnMenuKofi);
        ConfigureMenuItem(_paypalMenuItem, "Support via PayPal", Keys.None, OnMenuPaypal);
        _helpMenu.DropDownItems.AddRange(new ToolStripItem[]
        {
            _openLogFolderMenuItem, _copyDiagnosticsMenuItem, _helpSeparator,
            _aboutMenuItem, _kofiMenuItem, _paypalMenuItem
        });

        _menu.Items.AddRange(new ToolStripItem[] { _fileMenu, _editMenu, _viewMenu, _toolsMenu, _helpMenu });

        // ---------------- Status ----------------
        _statusStrip = new DarkStatusStrip();
        _statusPath = new DarkToolStripStatusLabel();
        _statusState = new DarkToolStripStatusLabel();
        _statusWarnings = new DarkToolStripStatusLabel();
        _statusSeparator1 = new DarkToolStripSeparator();
        _statusSeparator2 = new DarkToolStripSeparator();
        _progressBar = new DarkToolStripProgressBar();
        _stopExtractButton = new DarkToolStripButton();
        _statusStrip.SuspendLayout();

        _statusStrip.Font = new Font("Segoe UI", 8.25F);
        _statusStrip.Padding = new Padding(0, 4, 0, 4);
        _statusStrip.Dock = DockStyle.Bottom;
        _statusStrip.Height = 30;
        _statusPath.Spring = true;
        _statusPath.TextAlign = ContentAlignment.MiddleLeft;
        _statusPath.Text = string.Empty;
        _statusState.Text = "Starting...";
        _statusWarnings.IsLink = true;
        _statusWarnings.Visible = false;
        _statusWarnings.Click += OnStatusWarningsClick;
        _statusSeparator1.Visible = false;
        _statusSeparator2.Visible = false;
        _progressBar.Visible = false;
        _progressBar.Style = ProgressBarStyle.Continuous;
        _stopExtractButton.Text = "Stop Extract";
        _stopExtractButton.Visible = false;
        _stopExtractButton.Click += OnStopExtractClick;
        _statusStrip.Items.AddRange(new ToolStripItem[]
        {
            _statusPath, _statusState, _statusWarnings, _statusSeparator1, _progressBar, _statusSeparator2, _stopExtractButton
        });

        // ---------------- Tabs ----------------
        _tabs = new DarkTabControl();
        _overviewTab = new DarkTabPage();
        _packageTab = new DarkTabPage();
        _trophyTab = new DarkTabPage();
        _filesTab = new DarkTabPage();
        _artworkTab = new DarkTabPage();
        _tabs.SuspendLayout();
        _overviewTab.SuspendLayout();
        _packageTab.SuspendLayout();
        _trophyTab.SuspendLayout();
        _filesTab.SuspendLayout();
        _artworkTab.SuspendLayout();

        _tabs.Dock = DockStyle.Fill;
        _tabs.ItemSize = new Size(108, 28);
        _tabs.Padding = new Point(0, 0);

        _overviewTab.Text = "Overview";
        _overviewTab.Padding = new Padding(12);
        BuildOverviewTab();
        _packageTab.Text = "PKG Internals";
        _packageTab.Padding = new Padding(12);
        BuildPackageTab();
        _trophyTab.Text = "Trophies";
        _trophyTab.Padding = new Padding(12);
        BuildTrophyTab();
        _filesTab.Text = "File Browser";
        _filesTab.Padding = new Padding(12);
        BuildFilesTab();
        _artworkTab.Text = "Artwork";
        _artworkTab.Padding = new Padding(12);
        BuildArtworkTab();

        _tabs.TabPages.AddRange(new TabPage[] { _overviewTab, _packageTab, _trophyTab, _filesTab, _artworkTab });
        _tabs.SelectedIndexChanged += OnTabsSelectedIndexChanged;

        // ---------------- Form ----------------
        AutoScaleMode = AutoScaleMode.Font;
        Text = "Pkg Viewer";
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = true;
        MinimumSize = new Size(860, 560);
        ClientSize = new Size(1100, 680);
        StartPosition = FormStartPosition.CenterScreen;
        AllowDrop = true;
        _tabs.AllowDrop = true;
        DragEnter += OnDragEnterPackage;
        DragDrop += OnDragDropPackage;
        _tabs.DragEnter += OnDragEnterPackage;
        _tabs.DragDrop += OnDragDropPackage;
        Shown += OnFormShown;
        FormClosing += OnFormClosing;
        MainMenuStrip = _menu;

        Controls.Add(_tabs);
        Controls.Add(_headerPanel);
        Controls.Add(_menu);
        Controls.Add(_statusStrip);

        _overviewTab.ResumeLayout(false);
        _overviewTab.PerformLayout();
        _packageTab.ResumeLayout(false);
        _packageTab.PerformLayout();
        _trophyTab.ResumeLayout(false);
        _trophyTab.PerformLayout();
        _filesTab.ResumeLayout(false);
        _filesTab.PerformLayout();
        _artworkTab.ResumeLayout(false);
        _artworkTab.PerformLayout();
        _tabs.ResumeLayout(false);
        _headerPanel.ResumeLayout(false);
        _statusStrip.ResumeLayout(false);
        _menu.ResumeLayout(false);
        _menu.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }

    private static void ConfigureMenuItem(ToolStripMenuItem item, string text, Keys shortcut, EventHandler handler)
    {
        item.Text = text;
        item.ForeColor = Color.FromArgb(220, 220, 220);
        if (shortcut != Keys.None)
        {
            item.ShortcutKeys = shortcut;
            item.ShowShortcutKeys = true;
        }
        item.Click += handler;
    }

    // ------------------------------------------------------------------
    // Overview
    // ------------------------------------------------------------------

    private void BuildOverviewTab()
    {
        _overviewLayout = new TableLayoutPanel();
        _overviewPanel = new DarkSectionPanel();
        _overviewSummaryHost = new Panel();
        _overviewSummaryTable = new TableLayoutPanel();
        _sfoPanel = new DarkSectionPanel();
        _sfoGrid = new DarkDataGridView();
        _sfoKeyColumn = new DataGridViewTextBoxColumn();
        _sfoValueColumn = new DataGridViewTextBoxColumn();
        _paramJsonPanel = new DarkSectionPanel();
        _paramJsonTree = new DarkTreeView();
        _overviewSummaryHost.SuspendLayout();
        _overviewPanel.SuspendLayout();
        _sfoPanel.SuspendLayout();
        _paramJsonPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_sfoGrid).BeginInit();
        _overviewLayout.SuspendLayout();

        _overviewSummaryTable.Dock = DockStyle.Top;
        _overviewSummaryTable.ColumnCount = 2;
        _overviewSummaryTable.RowCount = 0;
        _overviewSummaryTable.AutoSize = true;
        _overviewSummaryTable.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _overviewSummaryTable.Margin = new Padding(0);
        _overviewSummaryTable.Padding = new Padding(0);
        _overviewSummaryTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140F));
        _overviewSummaryTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _overviewSummaryHost.Dock = DockStyle.Fill;
        _overviewSummaryHost.AutoScroll = true;
        _overviewSummaryHost.Padding = new Padding(0);
        _overviewSummaryHost.Controls.Add(_overviewSummaryTable);

        _overviewPanel.SectionHeader = "Package Summary";
        _overviewPanel.Dock = DockStyle.Fill;
        _overviewPanel.Margin = new Padding(0, 0, 6, 0);
        _overviewPanel.Padding = new Padding(16, 12, 16, 16);
        _overviewPanel.Controls.Add(_overviewSummaryHost);

        SetupGrid(_sfoGrid);
        _sfoKeyColumn.Name = "Key";
        _sfoKeyColumn.HeaderText = "Key";
        _sfoKeyColumn.FillWeight = 35F;
        _sfoKeyColumn.ReadOnly = true;
        _sfoValueColumn.Name = "Value";
        _sfoValueColumn.HeaderText = "Value";
        _sfoValueColumn.FillWeight = 65F;
        _sfoValueColumn.ReadOnly = true;
        _sfoGrid.Columns.Add(_sfoKeyColumn);
        _sfoGrid.Columns.Add(_sfoValueColumn);
        _sfoPanel.SectionHeader = "PARAM.SFO";
        _sfoPanel.Dock = DockStyle.Fill;
        _sfoPanel.Margin = new Padding(6, 0, 0, 0);
        _sfoPanel.Controls.Add(_sfoGrid);

        _paramJsonPanel.SectionHeader = "param.json";
        _paramJsonPanel.Dock = DockStyle.Fill;
        _paramJsonPanel.Margin = new Padding(6, 0, 0, 0);
        _paramJsonTree.Dock = DockStyle.Fill;
        _paramJsonTree.ShowLines = true;
        _paramJsonTree.ShowPlusMinus = true;
        _paramJsonTree.ShowRootLines = true;
        _paramJsonPanel.Controls.Add(_paramJsonTree);

        _overviewLayout.Dock = DockStyle.Fill;
        _overviewLayout.ColumnCount = 2;
        _overviewLayout.RowCount = 1;
        _overviewLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        _overviewLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        _overviewLayout.Controls.Add(_overviewPanel, 0, 0);
        _overviewLayout.Controls.Add(_sfoPanel, 1, 0);

        _overviewValues = new Dictionary<string, DarkLabel>(StringComparer.Ordinal);
        _overviewTab.Controls.Add(_overviewLayout);

        _overviewLayout.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_sfoGrid).EndInit();
        _paramJsonPanel.ResumeLayout(false);
        _sfoPanel.ResumeLayout(false);
        _overviewPanel.ResumeLayout(false);
        _overviewSummaryHost.ResumeLayout(false);
    }

    /// <summary>Adds one auto-sized caption/value row to the summary table and returns the value label.</summary>
    private DarkLabel AddOverviewRow(TableLayoutPanel table, string caption, bool bold = false, bool track = true)
    {
        int index = table.RowCount;
        table.RowCount = index + 1;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var captionLabel = new DarkLabel
        {
            Text = caption,
            AutoSize = false,
            Height = 22,
            Dock = DockStyle.Fill,
            Margin = new Padding(3),
            TextAlign = ContentAlignment.MiddleLeft
        };
        var value = new DarkLabel
        {
            Text = "Not available",
            AutoSize = false,
            Height = 22,
            Dock = DockStyle.Fill,
            Margin = new Padding(3),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            Cursor = Cursors.Hand,
            Font = bold ? new Font("Segoe UI", 9F, FontStyle.Bold) : new Font("Segoe UI", 9F)
        };
        value.DoubleClick += OnOverviewValueDoubleClick;
        if (track) _overviewValues[caption] = value;

        table.Controls.Add(captionLabel, 0, index);
        table.Controls.Add(value, 1, index);
        return value;
    }

    // ------------------------------------------------------------------
    // PKG Internals
    // ------------------------------------------------------------------

    private void BuildPackageTab()
    {
        _packageTabs = new DarkTabControl();
        _headerTab = new DarkTabPage();
        _buildTab = new DarkTabPage();
        _entriesTab = new DarkTabPage();
        _headerGrid = new DarkDataGridView();
        _headerFieldColumn = new DataGridViewTextBoxColumn();
        _headerValueColumn = new DataGridViewTextBoxColumn();
        _buildGrid = new DarkDataGridView();
        _buildFieldColumn = new DataGridViewTextBoxColumn();
        _buildValueColumn = new DataGridViewTextBoxColumn();
        _entriesGrid = new DarkDataGridView();
        _entriesNameColumn = new DataGridViewTextBoxColumn();
        _entriesOffsetColumn = new DataGridViewTextBoxColumn();
        _entriesSizeColumn = new DataGridViewTextBoxColumn();
        _entriesFlags1Column = new DataGridViewTextBoxColumn();
        _entriesFlags2Column = new DataGridViewTextBoxColumn();
        _entriesEncryptedColumn = new DataGridViewTextBoxColumn();
        _packageTabs.SuspendLayout();
        _headerTab.SuspendLayout();
        _buildTab.SuspendLayout();
        _entriesTab.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_headerGrid).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_buildGrid).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_entriesGrid).BeginInit();

        _packageTabs.Dock = DockStyle.Fill;
        _packageTabs.ItemSize = new Size(88, 28);
        _packageTabs.Padding = new Point(0, 0);

        _headerTab.Text = "Header";
        SetupGrid(_headerGrid);
        _headerFieldColumn.Name = "Field";
        _headerFieldColumn.HeaderText = "Field";
        _headerFieldColumn.FillWeight = 38F;
        _headerFieldColumn.ReadOnly = true;
        _headerValueColumn.Name = "Value";
        _headerValueColumn.HeaderText = "Value";
        _headerValueColumn.FillWeight = 62F;
        _headerValueColumn.ReadOnly = true;
        _headerGrid.Columns.Add(_headerFieldColumn);
        _headerGrid.Columns.Add(_headerValueColumn);
        AttachGridCopyMenu(_headerGrid);
        _headerTab.Controls.Add(_headerGrid);

        _buildTab.Text = "Build Info";
        SetupGrid(_buildGrid);
        _buildFieldColumn.Name = "Field";
        _buildFieldColumn.HeaderText = "Field";
        _buildFieldColumn.FillWeight = 38F;
        _buildFieldColumn.ReadOnly = true;
        _buildValueColumn.Name = "Value";
        _buildValueColumn.HeaderText = "Value";
        _buildValueColumn.FillWeight = 62F;
        _buildValueColumn.ReadOnly = true;
        _buildGrid.Columns.Add(_buildFieldColumn);
        _buildGrid.Columns.Add(_buildValueColumn);
        AttachGridCopyMenu(_buildGrid);
        _buildTab.Controls.Add(_buildGrid);

        _entriesTab.Text = "Entries";
        SetupGrid(_entriesGrid);
        _entriesNameColumn.Name = "Name";
        _entriesNameColumn.HeaderText = "Name";
        _entriesNameColumn.FillWeight = 24F;
        _entriesNameColumn.ReadOnly = true;
        _entriesOffsetColumn.Name = "Offset";
        _entriesOffsetColumn.HeaderText = "Offset";
        _entriesOffsetColumn.FillWeight = 17F;
        _entriesOffsetColumn.ReadOnly = true;
        _entriesOffsetColumn.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        _entriesSizeColumn.Name = "Size";
        _entriesSizeColumn.HeaderText = "Size";
        _entriesSizeColumn.FillWeight = 17F;
        _entriesSizeColumn.ReadOnly = true;
        _entriesSizeColumn.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        _entriesFlags1Column.Name = "Flags 1";
        _entriesFlags1Column.HeaderText = "Flags 1";
        _entriesFlags1Column.FillWeight = 14F;
        _entriesFlags1Column.ReadOnly = true;
        _entriesFlags2Column.Name = "Flags 2";
        _entriesFlags2Column.HeaderText = "Flags 2";
        _entriesFlags2Column.FillWeight = 14F;
        _entriesFlags2Column.ReadOnly = true;
        _entriesEncryptedColumn.Name = "Encrypted?";
        _entriesEncryptedColumn.HeaderText = "Encrypted?";
        _entriesEncryptedColumn.FillWeight = 14F;
        _entriesEncryptedColumn.ReadOnly = true;
        _entriesGrid.Columns.Add(_entriesNameColumn);
        _entriesGrid.Columns.Add(_entriesOffsetColumn);
        _entriesGrid.Columns.Add(_entriesSizeColumn);
        _entriesGrid.Columns.Add(_entriesFlags1Column);
        _entriesGrid.Columns.Add(_entriesFlags2Column);
        _entriesGrid.Columns.Add(_entriesEncryptedColumn);
        _entriesGrid.ColumnHeaderMouseClick += OnEntriesColumnHeaderClick;
        AttachGridCopyMenu(_entriesGrid);
        _entriesTab.Controls.Add(_entriesGrid);

        AttachGridCopyMenu(_sfoGrid);

        _packageTabs.TabPages.Add(_headerTab);
        _packageTabs.TabPages.Add(_buildTab);
        _packageTabs.TabPages.Add(_entriesTab);
        _packageTab.Controls.Add(_packageTabs);

        ((System.ComponentModel.ISupportInitialize)_entriesGrid).EndInit();
        ((System.ComponentModel.ISupportInitialize)_buildGrid).EndInit();
        ((System.ComponentModel.ISupportInitialize)_headerGrid).EndInit();
        _entriesTab.ResumeLayout(false);
        _buildTab.ResumeLayout(false);
        _headerTab.ResumeLayout(false);
        _packageTabs.ResumeLayout(false);
    }

    // ------------------------------------------------------------------
    // Trophy
    // ------------------------------------------------------------------

    private void BuildTrophyTab()
    {
        _trophyGrid = new DarkDataGridView();
        _trophyIconColumn = new DataGridViewImageColumn();
        _trophyIdColumn = new DataGridViewTextBoxColumn();
        _trophyNameColumn = new DataGridViewTextBoxColumn();
        _trophyDescriptionColumn = new DataGridViewTextBoxColumn();
        _trophyTypeColumn = new DataGridViewTextBoxColumn();
        _trophyHiddenColumn = new DataGridViewTextBoxColumn();
        _trophyState = new DarkLabel();
        _trophyFilter = new DarkSearchBox();
        ((System.ComponentModel.ISupportInitialize)_trophyGrid).BeginInit();

        SetupGrid(_trophyGrid);
        _trophyGrid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        _trophyGrid.RowTemplate.Height = 52;
        _trophyIconColumn.HeaderText = "Icon";
        _trophyIconColumn.FillWeight = 10F;
        _trophyIconColumn.ImageLayout = DataGridViewImageCellLayout.Zoom;
        _trophyIconColumn.ReadOnly = true;
        _trophyIconColumn.Resizable = DataGridViewTriState.True;
        _trophyIdColumn.Name = "ID";
        _trophyIdColumn.HeaderText = "ID";
        _trophyIdColumn.FillWeight = 8F;
        _trophyIdColumn.ReadOnly = true;
        _trophyNameColumn.Name = "Name";
        _trophyNameColumn.HeaderText = "Name";
        _trophyNameColumn.FillWeight = 23F;
        _trophyNameColumn.ReadOnly = true;
        _trophyDescriptionColumn.Name = "Description";
        _trophyDescriptionColumn.HeaderText = "Description";
        _trophyDescriptionColumn.FillWeight = 39F;
        _trophyDescriptionColumn.ReadOnly = true;
        _trophyTypeColumn.Name = "Type";
        _trophyTypeColumn.HeaderText = "Type";
        _trophyTypeColumn.FillWeight = 12F;
        _trophyTypeColumn.ReadOnly = true;
        _trophyHiddenColumn.Name = "Hidden";
        _trophyHiddenColumn.HeaderText = "Hidden";
        _trophyHiddenColumn.FillWeight = 8F;
        _trophyHiddenColumn.ReadOnly = true;
        _trophyGrid.Columns.Add(_trophyIconColumn);
        _trophyGrid.Columns.Add(_trophyIdColumn);
        _trophyGrid.Columns.Add(_trophyNameColumn);
        _trophyGrid.Columns.Add(_trophyDescriptionColumn);
        _trophyGrid.Columns.Add(_trophyTypeColumn);
        _trophyGrid.Columns.Add(_trophyHiddenColumn);
        AttachGridCopyMenu(_trophyGrid);

        _trophyState.Text = "Trophy information loads when this page is selected.";
        _trophyState.Dock = DockStyle.Top;
        _trophyState.Height = 30;
        _trophyState.Font = new Font("Segoe UI", 8.5F, FontStyle.Italic);
        _trophyState.Padding = new Padding(10, 7, 10, 7);
        _trophyState.TextAlign = ContentAlignment.MiddleLeft;
        _trophyFilter.Placeholder = "Filter trophies by name, description or type";
        _trophyFilter.Dock = DockStyle.Top;
        _trophyFilter.Height = 30;
        _trophyFilter.SearchTextChanged += OnTrophyFilterChanged;

        _trophyTab.Controls.Add(_trophyGrid);
        _trophyTab.Controls.Add(_trophyState);
        _trophyTab.Controls.Add(_trophyFilter);
        _trophyState.BringToFront();
        _trophyFilter.BringToFront();

        ((System.ComponentModel.ISupportInitialize)_trophyGrid).EndInit();
    }

    // ------------------------------------------------------------------
    // File Browser
    // ------------------------------------------------------------------

    private void BuildFilesTab()
    {
        _fileIcons = new ImageList();
        _fileTree = new DarkTreeView();
        _fileList = new DarkListView();
        _fileContextMenu = new DarkContextMenu();
        _filePreviewMenuItem = new ToolStripMenuItem();
        _fileExtractMenuItem = new ToolStripMenuItem();
        _fileOpenContainingMenuItem = new ToolStripMenuItem();
        _fileCopyPathMenuItem = new ToolStripMenuItem();
        _fileCopyNameMenuItem = new ToolStripMenuItem();
        _fileTreeContextMenu = new DarkContextMenu();
        _treePreviewMenuItem = new ToolStripMenuItem();
        _treeExtractMenuItem = new ToolStripMenuItem();
        _treeExpandMenuItem = new ToolStripMenuItem();
        _treeCollapseMenuItem = new ToolStripMenuItem();
        _treeExpandAllMenuItem = new ToolStripMenuItem();
        _treeCollapseAllMenuItem = new ToolStripMenuItem();
        _treeCopyPathMenuItem = new ToolStripMenuItem();
        _treeCopyNameMenuItem = new ToolStripMenuItem();
        _previewImage = new PictureBox();
        _previewText = new DarkTextBox();
        _previewBody = new Panel();
        _previewInfo = new DarkLabel();
        _previewPanel = new DarkSectionPanel();
        _filesSplitPane1 = new DarkSplitPane();
        _filesSplitPane2 = new DarkSplitPane();
        _filesSplitPane3 = new DarkSplitPane();
        _filesSplit = new DarkSplitContainer();
        _fileBreadcrumb = new DarkLabel();
        _upButton = new DarkButton();
        _extractSelectedButton = new DarkButton();
        _extractAllButton = new DarkButton();
        _filesToolbar = new TableLayoutPanel();
        _fileFilter = new DarkSearchBox();
        _filesLayout = new TableLayoutPanel();
        _filesSplit.SuspendLayout();
        _filesSplitPane1.SuspendLayout();
        _filesSplitPane2.SuspendLayout();
        _filesSplitPane3.SuspendLayout();
        _previewPanel.SuspendLayout();
        _previewBody.SuspendLayout();
        _filesToolbar.SuspendLayout();
        _filesLayout.SuspendLayout();

        _fileIcons.ColorDepth = ColorDepth.Depth32Bit;
        _fileIcons.ImageSize = new Size(16, 16);
        _fileIcons.TransparentColor = Color.Transparent;

        _fileTree.CheckBoxes = false;
        _fileTree.FullRowSelect = false;
        _fileTree.HotTracking = false;
        _fileTree.LabelEdit = false;
        _fileTree.Indent = 19;
        _fileTree.ItemHeight = 24;
        _fileTree.ShowLines = true;
        _fileTree.ShowPlusMinus = true;
        _fileTree.ShowRootLines = true;
        _fileTree.ImageList = _fileIcons;
        _fileTree.Dock = DockStyle.Fill;
        _fileTree.AfterSelect += OnFileTreeAfterSelect;
        _fileTree.NodeMouseClick += OnFileTreeNodeMouseClick;
        _fileTree.ItemDrag += OnFilesItemDrag;

        _fileList.View = View.Details;
        _fileList.FullRowSelect = true;
        _fileList.MultiSelect = true;
        _fileList.SmallImageList = _fileIcons;
        _fileList.Dock = DockStyle.Fill;
        _fileList.Columns.Add("Name", 220);
        _fileList.Columns.Add("Type", 100);
        _fileList.Columns.Add("Path", 260);
        _fileList.Columns.Add("Size", 90);
        _fileList.ItemActivate += OnFileListItemActivate;
        _fileList.MouseClick += OnFileListMouseClick;
        _fileList.SelectedIndexChanged += OnFileListSelectedIndexChanged;
        _fileList.KeyDown += OnFileListKeyDown;
        _fileList.ItemDrag += OnFilesItemDrag;

        // File context menu
        ConfigureMenuItem(_filePreviewMenuItem, "Preview", Keys.None, OnFileContextPreview);
        ConfigureMenuItem(_fileExtractMenuItem, "Extract...", Keys.None, OnFileContextExtract);
        ConfigureMenuItem(_fileOpenContainingMenuItem, "Open containing folder", Keys.None, OnFileContextOpenContaining);
        ConfigureMenuItem(_fileCopyPathMenuItem, "Copy path", Keys.None, OnFileContextCopyPath);
        ConfigureMenuItem(_fileCopyNameMenuItem, "Copy filename", Keys.None, OnFileContextCopyName);
        _fileContextMenu.Items.AddRange(new ToolStripItem[]
        {
            _filePreviewMenuItem, new DarkToolStripSeparator(), _fileExtractMenuItem,
            new DarkToolStripSeparator(), _fileOpenContainingMenuItem, _fileCopyPathMenuItem, _fileCopyNameMenuItem
        });
        _fileList.ContextMenuStrip = _fileContextMenu;

        // Folder-tree context menu
        ConfigureMenuItem(_treePreviewMenuItem, "Preview", Keys.None, OnTreeContextPreview);
        ConfigureMenuItem(_treeExtractMenuItem, "Extract...", Keys.None, OnTreeContextExtract);
        ConfigureMenuItem(_treeExpandMenuItem, "Expand", Keys.None, OnTreeContextExpand);
        ConfigureMenuItem(_treeCollapseMenuItem, "Collapse", Keys.None, OnTreeContextCollapse);
        ConfigureMenuItem(_treeExpandAllMenuItem, "Expand all", Keys.None, OnTreeContextExpandAll);
        ConfigureMenuItem(_treeCollapseAllMenuItem, "Collapse all", Keys.None, OnTreeContextCollapseAll);
        ConfigureMenuItem(_treeCopyPathMenuItem, "Copy path", Keys.None, OnTreeContextCopyPath);
        ConfigureMenuItem(_treeCopyNameMenuItem, "Copy name", Keys.None, OnTreeContextCopyName);
        _fileTreeContextMenu.Items.AddRange(new ToolStripItem[]
        {
            _treePreviewMenuItem, _treeExtractMenuItem, new DarkToolStripSeparator(),
            _treeExpandMenuItem, _treeCollapseMenuItem, _treeExpandAllMenuItem, _treeCollapseAllMenuItem,
            new DarkToolStripSeparator(), _treeCopyPathMenuItem, _treeCopyNameMenuItem
        });
        _fileTreeContextMenu.Opening += OnFileTreeContextOpening;
        _fileTree.ContextMenuStrip = _fileTreeContextMenu;

        _previewImage.SizeMode = PictureBoxSizeMode.Zoom;
        _previewImage.Visible = false;
        _previewImage.BackColor = Color.FromArgb(20, 20, 20);
        _previewImage.Dock = DockStyle.Fill;
        _previewText.Multiline = true;
        _previewText.ReadOnly = true;
        _previewText.ScrollBars = ScrollBars.Both;
        _previewText.WordWrap = false;
        _previewText.BorderStyle = BorderStyle.None;
        _previewText.Font = new Font("Consolas", 9F);
        _previewText.Visible = false;
        _previewText.Dock = DockStyle.Fill;
        _previewBody.BackColor = Color.FromArgb(20, 20, 20);
        _previewBody.Padding = new Padding(1);
        _previewBody.Dock = DockStyle.Fill;
        _previewBody.Controls.Add(_previewImage);
        _previewBody.Controls.Add(_previewText);
        _previewInfo.Text = "Double-click a file to preview it.";
        _previewInfo.Dock = DockStyle.Top;
        _previewInfo.AutoEllipsis = true;
        _previewInfo.Height = 34;
        _previewInfo.Padding = new Padding(8, 0, 8, 0);
        _previewInfo.TextAlign = ContentAlignment.MiddleLeft;
        _previewPanel.SectionHeader = "File Preview";
        _previewPanel.Dock = DockStyle.Fill;
        _previewPanel.Controls.Add(_previewBody);
        _previewPanel.Controls.Add(_previewInfo);
        _previewInfo.BringToFront();

        _filesSplitPane1.Controls.Add(_fileTree);
        _filesSplitPane2.Controls.Add(_fileList);
        _filesSplitPane3.Controls.Add(_previewPanel);
        _filesSplit.Dock = DockStyle.Fill;
        _filesSplit.Orientation = DarkSplitContainer.DarkSplitOrientation.Vertical;
        _filesSplit.SplitterWidth = 6;
        _filesSplit.Controls.Add(_filesSplitPane1);
        _filesSplit.Controls.Add(_filesSplitPane2);
        _filesSplit.Controls.Add(_filesSplitPane3);

        _fileBreadcrumb.Text = "Package root";
        _fileBreadcrumb.Dock = DockStyle.Fill;
        _fileBreadcrumb.AutoEllipsis = true;
        _fileBreadcrumb.TextAlign = ContentAlignment.MiddleLeft;
        _fileBreadcrumb.Padding = new Padding(6, 0, 6, 0);
        _upButton.Text = "Up";
        _upButton.Dock = DockStyle.Fill;
        _upButton.Margin = new Padding(2);
        _upButton.Click += OnUpClick;
        _extractSelectedButton.Text = "Extract Selected...";
        _extractSelectedButton.Dock = DockStyle.Fill;
        _extractSelectedButton.Margin = new Padding(2);
        _extractSelectedButton.Click += OnExtractSelectedClick;
        _extractAllButton.Text = "Extract All...";
        _extractAllButton.Dock = DockStyle.Fill;
        _extractAllButton.Margin = new Padding(2);
        _extractAllButton.Click += OnExtractAllClick;

        _filesToolbar.Dock = DockStyle.Fill;
        _filesToolbar.ColumnCount = 4;
        _filesToolbar.RowCount = 1;
        _filesToolbar.Margin = new Padding(0);
        _filesToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _filesToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 56F));
        _filesToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 156F));
        _filesToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F));
        _filesToolbar.Controls.Add(_fileBreadcrumb, 0, 0);
        _filesToolbar.Controls.Add(_upButton, 1, 0);
        _filesToolbar.Controls.Add(_extractSelectedButton, 2, 0);
        _filesToolbar.Controls.Add(_extractAllButton, 3, 0);

        _fileFilter.Placeholder = "Filter filename here";
        _fileFilter.Dock = DockStyle.Fill;
        _fileFilter.SearchTextChanged += OnFileFilterChanged;

        _filesLayout.Dock = DockStyle.Fill;
        _filesLayout.ColumnCount = 1;
        _filesLayout.RowCount = 3;
        _filesLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
        _filesLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        _filesLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _filesLayout.Controls.Add(_filesToolbar, 0, 0);
        _filesLayout.Controls.Add(_fileFilter, 0, 1);
        _filesLayout.Controls.Add(_filesSplit, 0, 2);
        _filesTab.Controls.Add(_filesLayout);

        _filesLayout.ResumeLayout(false);
        _filesToolbar.ResumeLayout(false);
        _previewBody.ResumeLayout(false);
        _previewPanel.ResumeLayout(false);
        _filesSplitPane3.ResumeLayout(false);
        _filesSplitPane2.ResumeLayout(false);
        _filesSplitPane1.ResumeLayout(false);
        _filesSplit.ResumeLayout(false);
    }

    private void AttachGridCopyMenu(DarkDataGridView grid)
    {
        if (_gridContextMenu is null)
        {
            _gridContextMenu = new DarkContextMenu();
            _gridCopyValueMenuItem = new ToolStripMenuItem();
            _gridCopyRowMenuItem = new ToolStripMenuItem();
            ConfigureMenuItem(_gridCopyValueMenuItem, "Copy value", Keys.None, OnGridCopyValue);
            ConfigureMenuItem(_gridCopyRowMenuItem, "Copy row", Keys.None, OnGridCopyRow);
            _gridContextMenu.Items.Add(_gridCopyValueMenuItem);
            _gridContextMenu.Items.Add(_gridCopyRowMenuItem);
        }
        grid.CellContextMenuStripNeeded += OnGridContextMenuNeeded;
    }

    // ------------------------------------------------------------------
    // Artwork
    // ------------------------------------------------------------------

    private void BuildArtworkTab()
    {
        _artworkLayout = new TableLayoutPanel();
        _iconPanel = new DarkSectionPanel();
        _pic0Panel = new DarkSectionPanel();
        _pic1Panel = new DarkSectionPanel();
        _pic2Panel = new DarkSectionPanel();
        _iconEmpty = new DarkLabel();
        _pic0Empty = new DarkLabel();
        _pic1Empty = new DarkLabel();
        _pic2Empty = new DarkLabel();
        _iconArtBox = new PictureBox();
        _pic0Box = new PictureBox();
        _pic1Box = new PictureBox();
        _pic2Box = new PictureBox();
        _iconArtMenu = new DarkContextMenu();
        _pic0ArtMenu = new DarkContextMenu();
        _pic1ArtMenu = new DarkContextMenu();
        _pic2ArtMenu = new DarkContextMenu();
        _iconArtSaveMenuItem = new ToolStripMenuItem();
        _pic0ArtSaveMenuItem = new ToolStripMenuItem();
        _pic1ArtSaveMenuItem = new ToolStripMenuItem();
        _pic2ArtSaveMenuItem = new ToolStripMenuItem();
        _iconPanel.SuspendLayout();
        _pic0Panel.SuspendLayout();
        _pic1Panel.SuspendLayout();
        _pic2Panel.SuspendLayout();
        _artworkLayout.SuspendLayout();

        BuildArtworkPanel(_iconPanel, _iconEmpty, _iconArtBox, "ICON", "No icon image in this package.");
        BuildArtworkPanel(_pic0Panel, _pic0Empty, _pic0Box, "PIC0", "No PIC0 image in this package.");
        BuildArtworkPanel(_pic1Panel, _pic1Empty, _pic1Box, "PIC1", "No PIC1 image in this package.");
        BuildArtworkPanel(_pic2Panel, _pic2Empty, _pic2Box, "PIC2", "No PIC2 image in this package.");
        BuildArtworkContextMenu(_iconArtMenu, _iconArtSaveMenuItem, _iconArtBox, "ICON");
        BuildArtworkContextMenu(_pic0ArtMenu, _pic0ArtSaveMenuItem, _pic0Box, "PIC0");
        BuildArtworkContextMenu(_pic1ArtMenu, _pic1ArtSaveMenuItem, _pic1Box, "PIC1");
        BuildArtworkContextMenu(_pic2ArtMenu, _pic2ArtSaveMenuItem, _pic2Box, "PIC2");

        _artworkLayout.Dock = DockStyle.Fill;
        _artworkLayout.ColumnCount = 2;
        _artworkLayout.RowCount = 1;
        _artworkLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        _artworkLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        _artworkLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _artworkTab.Controls.Add(_artworkLayout);

        _artworkLayout.ResumeLayout(false);
        _pic2Panel.ResumeLayout(false);
        _pic1Panel.ResumeLayout(false);
        _pic0Panel.ResumeLayout(false);
        _iconPanel.ResumeLayout(false);
    }

    private static void BuildArtworkPanel(DarkSectionPanel panel, DarkLabel empty, PictureBox box,
        string header, string emptyText)
    {
        panel.SectionHeader = header;
        panel.Dock = DockStyle.Fill;
        panel.Padding = new Padding(12, 10, 12, 12);
        empty.Text = emptyText;
        empty.Dock = DockStyle.Fill;
        empty.Font = new Font("Segoe UI", 9F, FontStyle.Italic);
        empty.TextAlign = ContentAlignment.MiddleCenter;
        box.Dock = DockStyle.Fill;
        box.TabStop = false;
        box.SizeMode = PictureBoxSizeMode.Zoom;
        box.Visible = false;
        panel.Controls.Add(empty);
        panel.Controls.Add(box);
    }

    private void BuildArtworkContextMenu(DarkContextMenu menu, ToolStripMenuItem saveItem, PictureBox box, string slot)
    {
        ConfigureMenuItem(saveItem, "Save image...", Keys.None, (_, _) => SaveArtworkSlot(box, slot));
        menu.Items.Add(saveItem);
        box.ContextMenuStrip = menu;
    }

    /// <summary>PS4 shows PIC0/PIC1; PS5 additionally shows the icon and PIC2 (all with placeholders).</summary>
    private TableLayoutPanel BuildArtworkLayout(bool isPs4)
    {
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = isPs4 ? 1 : 2 };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            components?.Dispose();
        }
        base.Dispose(disposing);
    }
}
