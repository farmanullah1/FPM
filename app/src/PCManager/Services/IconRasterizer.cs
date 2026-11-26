using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PCManager.Controls;
using PCManager.Core.Design;

namespace PCManager.Services;

/// <summary>
/// The tray is a WinForms NotifyIcon, which cannot host WPF drawing. This renders the same
/// Icon3D drawing into a small bitmap so the tray menu uses one icon system, not a second one.
/// </summary>
internal static class IconRasterizer
{
    public static System.Drawing.Bitmap? ToBitmap(string key, int size, Icon3DTint tint = Icon3DTint.Gold)
    {
        DrawingImage? drawing = Icon3DRenderer.Get(IconMetrics.NormalizeKey(key), IconMetrics.ForSize(size), tint, Icon3D.IsDarkTheme);

        if (drawing is null)
        {
            return null;
        }

        var target = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();

        using (DrawingContext context = visual.RenderOpen())
        {
            context.DrawImage(drawing, new Rect(0, 0, size, size));
        }

        target.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(target));

        using var stream = new MemoryStream();
        encoder.Save(stream);
        stream.Position = 0;

        // Image.FromStream keeps the stream alive for the image's lifetime, so decode and copy.
        using var decoded = System.Drawing.Image.FromStream(stream);
        return new System.Drawing.Bitmap(decoded);
    }
}
