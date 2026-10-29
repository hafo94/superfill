using SuperFill.Core.Geometry;

namespace SuperFill.Core.Debug;

/// <summary>Closed polygon (Fill == null for outline only) or open polyline.</summary>
public sealed record DebugPolygon(IReadOnlyList<Vec2> Points, bool Closed, DebugColor Stroke, DebugColor? Fill = null, double StrokeWidth = 1);
