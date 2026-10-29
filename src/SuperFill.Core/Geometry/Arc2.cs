namespace SuperFill.Core.Geometry;

/// <summary>Circular arc. Sweep is signed: positive = counter-clockwise.</summary>
public sealed record Arc2(Vec2 Center, double Radius, double StartAngle, double Sweep) : Curve2
{
    public override Vec2 Start => Center + Vec2.FromAngle(StartAngle, Radius);
    public override Vec2 End => Center + Vec2.FromAngle(StartAngle + Sweep, Radius);
    public override double Length => Math.Abs(Sweep) * Radius;
    public override Vec2 PointAt(double t) => Center + Vec2.FromAngle(StartAngle + Sweep * t, Radius);

    /// <summary>Bulge = tan(sweep / 4), AutoCAD convention.</summary>
    public double Bulge => Math.Tan(Sweep / 4);

    /// <summary>Arc from <paramref name="a"/> to <paramref name="b"/> with the given bulge.</summary>
    public static Arc2 FromBulge(Vec2 a, Vec2 b, double bulge)
    {
        double sweep = 4 * Math.Atan(bulge);
        Vec2 chord = b - a;
        double c = chord.Length;
        double radius = c / (2 * Math.Sin(Math.Abs(sweep) / 2));
        // Center lies left of a→b for CCW arcs (bulge > 0) and right of it for CW arcs.
        double h = radius * Math.Cos(sweep / 2);
        Vec2 center = (a + b) / 2 + chord.Perp().Normalized() * h * Math.Sign(sweep);
        double start = Math.Atan2(a.Y - center.Y, a.X - center.X);
        return new Arc2(center, radius, start, sweep);
    }
}
