using DarkUI.Controls;
using DarkUI.Forms;
using PkgViewer.Infrastructure;
using PkgViewer.Shell;

namespace PkgViewer.Forms;

/// <summary>
/// Dedicated file-association management: register/remove only PkgViewer's own entries, open the
/// Windows Default apps page, and (under an explicit advanced action) remove legacy handlers.
/// </summary>
internal sealed class FileAssociationsForm : DarkForm
{
    private readonly DarkLabel _status = new();

    public FileAssociationsForm()
    {
        AppIcon.Apply(this);
        Text = "File associations";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(560, 250);

        var info = new DarkLabel
        {
            Text = "PkgViewer registers itself as an optional handler for .pkg, .ffpfsc, .ffpkg and .exfat. "
                 + "Windows keeps the final default-app choice; use the Default apps page to pick it.",
            Location = new Point(16, 14),
            Size = new Size(528, 46)
        };

        _status.Location = new Point(16, 66);
        _status.Size = new Size(528, 30);
        _status.Text = string.Empty;

        var register = new DarkButton { Text = "Register PkgViewer", Location = new Point(16, 106), Size = new Size(170, 32) };
        register.Click += (_, _) => Run(PackageShellIntegration.Install);

        var remove = new DarkButton { Text = "Remove PkgViewer entries", Location = new Point(196, 106), Size = new Size(180, 32) };
        remove.Click += (_, _) => Run(PackageShellIntegration.Uninstall);

        var defaultApps = new DarkButton { Text = "Open Windows Default apps", Location = new Point(16, 148), Size = new Size(220, 32) };
        defaultApps.Click += (_, _) => Run(PackageShellIntegration.OpenDefaultAppsSettings);

        var legacy = new DarkButton { Text = "Remove legacy handlers...", Location = new Point(246, 148), Size = new Size(200, 32) };
        legacy.Click += (_, _) => RemoveLegacy();

        var close = new DarkButton { Text = "Close", Location = new Point(452, 202), Size = new Size(92, 30) };
        close.Click += (_, _) => Close();
        AcceptButton = close;
        CancelButton = close;

        Controls.AddRange([info, _status, register, remove, defaultApps, legacy, close]);
        UpdateStatus();
    }

    private void Run(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Logger.Exception("File associations", ex);
            DarkMessageBox.ShowError(ex.Message, "File associations");
        }
        UpdateStatus();
    }

    private void RemoveLegacy()
    {
        if (DarkMessageBox.ShowWarning(
                "Remove the file-type registrations of older PS4/PS5 PKG tools and viewers from Open With?\n\n" +
                "Only those specific entries are removed; PkgViewer's own registration is not changed.",
                "File associations", DarkDialogButton.YesNo) != DialogResult.Yes)
            return;
        Run(PackageShellIntegration.RemoveLegacyAssociations);
    }

    private void UpdateStatus()
    {
        _status.Text = PackageShellIntegration.IsInstalled()
            ? "Status: PkgViewer entries are registered."
            : "Status: PkgViewer entries are not registered.";
    }
}
