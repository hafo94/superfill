namespace SuperFill.Core.Geometry;

/// <summary>Exact boundary geometry: faces with holes. Outer loops CCW, holes CW (see <see cref="Normalize"/>).</summary>
public sealed record Region(IReadOnlyList<Face> Faces)
{
    public double Area
    {
        get
        {
            double a = 0;
            foreach (var f in Faces)
            {
                a += Math.Abs(f.Outer.SignedArea());
                foreach (var h in f.Holes) a -= Math.Abs(h.SignedArea());
            }
            return a;
        }
    }

    /// <summary>Builds a region from an outer loop and holes, fixing orientation.</summary>
    public static Region Normalize(IEnumerable<Loop> outers, IEnumerable<Loop> holes)
    {
        var o = outers.Select(l => l.IsCounterClockwise ? l : l.Reversed()).ToList();
        var h = holes.Select(l => l.IsCounterClockwise ? l.Reversed() : l).ToList();
        if (o.Count == 0) throw new ArgumentException("A region needs at least one outer loop.");
        // Assign each hole to the first outer loop (multi-outer containment is resolved in M1 with Clipper2).
        var faces = new List<Face>();
        for (int i = 0; i < o.Count; i++) faces.Add(new Face(o[i], i == 0 ? h : Array.Empty<Loop>()));
        return new Region(faces);
    }
}
