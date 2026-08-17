using Penelopa.Rendering;
using Xunit;

namespace Penelopa.Rendering.Tests;

public class ViewTransformTests
{
    [Fact]
    public void WorldToView_OriginIsCanvasCenter()
    {
        var transform = new ViewTransform(100, 200, 2f);

        var view = transform.WorldToView(0f, 0f);

        Assert.Equal(50f, view.X);
        Assert.Equal(100f, view.Y);
    }

    [Fact]
    public void WorldToView_AppliesCenterOffsetYFlipAndDprScale()
    {
        var transform = new ViewTransform(100, 200, 2f);

        var view = transform.WorldToView(10f, 30f);

        Assert.Equal(70f, view.X);   // width/2 + 10 * dpr
        Assert.Equal(40f, view.Y);   // height/2 - 30 * dpr
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
    public void WorldToView_WithUnitRatio_IsCenterPlusYFlip()
    {
        var transform = new ViewTransform(100, 200, 1f);

        var view = transform.WorldToView(10f, 30f);

        Assert.Equal(60f, view.X);   // width/2 + 10
        Assert.Equal(70f, view.Y);   // height/2 - 30
    }

    [Fact]
    public void Constructor_RejectsNonPositiveDimensions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ViewTransform(0, 10, 1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ViewTransform(10, 0, 1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ViewTransform(10, 10, 0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ViewTransform(10, 10, 1f, 0f));
    }

    [Fact]
    public void WorldToView_WithZoom_ScalesWorldAroundCenter()
    {
        var transform = new ViewTransform(100, 200, 1f, zoom: 2f);

        var view = transform.WorldToView(10f, 10f);

        Assert.Equal(70f, view.X);   // width/2 + 10 * zoom
        Assert.Equal(80f, view.Y);   // height/2 - 10 * zoom
    }

    [Fact]
    public void WorldToView_WithPan_OffsetsOrigin()
    {
        var transform = new ViewTransform(100, 200, 1f, zoom: 1f, panX: 15f, panY: -5f);

        var view = transform.WorldToView(0f, 0f);

        Assert.Equal(65f, view.X);   // width/2 + panX
        Assert.Equal(95f, view.Y);   // height/2 + panY
    }

    [Fact]
    public void ZoomedView_RoundTripsWorldToView()
    {
        var transform = new ViewTransform(100, 200, 2f, zoom: 1.5f, panX: 10f, panY: -8f);

        var view = transform.WorldToView(10f, 30f);
        var world = transform.ViewToWorld(view.X, view.Y);

        Assert.Equal(10f, world.X, 3);
        Assert.Equal(30f, world.Y, 3);
    }
}
