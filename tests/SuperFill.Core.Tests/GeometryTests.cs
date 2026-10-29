using SuperFill.Core.Geometry;
using Xunit;

namespace SuperFill.Core.Tests;

public class GeometryTests
{
    private const double Eps = 1e-9;

    [Theory]
    [InlineData(0.4142135623730951)]
    [InlineData(-0.4142135623730951)]
    [InlineData(1.0)]
    [InlineData(-1.0)]
    [InlineData(2.5)]
    [InlineData(-0.05)]
    public void ArcFromBulge_HitsEndpointsAndBulgeRoundTrips(double bulge)
    {
        var a = new Vec2(1, 2);
        var b = new Vec2(5, 3);
        var arc = Arc2.FromBulge(a, b, bulge);
        Assert.True(arc.Start.NearlyEquals(a, 1e-9));
        Assert.True(arc.End.NearlyEquals(b, 1e-9));
        Assert.Equal(bulge, arc.Bulge, 9);
        // CCW arcs (bulge > 0) bulge to the right of a→b; the center is on the left.
        double side = Vec2.Cross(b - a, arc.PointAt(0.5) - a);
        Assert.Equal(-Math.Sign(bulge), Math.Sign(side));
    }

    [Fact]
    public void CircleLoop_AreaIsPiRSquared_AndReverseFlipsSign()
    {
        var loop = Loop.FromCircle(new Vec2(3, -2), 6);
        Assert.Equal(Math.PI * 36, loop.SignedArea(), 9);
        Assert.Equal(-Math.PI * 36, loop.Reversed().SignedArea(), 9);
    }

    [Fact]
    public void PolylineWithBulges_AreaMatchesDiscretizedPolygon()
    {
        var loop = Loop.FromPolyline(new[]
        {
            (new Vec2(0, 0), 0.0), (new Vec2(20, 0), 0.0), (new Vec2(20, 10), 0.4142135623730951),
            (new Vec2(10, 15), 0.0), (new Vec2(0, 10), -0.3),
        });
        var poly = Discretizer.ToPolygon(loop, 1e-5);
        double shoelace = 0;
        for (int i = 0; i < poly.Count; i++) shoelace += Vec2.Cross(poly[i], poly[(i + 1) % poly.Count]) / 2;
        Assert.Equal(loop.SignedArea(), shoelace, 2);
    }

    [Fact]
    public void Transform_ComposeAndInverse()
    {
        var t = Transform2.Translation(new Vec2(5, -1)).After(Transform2.Rotation(0.7).After(Transform2.Scale(2)));
        var p = new Vec2(3, 4);
        Assert.True(t.Inverse().Apply(t.Apply(p)).NearlyEquals(p, 1e-9));
        Assert.True(Transform2.Rotation(Math.PI / 2).Apply(new Vec2(1, 0)).NearlyEquals(new Vec2(0, 1), Eps));
        Assert.False(t.IsMirror);
        Assert.True(new Transform2(-1, 0, 0, 1, 0, 0).IsMirror);
    }

    [Fact]
    public void Tolerances_ScaleFromMillimetres()
    {
        var t = Tolerances.Metres;
        Assert.Equal(0.0005, t.ChordError, 12);
        Assert.Equal(1e-6, t.Epsilon, 12);
    }

    [Theory]
    [InlineData(6.0, 0.0005)]
    [InlineData(0.3, 0.0005)]
    public void ArcSegmentCount_KeepsChordErrorBelowLimit(double r, double e)
    {
        int n = Discretizer.ArcSegmentCount(r, Math.PI * 2, e);
        double sagitta = r * (1 - Math.Cos(Math.PI * 2 / n / 2));
        Assert.True(sagitta <= e * 1.0001);
    }
}
