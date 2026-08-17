using Penelopa.Rendering;
using Xunit;

namespace Penelopa.Rendering.Tests;

public class ViewTransformTests
{
    [Fact]
    public void WorldToView_AppliesYFlipAndDprScale()
    {
        var transform = new ViewTransform(100, 200, 2f);

        var view = transform.WorldToView(10f, 30f);

        Assert.Equal(20f, view.X);          // 10 * dpr
        Assert.Equal(140f, view.Y);         // height - 30 * dpr
    }

    [Fact]
    public void ViewToWorld_IsInverseOfWorldToView()
    {
        var transform = new ViewTransform(100, 200, 2f);

        var view = transform.WorldToView(10f, 30f);
        var world = transform.ViewToWorld(view.X, view.Y);

        Assert.Equal(10f, world.X, 3);
        Assert.Equal(30f, world.Y, 3);
    }

    [Fact]
    public void ScreenToView_ScalesCssToPhysical()
    {
        var transform = new ViewTransform(100, 200, 2f);

        var view = transform.ScreenToView(10f, 20f);

        Assert.Equal(20f, view.X);
        Assert.Equal(40f, view.Y);
    }

    [Fact]
    public void ViewToScreen_IsInverseOfScreenToView()
    {
        var transform = new ViewTransform(100, 200, 2f);

        var view = transform.ScreenToView(10f, 20f);
        var screen = transform.ViewToScreen(view.X, view.Y);

        Assert.Equal(10f, screen.X, 3);
        Assert.Equal(20f, screen.Y, 3);
    }

    [Fact]
    public void WorldToView_WithUnitRatio_IsPureYFlip()
    {
        var transform = new ViewTransform(100, 200, 1f);

        var view = transform.WorldToView(10f, 30f);

        Assert.Equal(10f, view.X);
        Assert.Equal(170f, view.Y);
    }

    [Fact]
    public void Constructor_RejectsNonPositiveDimensions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ViewTransform(0, 10, 1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ViewTransform(10, 0, 1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ViewTransform(10, 10, 0f));
    }
}
