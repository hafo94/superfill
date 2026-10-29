namespace SuperFill.Core.Geometry;

/// <summary>
/// Geometric tolerances in drawing units. Defaults are defined in millimetres (ALGORITHMS §0)
/// and converted with <see cref="FromMillimetres"/>.
/// </summary>
public readonly record struct Tolerances(
    double Epsilon,
    double ChordError,
    double ClipperResolution,
    double HashResolution,
    double FullInsideFraction)
{
    public const double DefaultEpsilonMm = 0.001;
    public const double DefaultChordErrorMm = 0.5;
    public const double DefaultClipperResolutionMm = 0.01;
    public const double DefaultHashResolutionMm = 0.01;
    public const double DefaultFullInsideFraction = 0.9999;

    /// <param name="unitsPerMm">Drawing units per millimetre (0.001 for metres).</param>
    public static Tolerances FromMillimetres(double unitsPerMm) => new(
        DefaultEpsilonMm * unitsPerMm,
        DefaultChordErrorMm * unitsPerMm,
        DefaultClipperResolutionMm * unitsPerMm,
        DefaultHashResolutionMm * unitsPerMm,
        DefaultFullInsideFraction);

    public static Tolerances Metres => FromMillimetres(0.001);
    public static Tolerances Millimetres => FromMillimetres(1);
}
