using DarkUI.Config;
using DarkUI.Controls;

namespace PkgViewer.Forms;

/// <summary>
/// A themed, scrollable caption/value list used by the Package Summary. Hosting the rows in a
/// <see cref="DarkScrollView"/> gives DarkUI's themed scroll bar; a stock Panel would show the
/// default (light) OS scroll bar.
/// </summary>
internal sealed class OverviewSummaryView : DarkScrollView
{
    private const int RowHeight = 22;
    private const int CaptionWidth = 140;
    private const int LeftPad = 4;

    private readonly List<(string Caption, string Value)> _rows = [];
    private readonly Font _captionFont = new("Segoe UI", 9F);
    private readonly Font _valueFont = new("Segoe UI", 9F);

    /// <summary>Raised on double-click with the row's caption and value, for copy-to-clipboard.</summary>
    public event Action<string, string>? ValueCopyRequested;

    public void ClearRows()
    {
        _rows.Clear();
        UpdateContentSize();
        Invalidate();
    }

    /// <summary>Adds a row, or updates it in place when the caption already exists.</summary>
    public void SetRow(string caption, string value)
    {
        int index = _rows.FindIndex(row => string.Equals(row.Caption, caption, StringComparison.Ordinal));
        if (index >= 0) _rows[index] = (caption, value);
        else _rows.Add((caption, value));
        UpdateContentSize();
        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateContentSize();
    }

    // Width 0 means "no horizontal scrolling"; height drives the vertical scroll bar.
    private void UpdateContentSize() => ContentSize = new Size(0, _rows.Count * RowHeight);

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        UpdateContentSize();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible) UpdateContentSize();
    }

    protected override void PaintContent(Graphics g)
    {
        // TextRenderer uses GDI and ignores the graphics transform, so the viewport offset is applied
        // to the row Y position directly.
        for (int index = 0; index < _rows.Count; index++)
        {
            int y = (index * RowHeight) - Viewport.Top;
            if (y + RowHeight < 0 || y > ClientSize.Height) continue;

            (string caption, string value) = _rows[index];
            TextRenderer.DrawText(g, caption, _captionFont, new Point(LeftPad, y + (RowHeight - _captionFont.Height) / 2),
                Colors.LightText, TextFormatFlags.NoPrefix);
            // Viewport.Width already excludes the vertical scroll bar when it is visible.
            var valueBounds = new Rectangle(CaptionWidth, y,
                Math.Max(0, Viewport.Width - CaptionWidth - LeftPad - 2), RowHeight);
            TextRenderer.DrawText(g, value, _valueFont, valueBounds, Colors.LightText,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        int index = (e.Y + Viewport.Top) / RowHeight;
        if (index >= 0 && index < _rows.Count)
            ValueCopyRequested?.Invoke(_rows[index].Caption, _rows[index].Value);
    }
}
