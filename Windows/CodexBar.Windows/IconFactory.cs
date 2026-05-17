using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace CodexBar.Windows;

internal static class IconFactory
{
    public static Icon CreateTrayIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        using var background = new LinearGradientBrush(
            new Rectangle(0, 0, 32, 32),
            Color.FromArgb(26, 115, 232),
            Color.FromArgb(18, 179, 122),
            LinearGradientMode.ForwardDiagonal);
        graphics.FillEllipse(background, 2, 2, 28, 28);

        using var border = new Pen(Color.FromArgb(245, 255, 255, 255), 2);
        graphics.DrawEllipse(border, 2, 2, 28, 28);

        using var font = new Font("Segoe UI", 15, FontStyle.Bold, GraphicsUnit.Pixel);
        using var textBrush = new SolidBrush(Color.White);
        var textSize = graphics.MeasureString("C", font);
        graphics.DrawString("C", font, textBrush, (32 - textSize.Width) / 2, (32 - textSize.Height) / 2 - 1);

        var handle = bitmap.GetHicon();
        try
        {
            using var icon = Icon.FromHandle(handle);
            return (Icon)icon.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
