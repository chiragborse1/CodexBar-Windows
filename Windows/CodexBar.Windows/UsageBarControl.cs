using System.Drawing.Drawing2D;

namespace CodexBar.Windows;

internal sealed class UsageBarControl : Control
{
    private double usedPercent;
    private Color accentColor = Color.FromArgb(24, 132, 88);

    public UsageBarControl()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.UserPaint,
            true);
        Height = 8;
        Margin = new Padding(0, 3, 0, 4);
    }

    public double UsedPercent
    {
        get => usedPercent;
        set
        {
            usedPercent = Math.Clamp(value, 0, 100);
            Invalidate();
        }
    }

    public Color AccentColor
    {
        get => accentColor;
        set
        {
            accentColor = value;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = ClientRectangle;
        rect.Width -= 1;
        rect.Height -= 1;
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        using var track = RoundedPath(rect, rect.Height / 2);
        using var trackBrush = new SolidBrush(Color.FromArgb(229, 232, 238));
        e.Graphics.FillPath(trackBrush, track);

        var fillWidth = (int)Math.Round(rect.Width * (usedPercent / 100D));
        if (fillWidth <= 0)
        {
            return;
        }

        var fillRect = new Rectangle(rect.X, rect.Y, Math.Max(rect.Height, fillWidth), rect.Height);
        fillRect.Width = Math.Min(fillRect.Width, rect.Width);
        using var fill = RoundedPath(fillRect, rect.Height / 2);
        using var fillBrush = new SolidBrush(accentColor);
        e.Graphics.FillPath(fillBrush, fill);
    }

    private static GraphicsPath RoundedPath(Rectangle rect, int radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        if (diameter <= 0)
        {
            path.AddRectangle(rect);
            path.CloseFigure();
            return path;
        }

        path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
