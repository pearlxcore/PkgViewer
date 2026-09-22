using System.Drawing;
using System.Windows.Forms;
using DarkUI.Controls;

namespace PkgViewer.Forms;

partial class UpdateAvailableForm
{
    private System.ComponentModel.IContainer? components = null;

    private DarkLabel lblHeading = null!;
    private DarkLabel lblVersions = null!;
    private DarkLabel lblWhatsNew = null!;
    private DarkRichTextBox txtChangelog = null!;
    private DarkButton btnOpen = null!;
    private DarkButton btnCancel = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing) components?.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        lblHeading = new DarkLabel();
        lblVersions = new DarkLabel();
        lblWhatsNew = new DarkLabel();
        txtChangelog = new DarkRichTextBox();
        btnOpen = new DarkButton();
        btnCancel = new DarkButton();
        SuspendLayout();

        lblHeading.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
        lblHeading.Location = new Point(20, 18);
        lblHeading.Name = "lblHeading";
        lblHeading.Size = new Size(500, 26);
        lblHeading.Text = "A new version of Pkg Viewer is available.";

        lblVersions.Font = new Font("Segoe UI", 9F);
        lblVersions.Location = new Point(20, 50);
        lblVersions.Name = "lblVersions";
        lblVersions.Size = new Size(500, 20);
        lblVersions.Text = "Installed: 0.0.0    Latest: 0.0.0";

        lblWhatsNew.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        lblWhatsNew.Location = new Point(20, 82);
        lblWhatsNew.Name = "lblWhatsNew";
        lblWhatsNew.Size = new Size(500, 20);
        lblWhatsNew.Text = "What's new";

        txtChangelog.DetectUrls = false;
        txtChangelog.Location = new Point(20, 106);
        txtChangelog.Name = "txtChangelog";
        txtChangelog.ReadOnly = true;
        txtChangelog.Size = new Size(500, 250);
        txtChangelog.TabIndex = 3;
        txtChangelog.WordWrap = true;

        btnOpen.Font = new Font("Segoe UI", 9F);
        btnOpen.Location = new Point(250, 384);
        btnOpen.Name = "btnOpen";
        btnOpen.Size = new Size(150, 32);
        btnOpen.TabIndex = 1;
        btnOpen.Text = "Open Download Page";
        btnOpen.Click += btnOpen_Click;

        btnCancel.Font = new Font("Segoe UI", 9F);
        btnCancel.Location = new Point(410, 384);
        btnCancel.Name = "btnCancel";
        btnCancel.Size = new Size(110, 32);
        btnCancel.TabIndex = 2;
        btnCancel.Text = "Cancel";
        btnCancel.Click += btnCancel_Click;

        AcceptButton = btnOpen;
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        CancelButton = btnCancel;
        ClientSize = new Size(540, 436);
        Controls.Add(txtChangelog);
        Controls.Add(lblWhatsNew);
        Controls.Add(lblVersions);
        Controls.Add(lblHeading);
        Controls.Add(btnCancel);
        Controls.Add(btnOpen);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "UpdateAvailableForm";
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "New Version Available";
        ResumeLayout(false);
    }
}
