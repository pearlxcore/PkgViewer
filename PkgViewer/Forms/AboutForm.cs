using System.Diagnostics;

namespace PkgViewer.Forms;

/// <summary>About window mirroring PS5 PKG Tool: icon, identity, credits and support links.</summary>
internal sealed partial class AboutForm : DarkUI.Forms.DarkForm
{
    private const string GitHubUrl = "https://github.com/pearlxcore";
    private const string KoFiUrl = "https://ko-fi.com/R6R524N7X";
    private const string PayPalUrl = "https://www.paypal.com/paypalme/pearlxcoree";

    public AboutForm(string version)
    {
        InitializeComponent();
        AppIcon.Apply(this);
        lblVersion.Text = "Version " + version;
        if (AppIcon.Load() is { } icon)
        {
            using var sized = new Icon(icon, new Size(64, 64));
            picAppIcon.Image = sized.ToBitmap();
        }
    }

    private void btnGitHub_Click(object? sender, EventArgs e) => Open(GitHubUrl);

    private void btnKofi_Click(object? sender, EventArgs e) => Open(KoFiUrl);

    private void btnPayPal_Click(object? sender, EventArgs e) => Open(PayPalUrl);

    private void btnClose_Click(object? sender, EventArgs e) => Close();

    private static void Open(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or InvalidOperationException)
        {
        }
    }
}
