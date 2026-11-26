using System.Windows;
using System.Windows.Media;
using PCManager.Core.Design;

namespace PCManager.Controls;

/// <summary>
/// Composes the U2 layer recipe once per (icon, tint, theme, detail level):
/// soft shadow copies, extrusion copies 1 px down-right, gradient face, specular highlight
/// clipped to the face, and a 1 px rim on the top-left edge. One light source, upper-left.
///
/// Colours come from the theme dictionaries (Icon.* token brushes), so the icons follow
/// Light/Dark without duplicating the palette here.
/// </summary>
internal static class Icon3DRenderer
{
    private const double Grid = 24d;

    private static readonly Dictionary<string, DrawingImage?> Cache = new(StringComparer.Ordinal);
    private static readonly object Gate = new();

    public static DrawingImage? Get(string name, IconSizeClass sizeClass, Icon3DTint tint, bool dark)
    {
        string key = $"{name}|{sizeClass}|{tint}|{(dark ? "dark" : "light")}";

        lock (Gate)
        {
            if (Cache.TryGetValue(key, out DrawingImage? cached))
            {
                return cached;
            }

            DrawingImage? built = Build(name, sizeClass, tint);
            Cache[key] = built;
            return built;
        }
    }

    public static void ClearCache()
    {
        lock (Gate)
        {
            Cache.Clear();
        }
    }

    private static DrawingImage? Build(string name, IconSizeClass sizeClass, Icon3DTint tint)
    {
        if (GeometryResource(IconMetrics.GeometryKey(name, "Face")) is not Geometry face)
        {
            return null;
        }

        Geometry? neutral = GeometryResource(IconMetrics.GeometryKey(name, "Neutral"));
        Palette palette = Palette.For(tint);

        var silhouette = new GeometryGroup { FillRule = FillRule.Nonzero };
        silhouette.Children.Add(face);

        if (neutral is not null)
        {
            silhouette.Children.Add(neutral);
        }

        silhouette.Freeze();

        var group = new DrawingGroup();

        // 1. Soft shadow: two low-opacity offset silhouettes (no DropShadowEffect, which would
        //    force a software render pass on every frame).
        if (sizeClass == IconSizeClass.Large)
        {
            group.Children.Add(Offset(silhouette, 1.15, 1.95, Solid(palette.Shadow, 0.30)));
            group.Children.Add(Offset(silhouette, 0.5, 0.95, Solid(palette.Shadow, 0.18)));
        }

        // 2. Extrusion: 2 copies at Medium, 4 at Large, each a step darker.
        int depth = sizeClass switch
        {
            IconSizeClass.Small => 0,
            IconSizeClass.Medium => 2,
            _ => 4
        };

        double step = 0.42;
        for (int i = depth; i >= 1; i--)
        {
            group.Children.Add(Offset(silhouette, step * i, step * i * 1.15, Solid(palette.Depth, 1)));
        }

        // 3. Face: one linear gradient for the tinted parts, graphite for secondary parts.
        group.Children.Add(new GeometryDrawing(palette.Face, null, face));

        if (neutral is not null)
        {
            group.Children.Add(new GeometryDrawing(Palette.NeutralFace, null, neutral));
        }

        // 4. Specular highlight, clipped to the face so it cannot spill past the silhouette.
        if (sizeClass != IconSizeClass.Small)
        {
            group.Children.Add(Specular(face));

            if (neutral is not null)
            {
                group.Children.Add(Specular(neutral));
            }
        }

        // 5. Rim light on the top-left edge.
        if (sizeClass == IconSizeClass.Large)
        {
            group.Children.Add(new GeometryDrawing(null, new Pen(Solid(palette.Rim, 0.55), 0.45), face));
        }

        group.Freeze();

        var image = new DrawingImage(group);
        image.Freeze();
        return image;
    }

    private static Geometry? GeometryResource(string key) => Application.Current?.TryFindResource(key) as Geometry;

    private static Brush? BrushResource(string key) => Application.Current?.TryFindResource(key) as Brush;

    private static DrawingGroup Offset(Geometry geometry, double x, double y, Brush brush)
    {
        var group = new DrawingGroup { Transform = new TranslateTransform(x, y) };
        group.Children.Add(new GeometryDrawing(brush, null, geometry));
        group.Freeze();
        return group;
    }

    private static DrawingGroup Specular(Geometry geometry)
    {
        var gradient = new LinearGradientBrush
        {
            StartPoint = new Point(0.05, 0),
            EndPoint = new Point(0.65, 1)
        };
        gradient.GradientStops.Add(new GradientStop(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF), 0));
        gradient.GradientStops.Add(new GradientStop(Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF), 0.55));
        gradient.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF), 1));
        gradient.Freeze();

        var group = new DrawingGroup { ClipGeometry = geometry };
        group.Children.Add(new GeometryDrawing(gradient, null, new RectangleGeometry(new Rect(0, 0, Grid, Grid))));
        group.Freeze();
        return group;
    }

    private static SolidColorBrush Solid(Color color, double opacity)
    {
        var brush = new SolidColorBrush(color) { Opacity = opacity };
        brush.Freeze();
        return brush;
    }

    private readonly record struct Palette(Brush Face, Color Depth, Color Rim, Color Shadow)
    {
        public static Brush NeutralFace { get; } = Gradient("Icon.Neutral.FaceFrom", "Icon.Neutral.FaceTo");

        public static Palette For(Icon3DTint tint)
        {
            string prefix = tint switch
            {
                Icon3DTint.Neutral => "Icon.Neutral",
                Icon3DTint.Success => "Icon.Success",
                Icon3DTint.Warning => "Icon.Warning",
                Icon3DTint.Critical => "Icon.Critical",
                _ => "Icon.Gold"
            };

            Color from = ColorOf($"{prefix}.FaceFrom", Colors.Gray);
            Color to = ColorOf($"{prefix}.FaceTo", Colors.DimGray);

            return new Palette(
                Gradient(from, to),
                ColorOf($"{prefix}.Depth", Colors.Black),
                Blend(from, Colors.White, 0.55),
                ColorOf("Icon.Shadow", Colors.Black));
        }

        private static Brush Gradient(string fromKey, string toKey) =>
            Gradient(ColorOf(fromKey, Colors.Gray), ColorOf(toKey, Colors.DimGray));

        private static LinearGradientBrush Gradient(Color from, Color to)
        {
            var brush = new LinearGradientBrush
            {
                StartPoint = new Point(0.15, 0),
                EndPoint = new Point(0.85, 1)
            };
            brush.GradientStops.Add(new GradientStop(from, 0));
            brush.GradientStops.Add(new GradientStop(to, 1));
            brush.Freeze();
            return brush;
        }

        private static Color ColorOf(string key, Color fallback) =>
            BrushResource(key) is SolidColorBrush brush ? brush.Color : fallback;

        private static Color Blend(Color a, Color b, double amount) => Color.FromRgb(
            (byte)(a.R + ((b.R - a.R) * amount)),
            (byte)(a.G + ((b.G - a.G) * amount)),
            (byte)(a.B + ((b.B - a.B) * amount)));
    }
}
