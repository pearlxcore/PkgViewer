using DarkUI.Controls;
using DarkUI.Forms;

namespace PkgViewer.Forms;

internal enum ExtractionConflictChoice
{
    Replace,
    Skip,
    KeepBoth,
    Cancel
}

/// <summary>
/// Asks how to handle a destination that already exists during extraction. Offers an "apply to all"
/// option so a large selection only prompts once.
/// </summary>
internal sealed class ExtractionConflictForm : DarkForm
{
    private readonly DarkCheckBox _applyToAll = new();

    public ExtractionConflictForm(string destinationName, bool allowApplyToAll)
    {
        AppIcon.Apply(this);
        Text = "File already exists";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(520, 186);

        var message = new DarkLabel
        {
            Text = $"The destination already contains:\n\n{destinationName}\n\nHow should it be handled?",
            Location = new Point(16, 16),
            Size = new Size(488, 74)
        };

        _applyToAll.Text = "Apply to all remaining conflicts";
        _applyToAll.Location = new Point(16, 96);
        _applyToAll.Size = new Size(300, 24);
        _applyToAll.Visible = allowApplyToAll;

        var replace = new DarkButton { Text = "Replace", Location = new Point(16, 136), Size = new Size(110, 30) };
        var skip = new DarkButton { Text = "Skip", Location = new Point(134, 136), Size = new Size(110, 30) };
        var keepBoth = new DarkButton { Text = "Keep both", Location = new Point(252, 136), Size = new Size(110, 30) };
        var cancel = new DarkButton { Text = "Cancel", Location = new Point(392, 136), Size = new Size(110, 30) };

        replace.Click += (_, _) => Finish(ExtractionConflictChoice.Replace);
        skip.Click += (_, _) => Finish(ExtractionConflictChoice.Skip);
        keepBoth.Click += (_, _) => Finish(ExtractionConflictChoice.KeepBoth);
        cancel.Click += (_, _) => Finish(ExtractionConflictChoice.Cancel);

        AcceptButton = replace;
        CancelButton = cancel;
        Controls.AddRange([message, _applyToAll, replace, skip, keepBoth, cancel]);
    }

    public ExtractionConflictChoice Choice { get; private set; } = ExtractionConflictChoice.Cancel;

    public bool ApplyToAll => _applyToAll.Checked;

    private void Finish(ExtractionConflictChoice choice)
    {
        Choice = choice;
        DialogResult = choice == ExtractionConflictChoice.Cancel ? DialogResult.Cancel : DialogResult.OK;
        Close();
    }
}
