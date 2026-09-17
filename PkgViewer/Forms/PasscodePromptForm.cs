using System.Text;
using DarkUI.Controls;
using DarkUI.Forms;

namespace PkgViewer.Forms;

/// <summary>
/// Asks for a package passcode or lets the user continue without one. A debug passcode is exactly 32
/// printable ASCII characters; "no passcode / unknown retail key" opens metadata only.
/// </summary>
internal sealed class PasscodePromptForm : DarkForm
{
    private readonly DarkTextBox _input = new();
    private readonly DarkCheckBox _noPasscode = new();
    private readonly DarkCheckBox _show = new();
    private readonly DarkLabel _validation = new();
    private readonly DarkButton _ok = new();

    public PasscodePromptForm(string? initial = null)
    {
        AppIcon.Apply(this);
        Text = "Package passcode";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(480, 214);

        var prompt = new DarkLabel
        {
            Text = "This package may be protected by a passcode. Enter the 32-character debug passcode to "
                 + "read its contents, or continue without one to see metadata only.",
            Location = new Point(16, 14),
            Size = new Size(448, 52)
        };

        _input.Location = new Point(16, 72);
        _input.Size = new Size(392, 24);
        _input.MaxLength = 64;
        _input.UseSystemPasswordChar = true;
        if (!string.IsNullOrEmpty(initial)) _input.Text = initial;

        _show.Text = "Show";
        _show.Location = new Point(416, 74);
        _show.Size = new Size(56, 24);
        _show.CheckedChanged += (_, _) => _input.UseSystemPasswordChar = !_show.Checked;

        _noPasscode.Text = "Continue without a passcode (metadata only)";
        _noPasscode.Location = new Point(16, 106);
        _noPasscode.Size = new Size(320, 24);
        _noPasscode.CheckedChanged += (_, _) =>
        {
            _input.Enabled = !_noPasscode.Checked;
            _show.Enabled = !_noPasscode.Checked;
            UpdateValidation();
        };

        _validation.Location = new Point(16, 136);
        _validation.Size = new Size(448, 30);
        _validation.ForeColor = Color.FromArgb(230, 160, 160);
        _validation.Text = string.Empty;

        _ok.Text = "OK";
        _ok.Location = new Point(288, 172);
        _ok.Size = new Size(80, 28);
        _ok.Click += OnOk;

        var cancel = new DarkButton { Text = "Cancel", Location = new Point(376, 172), Size = new Size(88, 28) };
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

        AcceptButton = _ok;
        CancelButton = cancel;
        _input.TextChanged += (_, _) => UpdateValidation();

        Controls.AddRange([prompt, _input, _show, _noPasscode, _validation, _ok, cancel]);
        UpdateValidation();
    }

    /// <summary>The entered passcode (not trimmed), or empty when "no passcode" is chosen.</summary>
    public string? Passcode => _noPasscode.Checked ? string.Empty : _input.Text;

    public bool IsNoPasscode => _noPasscode.Checked;

    private void UpdateValidation()
    {
        if (_noPasscode.Checked)
        {
            _validation.Text = string.Empty;
            _ok.Enabled = true;
            return;
        }

        string value = _input.Text;
        if (value.Length == 0)
        {
            _validation.Text = "Enter a passcode, or choose to continue without one.";
            _ok.Enabled = false;
            return;
        }

        bool valid = value.Length == 32 && Encoding.ASCII.GetByteCount(value) == 32 &&
                     value.All(character => character is >= (char)0x20 and <= (char)0x7E);
        _validation.Text = valid
            ? string.Empty
            : $"{value.Length} character(s); a passcode must be exactly 32 printable ASCII characters.";
        _ok.Enabled = valid;
    }

    private void OnOk(object? sender, EventArgs e)
    {
        if (_noPasscode.Checked)
        {
            DialogResult = DialogResult.OK;
            Close();
            return;
        }
        if (!_ok.Enabled)
        {
            DarkMessageBox.ShowWarning(
                "Enter a 32-character passcode, or choose to continue without one.", "PkgViewer");
            return;
        }
        DialogResult = DialogResult.OK;
        Close();
    }
}
