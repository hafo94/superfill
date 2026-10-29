namespace SuperFill.Core.Debug;

public readonly record struct DebugColor(byte R, byte G, byte B, byte A = 255)
{
    public static readonly DebugColor Black = new(0, 0, 0);
    public static readonly DebugColor White = new(255, 255, 255);
    public static readonly DebugColor Gray = new(140, 140, 140);
    public static readonly DebugColor Red = new(214, 39, 40);
    public static readonly DebugColor Blue = new(31, 119, 180);
    public static readonly DebugColor Green = new(44, 160, 44);
    public static readonly DebugColor LightBlue = new(31, 119, 180, 40);

    public string ToSvgRgb() => $"rgb({R},{G},{B})";
    public double Opacity => A / 255.0;
}
