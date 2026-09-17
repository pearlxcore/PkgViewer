using DarkUI.Controls;
using DarkUI.Forms;

namespace PkgViewer.Forms;

/// <summary>Shows the package's diagnostic warnings in full, with a copy action.</summary>
internal sealed class WarningsForm : DarkForm
{
    public WarningsForm(IReadOnlyList<string> warnings)
    {
        AppIcon.Apply(this);
        Text = "Package warnings";
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        ShowInTaskbar = false;
        MinimumSize = new Size(520, 320);
        ClientSize = new Size(680, 420);

        string body = warnings.Count == 0
            ? "No warnings were reported for this package."
            : string.Join(Environment.NewLine, warnings.Select((warning, index) => $"{index + 1}. {warning}"));

        var text = new DarkTextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = true,
            Font = new Font("Consolas", 9F),
            Text = body
        };

        var copy = new DarkButton { Text = "Copy", Anchor = AnchorStyles.Bottom | AnchorStyles.Right, Size = new Size(90, 30) };
        copy.Location = new Point(ClientSize.Width - 200, ClientSize.Height - 42);
        copy.Click += (_, _) =>
        {
            try { Clipboard.SetText(body); }
            catch (System.Runtime.InteropServices.ExternalException) { }
        };
        var close = new DarkButton { Text = "Close", Anchor = AnchorStyles.Bottom | AnchorStyles.Right, Size = new Size(90, 30) };
        close.Location = new Point(ClientSize.Width - 102, ClientSize.Height - 42);
        close.Click += (_, _) => Close();
        AcceptButton = close;
        CancelButton = close;

        Controls.Add(text);
        Controls.Add(copy);
        Controls.Add(close);
        text.BringToFront();
    }
}
