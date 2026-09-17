using System.Drawing;
using System.Windows.Forms;
using DarkUI.Controls;

namespace PkgViewer.Forms;

partial class AboutForm
{
    private System.ComponentModel.IContainer? components = null;

    private PictureBox picAppIcon = null!;
    private DarkLabel lblTitle = null!;
    private DarkLabel lblVersion = null!;
    private DarkLabel lblCopyright = null!;
    private DarkLabel lblCredits = null!;
    private DarkLabel lblComponents = null!;
    private DarkButton btnGitHub = null!;
    private DarkButton btnKofi = null!;
    private DarkButton btnPayPal = null!;
    private DarkButton btnClose = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing) components?.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        picAppIcon = new PictureBox();
        lblTitle = new DarkLabel();
        lblVersion = new DarkLabel();
        lblCopyright = new DarkLabel();
        lblCredits = new DarkLabel();
        lblComponents = new DarkLabel();
        btnGitHub = new DarkButton();
        btnKofi = new DarkButton();
        btnPayPal = new DarkButton();
        btnClose = new DarkButton();
        ((System.ComponentModel.ISupportInitialize)picAppIcon).BeginInit();
        SuspendLayout();

        picAppIcon.Location = new Point(20, 20);
        picAppIcon.Name = "picAppIcon";
        picAppIcon.Size = new Size(64, 64);
        picAppIcon.SizeMode = PictureBoxSizeMode.Zoom;
        picAppIcon.TabStop = false;

        lblTitle.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
        lblTitle.Location = new Point(100, 18);
        lblTitle.Name = "lblTitle";
        lblTitle.Size = new Size(300, 26);
        lblTitle.Text = "Pkg Viewer";

        lblVersion.Font = new Font("Segoe UI", 9F);
        lblVersion.Location = new Point(100, 46);
        lblVersion.Name = "lblVersion";
        lblVersion.Size = new Size(300, 20);
        lblVersion.Text = "Version 1.0.0";

        lblCopyright.Font = new Font("Segoe UI", 9F);
        lblCopyright.Location = new Point(100, 66);
        lblCopyright.Name = "lblCopyright";
        lblCopyright.Size = new Size(300, 20);
        lblCopyright.Text = "Copyright (c) pearlxcore";

        lblCredits.Font = new Font("Segoe UI", 9F);
        lblCredits.Location = new Point(20, 104);
        lblCredits.Name = "lblCredits";
        lblCredits.Size = new Size(380, 46);
        lblCredits.Text = "Credit to Robin Perris, SvenGDK, PSBrew, Renan Barreto,\r\nkerrdec97, strongt1me, Sony";

        lblComponents.Font = new Font("Segoe UI", 9F);
        lblComponents.Location = new Point(20, 152);
        lblComponents.Name = "lblComponents";
        lblComponents.Size = new Size(380, 20);
        lblComponents.Text = "PS4 via OrbisPkgTool; PS5 via the clean-room ProsperoPkgTool engine.";

        btnGitHub.Font = new Font("Segoe UI", 9F);
        btnGitHub.Location = new Point(20, 182);
        btnGitHub.Name = "btnGitHub";
        btnGitHub.Size = new Size(85, 30);
        btnGitHub.Text = "GitHub";
        btnGitHub.Click += btnGitHub_Click;

        btnKofi.Font = new Font("Segoe UI", 9F);
        btnKofi.Location = new Point(112, 182);
        btnKofi.Name = "btnKofi";
        btnKofi.Size = new Size(85, 30);
        btnKofi.Text = "Ko-fi";
        btnKofi.Click += btnKofi_Click;

        btnPayPal.Font = new Font("Segoe UI", 9F);
        btnPayPal.Location = new Point(204, 182);
        btnPayPal.Name = "btnPayPal";
        btnPayPal.Size = new Size(95, 30);
        btnPayPal.Text = "PayPal";
        btnPayPal.Click += btnPayPal_Click;

        btnClose.Font = new Font("Segoe UI", 9F);
        btnClose.Location = new Point(306, 182);
        btnClose.Name = "btnClose";
        btnClose.Size = new Size(85, 30);
        btnClose.Text = "Close";
        btnClose.Click += btnClose_Click;

        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(411, 224);
        Controls.Add(btnClose);
        Controls.Add(btnPayPal);
        Controls.Add(btnKofi);
        Controls.Add(btnGitHub);
        Controls.Add(lblComponents);
        Controls.Add(lblCredits);
        Controls.Add(lblCopyright);
        Controls.Add(lblVersion);
        Controls.Add(lblTitle);
        Controls.Add(picAppIcon);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "AboutForm";
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "About Pkg Viewer";
        ((System.ComponentModel.ISupportInitialize)picAppIcon).EndInit();
        ResumeLayout(false);
    }
}
