using System.Text.Json;
using SuperFill.Core.Geometry;

namespace SuperFill.Core.Serialization;

/// <summary>Reads sample boundary files (TESTING §4). Coordinates are in the file's units, kept as-is.</summary>
public static class BoundaryJson
{
    public static Region Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var outers = new List<Loop>();
        var holes = new List<Loop>();
        foreach (var l in root.GetProperty("loops").EnumerateArray())
        {
            string role = l.TryGetProperty("role", out var r) ? r.GetString() ?? "outer" : "outer";
            Loop loop = ReadLoop(l);
            if (role == "hole") holes.Add(loop); else outers.Add(loop);
        }
        return Region.Normalize(outers, holes);
    }

    private static Loop ReadLoop(JsonElement l)
    {
        if (l.TryGetProperty("circle", out var c))
        {
            var ctr = c.GetProperty("center");
            return Loop.FromCircle(new Vec2(ctr[0].GetDouble(), ctr[1].GetDouble()), c.GetProperty("radius").GetDouble());
        }
        var verts = new List<(Vec2, double)>();
        foreach (var v in l.GetProperty("vertices").EnumerateArray())
            verts.Add((new Vec2(v[0].GetDouble(), v[1].GetDouble()), v.GetArrayLength() > 2 ? v[2].GetDouble() : 0));
        return Loop.FromPolyline(verts);
    }
}
