using DarkUI.Controls;
using DarkUI.Forms;

namespace PkgViewer.Forms;

/// <summary>Mirror of PS4PKGTool's PasscodePromptForm: asks for a package passcode or "no passcode".</summary>
internal sealed class PasscodePromptForm : DarkForm
{
    private readonly DarkTextBox _input = new();
    private readonly DarkCheckBox _noPasscode = new();

    public PasscodePromptForm()
    {
        AppIcon.Apply(this);
        Text = "PKG Passcode";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(460, 170);

        var prompt = new DarkLabel
        {
            Text = "This PKG is protected by a custom passcode. Enter the PKG passcode to list its files, "
                 + "or choose \"No passcode\" for official PKGs whose key is unknown.",
            Location = new Point(16, 16),
            Size = new Size(428, 60)
        };

        _input.Location = new Point(16, 84);
        _input.Size = new Size(428, 24);
        _input.MaxLength = 32;
        _input.Text = "00000000000000000000000000000000";
        _input.TextAlign = HorizontalAlignment.Center;

        _noPasscode.Text = "No passcode (official PKG)";
        _noPasscode.Location = new Point(16, 114);
        _noPasscode.Size = new Size(240, 24);
        _noPasscode.CheckedChanged += (_, _) => _input.Enabled = !_noPasscode.Checked;

        var ok = new DarkButton { Text = "OK", Location = new Point(268, 132), Size = new Size(80, 28) };
        var cancel = new DarkButton { Text = "Cancel", Location = new Point(356, 132), Size = new Size(88, 28) };
        ok.Click += OnOk;
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

        AcceptButton = ok;
        CancelButton = cancel;

        Controls.AddRange([prompt, _input, _noPasscode, ok, cancel]);
    }

    public string? Passcode => _noPasscode.Checked ? string.Empty : _input.Text.Trim();

    public bool IsNoPasscode => _noPasscode.Checked;

    private void OnOk(object? sender, EventArgs e)
    {
        if (_noPasscode.Checked)
        {
            DialogResult = DialogResult.OK;
            Close();
            return;
        }
        if (string.IsNullOrWhiteSpace(_input.Text))
        {
            DarkMessageBox.ShowWarning("Enter the PKG passcode, or press Cancel.", "PkgViewer");
            return;
        }
        DialogResult = DialogResult.OK;
        Close();
    }
}
