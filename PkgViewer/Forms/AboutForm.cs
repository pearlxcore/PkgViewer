using System.Reflection;
using DarkUI.Controls;
using DarkUI.Forms;

namespace PkgViewer.Forms;

internal sealed class AboutForm : DarkForm
{
    public AboutForm()
    {
        AppIcon.Apply(this);
        Text = "About PkgViewer";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(420, 170);

        string version = Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? Assembly.GetEntryAssembly()?.GetName().Version?.ToString()
            ?? "1.0.0";

        var title = new DarkLabel
        {
            Text = "PkgViewer",
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            Location = new Point(20, 18),
            AutoSize = true
        };
        var versionLabel = new DarkLabel { Text = "Version " + version, Location = new Point(22, 54), AutoSize = true };
        var description = new DarkLabel
        {
            Text = "Quick viewer for PS4 and PS5 packages.\nSupports .pkg, .ffpfsc, .ffpkg and .exfat.",
            Location = new Point(22, 80),
            Size = new Size(376, 44)
        };
        var ok = new DarkButton { Text = "OK", Location = new Point(318, 128), Size = new Size(80, 28) };
        ok.Click += (_, _) => Close();
        AcceptButton = ok;

        Controls.AddRange([title, versionLabel, description, ok]);
    }
}
