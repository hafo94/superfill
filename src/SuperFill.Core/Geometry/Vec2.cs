namespace SuperFill.Core.Geometry;

public readonly record struct Vec2(double X, double Y)
{
    public static readonly Vec2 Zero = new(0, 0);

    public double Length => Math.Sqrt(X * X + Y * Y);
    public double LengthSquared => X * X + Y * Y;

    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec2 operator -(Vec2 a) => new(-a.X, -a.Y);
    public static Vec2 operator *(Vec2 a, double s) => new(a.X * s, a.Y * s);
    public static Vec2 operator *(double s, Vec2 a) => new(a.X * s, a.Y * s);
    public static Vec2 operator /(Vec2 a, double s) => new(a.X / s, a.Y / s);

    public static double Dot(Vec2 a, Vec2 b) => a.X * b.X + a.Y * b.Y;
    public static double Cross(Vec2 a, Vec2 b) => a.X * b.Y - a.Y * b.X;
    public static double Distance(Vec2 a, Vec2 b) => (a - b).Length;

    /// <summary>Rotated 90° counter-clockwise.</summary>
    public Vec2 Perp() => new(-Y, X);

    public Vec2 Normalized()
    {
        double l = Length;
        return l > 0 ? new Vec2(X / l, Y / l) : Zero;
    }

    public static Vec2 FromAngle(double radians, double length = 1) =>
        new(Math.Cos(radians) * length, Math.Sin(radians) * length);

    public static Vec2 Lerp(Vec2 a, Vec2 b, double t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

    public bool NearlyEquals(Vec2 other, double epsilon) =>
        Math.Abs(X - other.X) <= epsilon && Math.Abs(Y - other.Y) <= epsilon;
}
