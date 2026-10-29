namespace SuperFill.Core.Geometry;

/// <summary>
/// Affine 2D transform (rigid + uniform scale + optional mirror).
/// x' = A·x + C·y + Tx, y' = B·x + D·y + Ty.
/// </summary>
public readonly record struct Transform2(double A, double B, double C, double D, double Tx, double Ty)
{
    public static readonly Transform2 Identity = new(1, 0, 0, 1, 0, 0);

    public double Determinant => A * D - B * C;
    public bool IsMirror => Determinant < 0;

    public static Transform2 Translation(Vec2 t) => new(1, 0, 0, 1, t.X, t.Y);

    public static Transform2 Rotation(double radians)
    {
        double c = Math.Cos(radians), s = Math.Sin(radians);
        return new Transform2(c, s, -s, c, 0, 0);
    }

    public static Transform2 Scale(double s) => new(s, 0, 0, s, 0, 0);

    public Vec2 Apply(Vec2 p) => new(A * p.X + C * p.Y + Tx, B * p.X + D * p.Y + Ty);

    /// <summary>Transforms a direction (ignores translation).</summary>
    public Vec2 ApplyVector(Vec2 v) => new(A * v.X + C * v.Y, B * v.X + D * v.Y);

    /// <summary>Returns the transform that applies <paramref name="first"/>, then this.</summary>
    public Transform2 After(Transform2 first) => new(
        A * first.A + C * first.B, B * first.A + D * first.B,
        A * first.C + C * first.D, B * first.C + D * first.D,
        A * first.Tx + C * first.Ty + Tx, B * first.Tx + D * first.Ty + Ty);

    public Transform2 Inverse()
    {
        double det = Determinant;
        if (Math.Abs(det) < 1e-300) throw new InvalidOperationException("Transform is singular.");
        double ia = D / det, ib = -B / det, ic = -C / det, id = A / det;
        return new Transform2(ia, ib, ic, id, -(ia * Tx + ic * Ty), -(ib * Tx + id * Ty));
    }
}
