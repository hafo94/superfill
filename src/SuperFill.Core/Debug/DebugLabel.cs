using SuperFill.Core.Geometry;

namespace SuperFill.Core.Debug;

public sealed record DebugLabel(Vec2 At, string Text, DebugColor Color);
