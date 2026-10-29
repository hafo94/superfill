namespace SuperFill.Core.Geometry;

/// <summary>A boundary curve: a line segment or a circular arc.</summary>
public abstract record Curve2
{
    public abstract Vec2 Start { get; }
    public abstract Vec2 End { get; }
    public abstract double Length { get; }
    /// <summary>Point at parameter t in [0, 1] along the curve.</summary>
    public abstract Vec2 PointAt(double t);
}
