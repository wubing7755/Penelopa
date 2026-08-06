using Penelopa.Core.Primitives;
using Penelopa.Rendering;
using SkiaSharp;
using Xunit;

namespace Penelopa.Rendering.Tests;

public class CanvasRendererTests
{
    private const int CanvasSize = 512;

    [Fact]
    public void HitTest_AtCircleScreenPosition_ReturnsThatCircle()
    {
        var renderer = new CanvasRenderer();
        // World center (20,20) renders at screen pixel (20, 512-20) after the
        // y-flip (origin at bottom-left, y grows up).
        var circle = new Circle { CenterX = { Value = 20f }, CenterY = { Value = 20f }, Radius = { Value = 10f } };

        renderer.Render(NewSurface(), new Primitive[] { circle });
        var hit = renderer.HitTest(20, CanvasSize - 20);

        Assert.Same(circle, hit);
    }

    [Fact]
    public void HitTest_AtMirroredScreenPosition_ReturnsNull()
    {
        var renderer = new CanvasRenderer();
        var circle = new Circle { CenterX = { Value = 20f }, CenterY = { Value = 20f }, Radius = { Value = 10f } };

        renderer.Render(NewSurface(), new Primitive[] { circle });
        // Screen pixel (20,20) corresponds to world (20, 492), outside the circle.
        var hit = renderer.HitTest(20, 20);

        Assert.Null(hit);
    }

    [Fact]
    public void HitTest_AtEmptyArea_ReturnsNull()
    {
        var renderer = new CanvasRenderer();
        var circle = new Circle { CenterX = { Value = 30f }, CenterY = { Value = 30f }, Radius = { Value = 5f } };

        renderer.Render(NewSurface(), new Primitive[] { circle });
        var hit = renderer.HitTest(100, 100);

        Assert.Null(hit);
    }

    [Fact]
    public void HitTest_TwoOverlappingShapes_RespectsZOrder()
    {
        var renderer = new CanvasRenderer();
        var bottom = new Rectangle { PosX = { Value = 0f }, PosY = { Value = 0f }, Width = { Value = 50f }, Height = { Value = 50f } };
        var top = new Circle { CenterX = { Value = 25f }, CenterY = { Value = 25f }, Radius = { Value = 10f } };

        // Later primitives paint over earlier ones, so the circle wins at its center.
        renderer.Render(NewSurface(), new Primitive[] { bottom, top });
        var hit = renderer.HitTest(25, CanvasSize - 25);

        Assert.Same(top, hit);
    }

    [Fact]
    public void HitTest_TwoOverlappingShapes_BackgroundWinsOutsideTopShape()
    {
        var renderer = new CanvasRenderer();
        var bottom = new Rectangle { PosX = { Value = 0f }, PosY = { Value = 0f }, Width = { Value = 50f }, Height = { Value = 50f } };
        var top = new Circle { CenterX = { Value = 25f }, CenterY = { Value = 25f }, Radius = { Value = 10f } };

        renderer.Render(NewSurface(), new Primitive[] { bottom, top });
        // Inside the rectangle but outside the circle (radius 10): bottom wins.
        var hit = renderer.HitTest(10, CanvasSize - 10);

        Assert.Same(bottom, hit);
    }

    [Fact]
    public void HitTest_EmptyCanvas_ReturnsNull()
    {
        var renderer = new CanvasRenderer();

        renderer.Render(NewSurface(), Array.Empty<Primitive>());
        var hit = renderer.HitTest(0, 0);

        Assert.Null(hit);
    }

    private static SKCanvas NewSurface()
    {
        var bitmap = new SKBitmap(new SKImageInfo(CanvasSize, CanvasSize));
        return new SKCanvas(bitmap);
    }
}
