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

        renderer.Render(NewSurface(), NewInfo(), 1f, new Primitive[] { circle });
        var hit = renderer.HitTest(20f, CanvasSize - 20f);

        Assert.Same(circle, hit);
    }

    [Fact]
    public void HitTest_AtMirroredScreenPosition_ReturnsNull()
    {
        var renderer = new CanvasRenderer();
        var circle = new Circle { CenterX = { Value = 20f }, CenterY = { Value = 20f }, Radius = { Value = 10f } };

        renderer.Render(NewSurface(), NewInfo(), 1f, new Primitive[] { circle });
        // Screen pixel (20,20) corresponds to world (20, 492), outside the circle.
        var hit = renderer.HitTest(20f, 20f);

        Assert.Null(hit);
    }

    [Fact]
    public void HitTest_AtEmptyArea_ReturnsNull()
    {
        var renderer = new CanvasRenderer();
        var circle = new Circle { CenterX = { Value = 30f }, CenterY = { Value = 30f }, Radius = { Value = 5f } };

        renderer.Render(NewSurface(), NewInfo(), 1f, new Primitive[] { circle });
        var hit = renderer.HitTest(100f, 100f);

        Assert.Null(hit);
    }

    [Fact]
    public void HitTest_TwoOverlappingShapes_RespectsZOrder()
    {
        var renderer = new CanvasRenderer();
        var bottom = new Rectangle { PosX = { Value = 0f }, PosY = { Value = 0f }, Width = { Value = 50f }, Height = { Value = 50f } };
        var top = new Circle { CenterX = { Value = 25f }, CenterY = { Value = 25f }, Radius = { Value = 10f } };

        // Later primitives paint over earlier ones, so the circle wins at its center.
        renderer.Render(NewSurface(), NewInfo(), 1f, new Primitive[] { bottom, top });
        var hit = renderer.HitTest(25f, CanvasSize - 25f);

        Assert.Same(top, hit);
    }

    [Fact]
    public void HitTest_TwoOverlappingShapes_BackgroundWinsOutsideTopShape()
    {
        var renderer = new CanvasRenderer();
        var bottom = new Rectangle { PosX = { Value = 0f }, PosY = { Value = 0f }, Width = { Value = 50f }, Height = { Value = 50f } };
        var top = new Circle { CenterX = { Value = 25f }, CenterY = { Value = 25f }, Radius = { Value = 10f } };

        renderer.Render(NewSurface(), NewInfo(), 1f, new Primitive[] { bottom, top });
        // Inside the rectangle but outside the circle (radius 10): bottom wins.
        var hit = renderer.HitTest(10f, CanvasSize - 10f);

        Assert.Same(bottom, hit);
    }

    [Fact]
    public void HitTest_EmptyCanvas_ReturnsNull()
    {
        var renderer = new CanvasRenderer();

        renderer.Render(NewSurface(), NewInfo(), 1f, Array.Empty<Primitive>());
        var hit = renderer.HitTest(0f, 0f);

        Assert.Null(hit);
    }

    [Fact]
    public void HitTest_WithDevicePixelRatio2_MapsCssPixelsToPhysicalPixels()
    {
        var renderer = new CanvasRenderer();
        var circle = new Circle { CenterX = { Value = 20f }, CenterY = { Value = 20f }, Radius = { Value = 10f } };

        // SKGLView with IgnorePixelScaling=false reports the physical render
        // target size in e.Info (CSS size x dpr). The visible canvas must be
        // sized accordingly so the world maps to CSS 1:1.
        var physicalSize = CanvasSize * 2;
        var bitmap = new SKBitmap(new SKImageInfo(physicalSize, physicalSize));
        var canvas = new SKCanvas(bitmap);
        renderer.Render(canvas, new SKImageInfo(physicalSize, physicalSize), 2f, new Primitive[] { circle });

        // World (20,20) -> view pixel (40, 1024-40) -> CSS (20, 512-20).
        var hit = renderer.HitTest(20f, CanvasSize - 20f);
        Assert.Same(circle, hit);

        // CSS top-left (20,20) maps to world (20,492): outside the circle.
        Assert.Null(renderer.HitTest(20f, 20f));

        // The visible color is painted at the physical pixel (40, 984).
        var rendered = bitmap.GetPixel(40, physicalSize - 40);
        Assert.Equal(0xFFFFFFFFu, (uint)rendered);
    }

    private static SKCanvas NewSurface()
    {
        var bitmap = new SKBitmap(new SKImageInfo(CanvasSize, CanvasSize));
        return new SKCanvas(bitmap);
    }

    private static SKImageInfo NewInfo() => new(CanvasSize, CanvasSize);
}
