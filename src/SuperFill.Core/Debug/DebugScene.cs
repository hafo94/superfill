using SuperFill.Core.Geometry;

namespace SuperFill.Core.Debug;

/// <summary>Collects polygons, lines and labels for rendering to PNG/SVG during development.</summary>
public sealed class DebugScene
{
    private readonly List<DebugPolygon> _polygons = new();
    private readonly List<DebugLabel> _labels = new();

    public IReadOnlyList<DebugPolygon> Polygons => _polygons;
    public IReadOnlyList<DebugLabel> Labels => _labels;

    public void AddPolygon(IReadOnlyList<Vec2> points, DebugColor stroke, DebugColor? fill = null, double strokeWidth = 1) =>
        _polygons.Add(new DebugPolygon(points, true, stroke, fill, strokeWidth));

    public void AddPolyline(IReadOnlyList<Vec2> points, DebugColor stroke, double strokeWidth = 1) =>
        _polygons.Add(new DebugPolygon(points, false, stroke, null, strokeWidth));

    public void AddLine(Vec2 a, Vec2 b, DebugColor stroke, double strokeWidth = 1) =>
        AddPolyline(new[] { a, b }, stroke, strokeWidth);

    public void AddLabel(Vec2 at, string text, DebugColor color) => _labels.Add(new DebugLabel(at, text, color));

    /// <summary>Adds a region: faces filled lightly, loops outlined. Holes use <paramref name="holeStroke"/>.</summary>
    public void AddRegion(Region region, Tolerances tol, DebugColor stroke, DebugColor fill, DebugColor holeStroke)
    {
        foreach (var f in region.Faces)
        {
            AddPolygon(Discretizer.ToPolygon(f.Outer, tol.ChordError), stroke, fill, 1.5);
            foreach (var h in f.Holes)
                AddPolygon(Discretizer.ToPolygon(h, tol.ChordError), holeStroke, DebugColor.White, 1.5);
        }
    }

    public (Vec2 Min, Vec2 Max) Bounds()
    {
        double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
        foreach (var p in _polygons.SelectMany(p => p.Points).Concat(_labels.Select(l => l.At)))
        {
            x0 = Math.Min(x0, p.X); y0 = Math.Min(y0, p.Y);
            x1 = Math.Max(x1, p.X); y1 = Math.Max(y1, p.Y);
        }
        if (x0 > x1) return (Vec2.Zero, new Vec2(1, 1));
        return (new Vec2(x0, y0), new Vec2(x1, y1));
    }
}
