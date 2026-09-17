using DarkUI.Controls;
using DarkUI.Forms;
using PkgViewer.Infrastructure;
using PkgViewer.Shell;

namespace PkgViewer.Forms;

/// <summary>
/// First-run prompt offering to register PkgViewer's file-type integration. On success the dialog
/// switches to a confirmation state (so the user sees it can double-click .pkg) before closing.
/// </summary>
internal sealed class IntegrationPromptForm : DarkForm
{
    private readonly DarkLabel _message = new();
    private readonly DarkButton _primary = new();
    private readonly DarkButton _decline = new();
    private bool _integrated;
    private bool _declined;

    public IntegrationPromptForm()
    {
        AppIcon.Apply(this);
        Text = "PkgViewer Integration";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(480, 196);

        _message.Location = new Point(18, 16);
        _message.Size = new Size(444, 116);
        _message.Text =
            "Associate PkgViewer with PS4/PS5 package files (.pkg, .ffpfsc, .ffpkg, .exfat)?\n\n" +
            "After this you can double-click a package to open it in PkgViewer.";

        _primary.Text = "Set integration";
        _primary.Location = new Point(206, 146);
        _primary.Size = new Size(126, 32);
        _primary.Click += OnPrimaryClick;

        _decline.Text = "Not now";
        _decline.Location = new Point(342, 146);
        _decline.Size = new Size(120, 32);
        _decline.Click += (_, _) => { _declined = true; Close(); };

        AcceptButton = _primary;
        CancelButton = _decline;

        Controls.AddRange([_message, _primary, _decline]);
    }

    /// <summary>True when the user completed the integration.</summary>
    public bool Integrated => _integrated;

    /// <summary>True when the user chose "Not now" without integrating.</summary>
    public bool Declined => _declined;

    private void OnPrimaryClick(object? sender, EventArgs e)
    {
        if (_integrated)
        {
            Close();
            return;
        }

        try
        {
            PackageShellIntegration.Install();
            _integrated = true;
            _message.Text = "Integration added.\n\nYou can now double-click a .pkg file to open it in PkgViewer.";
            _primary.Text = "OK";
            _primary.Location = new Point(342, 146);
            _decline.Visible = false;
        }
        catch (Exception ex)
        {
            Logger.Exception("Integration prompt", ex);
            DarkMessageBox.ShowError(ex.Message, "PkgViewer");
        }
    }
}
