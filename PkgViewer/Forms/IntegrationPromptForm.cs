using DarkUI.Controls;
using DarkUI.Forms;
using PkgViewer.Infrastructure;
using PkgViewer.Shell;

namespace PkgViewer.Forms;

/// <summary>
/// Shown on every bare launch. Copies the viewer to a fixed folder and registers it as a handler for
/// .pkg, .ffpfsc, .ffpkg and .exfat (unassigned types become PkgViewer automatically), then explains
/// the one manual step Windows needs for types that already belong to another app. "Close" dismisses
/// it; "Open file" dismisses it and continues to the file picker.
/// </summary>
internal sealed class IntegrationPromptForm : DarkForm
{
    private readonly DarkLabel _message = new();
    private readonly DarkButton _copy = new();
    private readonly DarkButton _close = new();
    private readonly DarkButton _openFile = new();
    private readonly string _folder;

    public IntegrationPromptForm()
    {
        AppIcon.Apply(this);
        Text = "PkgViewer";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(560, 264);

        _folder = RunSetup();

        _message.Location = new Point(16, 16);
        _message.Size = new Size(528, 196);
        _message.Text =
            "Set .pkg files to open with PkgViewer\n\n" +
            "PkgViewer is registered for .pkg, .ffpfsc, .ffpkg and .exfat, with a copy in:\n" + _folder + "\n\n" +
            "If a type still opens with another app, set it once:\n" +
            "1. Right-click a file of that type and choose Properties.\n" +
            "2. Next to \"Opens with\", click Change.\n" +
            "3. Pick PkgViewer and tick \"Always use this app\".";

        _copy.Text = "Copy folder";
        _copy.Location = new Point(16, 216);
        _copy.Size = new Size(140, 32);
        _copy.Click += OnCopyClick;

        _close.Text = "Close";
        _close.Location = new Point(300, 216);
        _close.Size = new Size(120, 32);
        _close.Click += (_, _) => { OpenFilePicker = false; Close(); };

        _openFile.Text = "Open file";
        _openFile.Location = new Point(424, 216);
        _openFile.Size = new Size(120, 32);
        _openFile.Click += (_, _) => { OpenFilePicker = true; Close(); };

        AcceptButton = _close;
        CancelButton = _close;

        Controls.AddRange([_message, _copy, _close, _openFile]);
    }

    /// <summary>True when the user chose "Open file" and the caller should open the file picker.</summary>
    public bool OpenFilePicker { get; private set; }

    private void OnCopyClick(object? sender, EventArgs e)
    {
        try
        {
            if (_folder.Length > 0)
            {
                Clipboard.SetText(_folder);
                _copy.Text = "Copied!";
            }
        }
        catch (Exception ex)
        {
            Logger.Exception("Copy folder", ex);
            DarkMessageBox.ShowError(ex.Message, "PkgViewer");
        }
    }

    /// <summary>
    /// Copies the viewer into %LocalAppData%\PkgViewer and registers it for the package types, then
    /// returns that folder. Falls back to the running folder if setup fails.
    /// </summary>
    private static string RunSetup()
    {
        try
        {
            PackageShellIntegration.Install();
            string? directory = Path.GetDirectoryName(PackageShellIntegration.InstalledExecutable);
            if (!string.IsNullOrEmpty(directory)) return directory;
        }
        catch (Exception ex)
        {
            Logger.Exception("Setup", ex);
        }

        string? process = Environment.ProcessPath;
        return (process is not null ? Path.GetDirectoryName(process) : null) ?? process ?? string.Empty;
    }
}
