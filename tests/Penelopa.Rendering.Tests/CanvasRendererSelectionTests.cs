using Penelopa.Core.Interaction;
using Penelopa.Core.Primitives;
using Penelopa.Rendering;
using SkiaSharp;
using Xunit;

namespace Penelopa.Rendering.Tests;

public class CanvasRendererSelectionTests
{
    private const int CanvasSize = 512;

    [Fact]
    public void HitTestSelection_SingleSelection_HandleWinsOverBody()
    {
        var renderer = new CanvasRenderer();
        var rect = new Rectangle { PosX = { Value = 10f }, PosY = { Value = 10f }, Width = { Value = 50f }, Height = { Value = 50f } };
        var selection = new[] { rect };
        renderer.Render(NewSurface(), NewInfo(), 1f, new Primitive[] { rect }, selection);

        // World bottom-right corner (60, 10) → CSS (60, 512-10=502).
        var hit = renderer.HitTestSelection(60f, 502f, selection);

        Assert.Equal(ResizeHandle.BottomRight, hit.Handle);
        Assert.Same(rect, hit.Primitive);
    }

    [Fact]
    public void HitTestSelection_HandleHitRadius_DetectsNearbyPresses()
    {
        var renderer = new CanvasRenderer();
        var rect = new Rectangle { PosX = { Value = 10f }, PosY = { Value = 10f }, Width = { Value = 50f }, Height = { Value = 50f } };
        var selection = new[] { rect };
        renderer.Render(NewSurface(), NewInfo(), 1f, new Primitive[] { rect }, selection);

        // 3 CSS px inside the corner still counts as the handle.
        var hit = renderer.HitTestSelection(58f, 500f, selection);

        Assert.Equal(ResizeHandle.BottomRight, hit.Handle);
    }

    [Fact]
    public void HitTestSelection_BodyHit_ReturnsPrimitive()
    {
        var renderer = new CanvasRenderer();
        var rect = new Rectangle { PosX = { Value = 10f }, PosY = { Value = 10f }, Width = { Value = 50f }, Height = { Value = 50f } };
        var selection = new[] { rect };
        renderer.Render(NewSurface(), NewInfo(), 1f, new Primitive[] { rect }, selection);

        // World center (35, 35) → CSS (35, 477).
        var hit = renderer.HitTestSelection(35f, 477f, selection);

        Assert.Null(hit.Handle);
        Assert.Same(rect, hit.Primitive);
        Assert.False(hit.InUnionBox);
    }

    [Fact]
    public void HitTestSelection_EmptyAreaOutsideUnion_ReturnsNothing()
    {
        var renderer = new CanvasRenderer();
        var a = new Rectangle { PosX = { Value = 0f }, PosY = { Value = 0f }, Width = { Value = 20f }, Height = { Value = 20f } };
        var b = new Rectangle { PosX = { Value = 40f }, PosY = { Value = 0f }, Width = { Value = 20f }, Height = { Value = 20f } };
        var selection = new[] { a, b };
        renderer.Render(NewSurface(), NewInfo(), 1f, new Primitive[] { a, b }, selection);

        // (90, 10) is outside the union box (0..60, 0..20 in world).
        var hit = renderer.HitTestSelection(90f, 502f, selection);

        Assert.Null(hit.Handle);
        Assert.Null(hit.Primitive);
        Assert.False(hit.InUnionBox);
    }

    [Fact]
    public void HitTestSelection_MultiSelectionUnionBox_HitInBlankArea()
    {
        var renderer = new CanvasRenderer();
        var a = new Rectangle { PosX = { Value = 0f }, PosY = { Value = 0f }, Width = { Value = 20f }, Height = { Value = 20f } };
        var b = new Rectangle { PosX = { Value = 40f }, PosY = { Value = 0f }, Width = { Value = 20f }, Height = { Value = 20f } };
        var selection = new[] { a, b };
        renderer.Render(NewSurface(), NewInfo(), 1f, new Primitive[] { a, b }, selection);

        // World (30, 10) lies between the two rects: inside the union, no body.
        var hit = renderer.HitTestSelection(30f, 502f, selection);

        Assert.Null(hit.Primitive);
        Assert.True(hit.InUnionBox);
    }

    [Fact]
    public void CssToWorld_RoundTripsThroughViewTransform()
    {
        var renderer = new CanvasRenderer();
        renderer.Render(NewSurface(), NewInfo(), 1f, Array.Empty<Primitive>());

        var world = renderer.CssToWorld(100f, 200f);

        Assert.Equal(100f, world.X, 3);
        Assert.Equal(CanvasSize - 200f, world.Y, 3); // y-flip at unit ratio
    }

    [Fact]
    public void SelectionOverlay_DrawsHandlesAtCorners()
    {
        var renderer = new CanvasRenderer();
        var rect = new Rectangle { PosX = { Value = 10f }, PosY = { Value = 10f }, Width = { Value = 50f }, Height = { Value = 50f } };
        var bitmap = new SKBitmap(new SKImageInfo(CanvasSize, CanvasSize));
        var canvas = new SKCanvas(bitmap);
        renderer.Render(canvas, NewInfo(), 1f, new Primitive[] { rect }, new[] { rect });

        // Bottom-right handle: world (60, 10) → pixel (60, 502), colored with
        // the selection color (0xFF4D9FFF).
        var pixel = bitmap.GetPixel(60, CanvasSize - 10);
        Assert.Equal(0xFF4D9FFFu, (uint)pixel);
    }

    private static SKCanvas NewSurface()
    {
        var bitmap = new SKBitmap(new SKImageInfo(CanvasSize, CanvasSize));
        return new SKCanvas(bitmap);
    }

    private static SKImageInfo NewInfo() => new(CanvasSize, CanvasSize);
}
