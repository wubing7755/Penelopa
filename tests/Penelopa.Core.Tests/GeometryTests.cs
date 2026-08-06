using Penelopa.Core.Alignment;
using Penelopa.Core.Primitives;
using Xunit;

namespace Penelopa.Core.Tests;

public class GeometryTests
{
    [Fact]
    public void Box_FromSize_ComputesMaxFromPositionAndSize()
    {
        var box = Box.FromSize(10f, 20f, 30f, 40f);

        Assert.Equal(10f, box.MinX);
        Assert.Equal(20f, box.MinY);
        Assert.Equal(40f, box.MaxX);
        Assert.Equal(60f, box.MaxY);
        Assert.Equal(30f, box.Width);
        Assert.Equal(40f, box.Height);
    }

    [Fact]
    public void Box_Center_IsMidpointOfBounds()
    {
        var box = new Box(0f, 0f, 10f, 20f);

        Assert.Equal(5f, box.CenterX);
        Assert.Equal(10f, box.CenterY);
        Assert.Equal(new Point(5f, 10f), box.Center);
    }

    [Fact]
    public void Box_InvalidBounds_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Box(10f, 0f, 5f, 20f));
        Assert.Throws<ArgumentException>(() => new Box(0f, 20f, 5f, 10f));
    }

    [Fact]
    public void Box_Equals_ComparesAllBounds()
    {
        var a = new Box(1f, 2f, 3f, 4f);
        var b = new Box(1f, 2f, 3f, 4f);
        var c = new Box(1f, 2f, 3f, 5f);

        Assert.True(a == b);
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
        Assert.True(a != c);
    }

    [Fact]
    public void Point_Equals_ComparesCoordinates()
    {
        Assert.Equal(new Point(1f, 2f), new Point(1f, 2f));
        Assert.NotEqual(new Point(1f, 2f), new Point(2f, 1f));
        Assert.True(new Point(3f, 4f) == new Point(3f, 4f));
        Assert.False(new Point(3f, 4f) != new Point(3f, 4f));
    }

    [Fact]
    public void Transform_Translate_CreatesIdentityWithOffsets()
    {
        var t = Transform.Translate(7f, 9f);

        Assert.Equal(1f, t.A);
        Assert.Equal(0f, t.B);
        Assert.Equal(0f, t.C);
        Assert.Equal(1f, t.D);
        Assert.Equal(7f, t.Tx);
        Assert.Equal(9f, t.Ty);
    }

    [Fact]
    public void Color_FromUint_RoundTripsToUint()
    {
        const uint key = 0xFF336699;

        var color = Color.FromUint(key);

        Assert.Equal(0x33, color.R);
        Assert.Equal(0x66, color.G);
        Assert.Equal(0x99, color.B);
        Assert.Equal(0xFF, color.A);
        Assert.Equal(key, color.ToUint());
    }

    [Fact]
    public void Color_Transparent_HasZeroAlpha()
    {
        Assert.Equal(0, Color.Transparent.A);
        Assert.Equal(0u, Color.Transparent.ToUint());
    }
}
