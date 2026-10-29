namespace SuperFill.Core.Geometry;

/// <summary>A closed chain of curves.</summary>
public sealed record Loop(IReadOnlyList<Curve2> Curves)
{
    /// <summary>Closed polyline with optional bulges (bulge i applies to the segment from vertex i to i+1).</summary>
    public static Loop FromPolyline(IReadOnlyList<(Vec2 P, double Bulge)> vertices)
    {
        int n = vertices.Count;
        if (n < 2) throw new ArgumentException("A loop needs at least two vertices.", nameof(vertices));
        var curves = new List<Curve2>(n);
        for (int i = 0; i < n; i++)
        {
            var (a, bulge) = vertices[i];
            Vec2 b = vertices[(i + 1) % n].P;
            if (Math.Abs(bulge) < 1e-12) curves.Add(new Segment2(a, b));
            else curves.Add(Arc2.FromBulge(a, b, bulge));
        }
        return new Loop(curves);
    }

    /// <summary>Full circle as two half arcs (CCW).</summary>
    public static Loop FromCircle(Vec2 center, double radius) =>
        new(new Curve2[]
        {
            new Arc2(center, radius, 0, Math.PI),
            new Arc2(center, radius, Math.PI, Math.PI),
        });

    /// <summary>Signed area (positive = CCW), exact for line/arc chains.</summary>
    public double SignedArea()
    {
        double area = 0;
        foreach (var c in Curves)
        {
            switch (c)
            {
                case Segment2 s:
                    area += Vec2.Cross(s.A, s.B) / 2;
                    break;
                case Arc2 a:
                    // Green's theorem for a circular arc.
                    double t0 = a.StartAngle, t1 = a.StartAngle + a.Sweep;
                    area += 0.5 * (a.Radius * a.Radius * (t1 - t0)
                        + a.Radius * (a.Center.X * (Math.Sin(t1) - Math.Sin(t0)) - a.Center.Y * (Math.Cos(t1) - Math.Cos(t0))));
                    break;
            }
        }
        return area;
    }

    public bool IsCounterClockwise => SignedArea() > 0;

    /// <summary>Same loop traversed in the opposite direction.</summary>
    public Loop Reversed()
    {
        var list = new List<Curve2>(Curves.Count);
        for (int i = Curves.Count - 1; i >= 0; i--)
        {
            list.Add(Curves[i] switch
            {
                Segment2 s => new Segment2(s.B, s.A),
                Arc2 a => new Arc2(a.Center, a.Radius, a.StartAngle + a.Sweep, -a.Sweep),
                _ => throw new NotSupportedException(),
            });
        }
        return new Loop(list);
    }
}
