using DarkUI.Controls;
using DarkUI.Forms;
using PkgViewer.Infrastructure;
using PkgViewer.Shell;

namespace PkgViewer.Forms;

/// <summary>
/// First-run prompt offering to set PkgViewer up: install a managed copy under %LocalAppData% and
/// register it as a package-file viewer. On success the dialog switches to a confirmation state and
/// opens the Windows Default apps page so the user can make it the default once.
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
        Text = "PkgViewer Setup";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(500, 244);

        _message.Location = new Point(18, 16);
        _message.Size = new Size(464, 158);
        _message.Text =
            "Set up PkgViewer?\n\n" +
            "A copy is installed to:\n" + PackageShellIntegration.InstallDirectory + "\n\n" +
            "and registered as a viewer for .pkg, .ffpfsc, .ffpkg and .exfat. Windows will then ask " +
            "you to confirm PkgViewer as the default the first time you open one.";

        _primary.Text = "Set up";
        _primary.Location = new Point(216, 194);
        _primary.Size = new Size(126, 32);
        _primary.Click += OnPrimaryClick;

        _decline.Text = "Not now";
        _decline.Location = new Point(352, 194);
        _decline.Size = new Size(120, 32);
        _decline.Click += (_, _) => { _declined = true; Close(); };

        AcceptButton = _primary;
        CancelButton = _decline;

        Controls.AddRange([_message, _primary, _decline]);
    }

    /// <summary>True when the user completed the setup.</summary>
    public bool Integrated => _integrated;

    /// <summary>True when the user chose "Not now" without setting up.</summary>
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
            _message.Text =
                "Set up complete.\n\n" +
                "Opening Windows Default apps so you can pick PkgViewer for .pkg.";
            _primary.Text = "OK";
            _primary.Location = new Point(352, 194);
            _decline.Visible = false;
            PackageShellIntegration.OpenDefaultAppsSettings();
        }
        catch (Exception ex)
        {
            Logger.Exception("Integration prompt", ex);
            DarkMessageBox.ShowError(ex.Message, "PkgViewer");
        }
    }
}
