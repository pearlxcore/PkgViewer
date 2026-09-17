using System.Drawing.Drawing2D;

namespace PkgViewer.Forms;

/// <summary>
/// Small generated 16x16 file/folder icons for the File Browser, matching the PS4 viewer's
/// image-list indices: folder, document, image, config, binary, folder-open, audio, unknown,
/// package, video, code.
/// </summary>
internal static class FileIcons
{
    public const int Folder = 0;
    public const int Document = 1;
    public const int Image = 2;
    public const int Config = 3;
    public const int Binary = 4;
    public const int FolderOpen = 5;
    public const int Audio = 6;
    public const int Unknown = 7;
    public const int Package = 8;
    public const int Video = 9;
    public const int Code = 10;

    public static ImageList Create()
    {
        var list = new ImageList
        {
            ColorDepth = ColorDepth.Depth32Bit,
            ImageSize = new Size(16, 16),
            TransparentColor = Color.Transparent
        };
        Populate(list);
        return list;
    }

    /// <summary>Adds the generated icons to an ImageList created by the designer.</summary>
    public static void Populate(ImageList list)
    {
        list.Images.Add("folder", DrawFolder(open: false));
        list.Images.Add("document", DrawPage(Color.FromArgb(245, 245, 245), DocumentOverlay));
        list.Images.Add("image", DrawPage(Color.FromArgb(235, 245, 255), ImageOverlay));
        list.Images.Add("config", DrawPage(Color.FromArgb(245, 245, 235), ConfigOverlay));
        list.Images.Add("binary", DrawPage(Color.FromArgb(230, 230, 230), (g, b) => DrawGlyph(g, b, "01", Color.FromArgb(110, 110, 110))));
        list.Images.Add("folder-open", DrawFolder(open: true));
        list.Images.Add("audio", DrawPage(Color.FromArgb(245, 235, 250), AudioOverlay));
        list.Images.Add("unknown", DrawPage(Color.FromArgb(235, 235, 235), (g, b) => DrawGlyph(g, b, "?", Color.FromArgb(120, 120, 120))));
        list.Images.Add("package", DrawPackage());
        list.Images.Add("video", DrawPage(Color.FromArgb(235, 238, 250), VideoOverlay));
        list.Images.Add("code", DrawPage(Color.FromArgb(235, 245, 235), (g, b) => DrawGlyph(g, b, "<>", Color.FromArgb(40, 120, 60))));
    }

    public static int IconFor(string name)
    {
        string extension = Path.GetExtension(name).ToLowerInvariant();
        if (extension.Length == 0) return Binary;
        return extension switch
        {
            ".png" or ".jpg" or ".jpeg" or ".dds" or ".bmp" or ".gif" => Image,
            ".txt" => Document,
            ".xml" or ".json" or ".sfo" or ".ini" or ".cfg" or ".dat" => Config,
            ".at9" or ".ogg" or ".mp3" or ".wav" => Audio,
            ".mp4" or ".avi" or ".mkv" => Video,
            ".pkg" => Package,
            ".cs" or ".js" or ".h" or ".c" or ".cpp" or ".py" or ".lua" => Code,
            _ => Binary
        };
    }

    private static Bitmap Canvas() => new(16, 16, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

    private static Graphics Begin(Bitmap bitmap)
    {
        Graphics graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);
        return graphics;
    }

    private static Bitmap DrawFolder(bool open)
    {
        Bitmap bitmap = Canvas();
        using Graphics graphics = Begin(bitmap);
        Color body = open ? Color.FromArgb(255, 216, 150) : Color.FromArgb(255, 196, 100);
        using var brush = new SolidBrush(body);
        using var pen = new Pen(Color.FromArgb(170, 115, 25));

        graphics.FillRectangle(brush, 1, 3, 6, 3);
        graphics.FillRectangle(brush, 1, 5, 14, 9);
        graphics.DrawRectangle(pen, 1, 3, 6, 3);
        graphics.DrawRectangle(pen, 1, 5, 13, 8);
        if (open)
        {
            using var highlight = new SolidBrush(Color.FromArgb(255, 238, 195));
            graphics.FillRectangle(highlight, 2, 7, 11, 5);
        }
        return bitmap;
    }

    private static Bitmap DrawPage(Color page, Action<Graphics, Rectangle> overlay)
    {
        Bitmap bitmap = Canvas();
        using Graphics graphics = Begin(bitmap);
        using var brush = new SolidBrush(page);
        using var pen = new Pen(Color.FromArgb(120, 120, 120));
        var body = new[]
        {
            new Point(3, 1), new Point(10, 1), new Point(13, 4), new Point(13, 14), new Point(3, 14)
        };
        graphics.FillPolygon(brush, body);
        graphics.DrawPolygon(pen, body);
        using (var fold = new SolidBrush(Color.FromArgb(205, 205, 205)))
            graphics.FillPolygon(fold, [new Point(10, 1), new Point(13, 4), new Point(10, 4)]);

        overlay(graphics, new Rectangle(3, 3, 10, 11));
        return bitmap;
    }

    private static void DocumentOverlay(Graphics graphics, Rectangle bounds)
    {
        using var pen = new Pen(Color.FromArgb(120, 120, 120));
        for (int index = 0; index < 4; index++)
        {
            int y = bounds.Top + 2 + index * 2;
            graphics.DrawLine(pen, bounds.Left + 1, y, bounds.Right - 2, y);
        }
    }

    private static void ImageOverlay(Graphics graphics, Rectangle bounds)
    {
        using var sky = new SolidBrush(Color.FromArgb(120, 190, 235));
        graphics.FillRectangle(sky, bounds.Left + 1, bounds.Top + 1, bounds.Width - 2, bounds.Height - 3);
        using var mountain = new SolidBrush(Color.FromArgb(80, 150, 90));
        graphics.FillPolygon(mountain,
        [
            new Point(bounds.Left + 1, bounds.Bottom - 2),
            new Point(bounds.Left + 4, bounds.Top + 4),
            new Point(bounds.Left + 7, bounds.Bottom - 2)
        ]);
        using var sun = new SolidBrush(Color.FromArgb(245, 205, 70));
        graphics.FillEllipse(sun, bounds.Right - 5, bounds.Top + 1, 3, 3);
    }

    private static void ConfigOverlay(Graphics graphics, Rectangle bounds)
    {
        int cx = bounds.Left + bounds.Width / 2;
        int cy = bounds.Top + bounds.Height / 2 - 1;
        using var brush = new SolidBrush(Color.FromArgb(215, 150, 40));
        graphics.FillEllipse(brush, cx - 3, cy - 3, 6, 6);
        using var center = new SolidBrush(Color.FromArgb(245, 245, 235));
        graphics.FillEllipse(center, cx - 1, cy - 1, 2, 2);
    }

    private static void AudioOverlay(Graphics graphics, Rectangle bounds)
    {
        using var brush = new SolidBrush(Color.FromArgb(150, 90, 190));
        int cx = bounds.Left + bounds.Width / 2;
        int cy = bounds.Top + bounds.Height / 2;
        graphics.FillEllipse(brush, cx - 3, cy, 4, 4);
        graphics.FillRectangle(brush, cx + 1, cy - 4, 1, 5);
        graphics.FillRectangle(brush, cx + 1, cy - 4, 3, 1);
    }

    private static void VideoOverlay(Graphics graphics, Rectangle bounds)
    {
        using var brush = new SolidBrush(Color.FromArgb(70, 90, 180));
        int cx = bounds.Left + bounds.Width / 2;
        int cy = bounds.Top + bounds.Height / 2;
        graphics.FillPolygon(brush,
        [
            new Point(cx - 2, cy - 3), new Point(cx + 3, cy), new Point(cx - 2, cy + 3)
        ]);
    }

    private static Bitmap DrawPackage()
    {
        Bitmap bitmap = Canvas();
        using Graphics graphics = Begin(bitmap);
        using var brush = new SolidBrush(Color.FromArgb(200, 150, 90));
        using var pen = new Pen(Color.FromArgb(140, 95, 40));
        graphics.FillRectangle(brush, 2, 4, 12, 9);
        graphics.DrawRectangle(pen, 2, 4, 12, 9);
        graphics.DrawLine(pen, 2, 7, 14, 7);
        graphics.DrawLine(pen, 8, 4, 8, 13);
        return bitmap;
    }

    private static void DrawGlyph(Graphics graphics, Rectangle bounds, string text, Color color)
    {
        using var font = new Font("Segoe UI", 6F, FontStyle.Bold, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(color);
        var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        graphics.DrawString(text, font, brush, bounds, format);
    }
}
