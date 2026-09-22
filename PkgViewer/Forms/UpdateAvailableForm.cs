using DarkUI.Forms;
using PkgViewer.Infrastructure;

namespace PkgViewer.Forms;

/// <summary>
/// Shown when a newer PkgViewer release exists. Presents the installed and latest versions and the
/// release notes in a scrollable pane, with a link to the download page.
/// </summary>
internal sealed partial class UpdateAvailableForm : DarkForm
{
    public UpdateAvailableForm(UpdateCheck update, string currentVersion)
    {
        InitializeComponent();
        AppIcon.Apply(this);

        lblVersions.Text = $"Installed: {currentVersion}    Latest: {update.Latest}";
        txtChangelog.Text = FormatNotes(update.Notes);
        txtChangelog.Select(0, 0);
        PageUrl = update.PageUrl;
    }

    /// <summary>The release page to open when the user chooses to download.</summary>
    public string PageUrl { get; }

    private void btnOpen_Click(object? sender, EventArgs e)
    {
        DialogResult = DialogResult.OK;
        Close();
    }

    private void btnCancel_Click(object? sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }

    /// <summary>Turns the Markdown release body into plain, readable text for the scrollable pane.</summary>
    private static string FormatNotes(string notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
            return "See the release page for the full list of changes.";

        var output = new List<string>();
        foreach (string raw in notes.Replace("\r\n", "\n").Split('\n'))
        {
            string line = raw.TrimEnd();
            string trimmed = line.TrimStart();

            if (trimmed.StartsWith("### ")) line = trimmed[4..];
            else if (trimmed.StartsWith("## ")) line = trimmed[3..];
            else if (trimmed.StartsWith("# ")) line = trimmed[2..];
            else if (trimmed is "---" or "***" or "___") continue;

            output.Add(line);
        }

        string text = string.Join(Environment.NewLine, output).Trim();
        return text.Length == 0 ? "See the release page for the full list of changes." : text;
    }
}
