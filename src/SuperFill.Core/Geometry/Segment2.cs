namespace SuperFill.Core.Geometry;

public sealed record Segment2(Vec2 A, Vec2 B) : Curve2
{
    public override Vec2 Start => A;
    public override Vec2 End => B;
    public override double Length => Vec2.Distance(A, B);
    public override Vec2 PointAt(double t) => Vec2.Lerp(A, B, t);
}
