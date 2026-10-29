using SuperFill.Core.Serialization;
using Xunit;

namespace SuperFill.Core.Tests;

public class SampleBoundaryTests
{
    private static string SamplesDir
    {
        get
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d != null && !Directory.Exists(Path.Combine(d.FullName, "samples", "boundaries"))) d = d.Parent;
            return Path.Combine(d!.FullName, "samples", "boundaries");
        }
    }

    [Theory]
    [InlineData("rect-20x10", 200.0, 0.0)]
    [InlineData("rondell-r6", Math.PI * 36, 1e-6)]
    [InlineData("big-1000m2", 1000.0, 0.0)]
    [InlineData("sweref-far", 200.0, 0.0)]
    [InlineData("sliver-trap", 10.003 * 5, 1e-9)]
    public void KnownAreas(string name, double area, double tol)
    {
        var region = BoundaryJson.Parse(File.ReadAllText(Path.Combine(SamplesDir, name + ".json")));
        Assert.Equal(area, region.Area, tol + 1e-9);
    }

    [Fact]
    public void StreetCurve_AreaIsWidthTimesCenterlineLength()
    {
        var region = BoundaryJson.Parse(File.ReadAllText(Path.Combine(SamplesDir, "street-curve.json")));
        Assert.Equal(8 * 60.0, region.Area, 0);
    }

    [Fact]
    public void PlazaHoles_SubtractsHoles()
    {
        var region = BoundaryJson.Parse(File.ReadAllText(Path.Combine(SamplesDir, "plaza-holes.json")));
        Assert.Single(region.Faces);
        Assert.Equal(3, region.Faces[0].Holes.Count);
        Assert.All(region.Faces[0].Holes, h => Assert.False(h.IsCounterClockwise));
        Assert.True(region.Faces[0].Outer.IsCounterClockwise);
    }

    [Fact]
    public void AllSamples_Load()
    {
        var files = Directory.GetFiles(SamplesDir, "*.json");
        Assert.Equal(8, files.Length);
        foreach (var f in files) Assert.True(BoundaryJson.Parse(File.ReadAllText(f)).Area > 0);
    }
}
