using DarkUI.Controls;
using DarkUI.Forms;
using PkgViewer.Infrastructure;

namespace PkgViewer.Forms;

/// <summary>
/// Shown on every bare launch. Explains the one manual step Windows needs to make PkgViewer open
/// .pkg files, with a button that copies the viewer's folder to the clipboard. "Close" dismisses it;
/// "Open file" dismisses it and continues to the file picker.
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
        ClientSize = new Size(560, 248);

        string? process = Environment.ProcessPath;
        _folder = (process is not null ? Path.GetDirectoryName(process) : null) ?? process ?? string.Empty;

        _message.Location = new Point(16, 16);
        _message.Size = new Size(528, 180);
        _message.Text =
            "Set .pkg files to open with PkgViewer\n\n" +
            "Windows needs you to do this once:\n" +
            "1. Right-click a .pkg file and choose Properties.\n" +
            "2. Next to \"Opens with\", click Change.\n" +
            "3. Choose \"More apps\" / \"Choose another app\" and browse to:\n" +
            _folder + "\n" +
            "4. Pick PkgViewer.exe, tick \"Always use this app\", and click OK.";

        _copy.Text = "Copy folder";
        _copy.Location = new Point(16, 200);
        _copy.Size = new Size(140, 32);
        _copy.Click += OnCopyClick;

        _close.Text = "Close";
        _close.Location = new Point(300, 200);
        _close.Size = new Size(120, 32);
        _close.Click += (_, _) => { OpenFilePicker = false; Close(); };

        _openFile.Text = "Open file";
        _openFile.Location = new Point(424, 200);
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
}
