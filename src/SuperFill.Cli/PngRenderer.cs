using SkiaSharp;
using SuperFill.Core.Debug;
using SuperFill.Core.Geometry;

namespace SuperFill.Cli;

public static class PngRenderer
{
    public static void Render(DebugScene scene, string path, int widthPx = 1200, int marginPx = 20)
    {
        var (min, max) = scene.Bounds();
        double w = Math.Max(max.X - min.X, 1e-9), h = Math.Max(max.Y - min.Y, 1e-9);
        double scale = (widthPx - 2 * marginPx) / w;
        int heightPx = (int)Math.Ceiling(h * scale) + 2 * marginPx;
        SKPoint P(Vec2 v) => new((float)((v.X - min.X) * scale + marginPx), (float)((max.Y - v.Y) * scale + marginPx));
        static SKColor C(DebugColor c) => new(c.R, c.G, c.B, c.A);

        using var surface = SKSurface.Create(new SKImageInfo(widthPx, heightPx));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);
        foreach (var poly in scene.Polygons)
        {
            using var path2 = new SKPath();
            for (int i = 0; i < poly.Points.Count; i++)
            {
                var pt = P(poly.Points[i]);
                if (i == 0) path2.MoveTo(pt); else path2.LineTo(pt);
            }
            if (poly.Closed) path2.Close();
            if (poly.Fill is { } f)
            {
                using var fill = new SKPaint { Style = SKPaintStyle.Fill, Color = C(f), IsAntialias = true };
                canvas.DrawPath(path2, fill);
            }
            using var stroke = new SKPaint { Style = SKPaintStyle.Stroke, Color = C(poly.Stroke), StrokeWidth = (float)poly.StrokeWidth, IsAntialias = true };
            canvas.DrawPath(path2, stroke);
        }
        foreach (var l in scene.Labels)
        {
            using var paint = new SKPaint { Color = C(l.Color), TextSize = 14, IsAntialias = true };
            canvas.DrawText(l.Text, P(l.At), paint);
        }
        using var img = surface.Snapshot();
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        using var fs = File.Create(path);
        data.SaveTo(fs);
    }
}
