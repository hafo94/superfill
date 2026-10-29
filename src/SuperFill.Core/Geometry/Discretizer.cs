namespace SuperFill.Core.Geometry;

public static class Discretizer
{
    /// <summary>Segment count for an arc so the chord error stays below <paramref name="chordError"/> (ALGORITHMS §1).</summary>
    public static int ArcSegmentCount(double radius, double sweep, double chordError)
    {
        double abs = Math.Abs(sweep);
        if (radius <= chordError) return Math.Max(4, (int)Math.Ceiling(abs / (Math.PI / 8)));
        double step = 2 * Math.Acos(1 - chordError / radius);
        return Math.Max(1, (int)Math.Ceiling(abs / step));
    }

    /// <summary>Closed polygon (no repeated end point) approximating the loop.</summary>
    public static List<Vec2> ToPolygon(Loop loop, double chordError)
    {
        var pts = new List<Vec2>();
        foreach (var c in loop.Curves)
        {
            pts.Add(c.Start);
            if (c is Arc2 a)
            {
                int n = ArcSegmentCount(a.Radius, a.Sweep, chordError);
                for (int i = 1; i < n; i++) pts.Add(a.PointAt((double)i / n));
            }
        }
        return pts;
    }
}
