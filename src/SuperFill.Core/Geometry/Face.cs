namespace SuperFill.Core.Geometry;

/// <summary>One outer loop (CCW) with its holes (CW).</summary>
public sealed record Face(Loop Outer, IReadOnlyList<Loop> Holes);
