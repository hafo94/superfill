using System.Globalization;
using System.Text;

namespace SuperFill.Core.Debug;

public static class SvgWriter
{
    /// <summary>Renders the scene to SVG; world Y points up, so the output is flipped.</summary>
    public static string Render(DebugScene scene, int widthPx = 1200, int marginPx = 20)
    {
        var (min, max) = scene.Bounds();
        double w = Math.Max(max.X - min.X, 1e-9), h = Math.Max(max.Y - min.Y, 1e-9);
        double scale = (widthPx - 2 * marginPx) / w;
        int heightPx = (int)Math.Ceiling(h * scale) + 2 * marginPx;
        var ci = CultureInfo.InvariantCulture;
        string X(double x) => ((x - min.X) * scale + marginPx).ToString("0.##", ci);
        string Y(double y) => ((max.Y - y) * scale + marginPx).ToString("0.##", ci);

        var sb = new StringBuilder();
        sb.Append(ci, $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{widthPx}\" height=\"{heightPx}\" viewBox=\"0 0 {widthPx} {heightPx}\">\n");
        sb.Append("<rect width=\"100%\" height=\"100%\" fill=\"white\"/>\n");
        foreach (var p in scene.Polygons)
        {
            sb.Append(p.Closed ? "<polygon" : "<polyline").Append(" points=\"");
            foreach (var pt in p.Points) sb.Append(X(pt.X)).Append(',').Append(Y(pt.Y)).Append(' ');
            string fill = p.Fill is { } f ? f.ToSvgRgb() : "none";
            string fillOpacity = p.Fill is { } f2 ? f2.Opacity.ToString("0.##", ci) : "1";
            sb.Append(ci, $"\" fill=\"{fill}\" fill-opacity=\"{fillOpacity}\" stroke=\"{p.Stroke.ToSvgRgb()}\" stroke-width=\"{p.StrokeWidth.ToString("0.##", ci)}\"/>\n");
        }
        foreach (var l in scene.Labels)
            sb.Append(ci, $"<text x=\"{X(l.At.X)}\" y=\"{Y(l.At.Y)}\" font-size=\"12\" fill=\"{l.Color.ToSvgRgb()}\">{System.Security.SecurityElement.Escape(l.Text)}</text>\n");
        sb.Append("</svg>\n");
        return sb.ToString();
    }
}
