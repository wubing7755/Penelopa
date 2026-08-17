using Penelopa.Core.Alignment;
using Penelopa.Core.Interaction;
using Penelopa.Core.Primitives;
using SkiaSharp;

namespace Penelopa.Rendering;

/// <summary>
/// Renders primitives onto a render-target-sized bitmap and performs color-key
/// hit testing. The visible canvas and the off-screen hit buffer share one
/// <see cref="ViewTransform"/> (origin at bottom-left, Y grows up, scaled by
/// the device pixel ratio), so a CSS screen pixel from the mouse maps into the
/// hit buffer through the same transform that positioned the visible content.
/// The render target size is supplied by the host from the SKGLView event
/// (<c>e.Info</c>), never inferred from the canvas device bounds.
/// </summary>
public sealed class CanvasRenderer
{
    private const int FallbackCanvasSize = 512;

    private SKBitmap _drawBitmap = new(new SKImageInfo(FallbackCanvasSize, FallbackCanvasSize));
    private SKBitmap _hitBitmap = new(new SKImageInfo(FallbackCanvasSize, FallbackCanvasSize));
    private ViewTransform _viewTransform = new(FallbackCanvasSize, FallbackCanvasSize, 1f);

    private readonly SKPaint _axisPaint = new()
    {
        Color = SKColors.Red,
        StrokeWidth = 2,
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
    };

    private readonly SKPaint _arrowPaint = new()
    {
        Color = SKColors.Red,
        StrokeWidth = 2,
        IsAntialias = true,
        Style = SKPaintStyle.Fill,
    };

    private readonly SKPaint _textPaint = new()
    {
        Color = SKColors.Red,
        IsAntialias = true,
    };

    private readonly SKFont _textFont = new()
    {
        Edging = SKFontEdging.Antialias,
    };

    /// <summary>
    /// Renders the primitives to the canvas and refreshes the hit buffer.
    /// </summary>
    /// <param name="canvas">The target canvas from the SKGLView paint event.</param>
    /// <param name="info">The user-visible render target size
    /// (<c>SKPaintGLSurfaceEventArgs.Info</c>, in physical pixels when
    /// <c>IgnorePixelScaling</c> is false). Do not pass the raw info of a
    /// scaled surface, or the coordinate spaces diverge.</param>
    /// <param name="devicePixelRatio">The CSS-to-physical pixel ratio
    /// (<c>window.devicePixelRatio</c>).</param>
    /// <param name="primitives">The primitives to draw.</param>
    /// <param name="selection">The current selection; when non-empty, a
    /// selection box (and corner handles for a single item) is drawn on top.</param>
    public void Render(
        SKCanvas canvas,
        SKImageInfo info,
        float devicePixelRatio,
        IReadOnlyList<Primitive> primitives,
        IReadOnlyList<Primitive>? selection = null)
    {
        EnsureBuffersFor(info.Width, info.Height, devicePixelRatio);
        canvas.Clear(SKColors.Black);
        DrawPrimitives(primitives);
        canvas.DrawBitmap(_drawBitmap, 0, 0);
        DrawSelectionOverlay(canvas, selection);
    }

    /// <summary>Gets the current view transform (zoom/pan state).</summary>
    public ViewTransform CurrentViewTransform => _viewTransform;

    /// <summary>Replaces the view transform (zoom/pan).</summary>
    public void SetViewTransform(ViewTransform transform) => _viewTransform = transform;

    /// <summary>
    /// Zooms to <paramref name="newZoom"/> keeping the world point under the
    /// CSS cursor fixed (wheel-zoom around the pointer).
    /// </summary>
    public void ZoomAt(float cssX, float cssY, float newZoom)
    {
        var view = _viewTransform.ScreenToView(cssX, cssY);
        var world = _viewTransform.ViewToWorld(view.X, view.Y);

        float halfW = _viewTransform.ViewWidth / (2f * _viewTransform.DevicePixelRatio);
        float halfH = _viewTransform.ViewHeight / (2f * _viewTransform.DevicePixelRatio);
        float panX = cssX - halfW - world.X * newZoom;
        float panY = cssY - halfH + world.Y * newZoom;
        _viewTransform = new ViewTransform(
            _viewTransform.ViewWidth,
            _viewTransform.ViewHeight,
            _viewTransform.DevicePixelRatio,
            newZoom,
            panX,
            panY);
    }

    /// <summary>Pans the view by a CSS-pixel delta.</summary>
    public void PanBy(float cssDx, float cssDy)
    {
        _viewTransform = new ViewTransform(
            _viewTransform.ViewWidth,
            _viewTransform.ViewHeight,
            _viewTransform.DevicePixelRatio,
            _viewTransform.Zoom,
            _viewTransform.PanX + cssDx,
            _viewTransform.PanY + cssDy);
    }

    /// <summary>
    /// Fits the content bounds into the viewport with padding, centering the
    /// content (zoom may shrink or enlarge; empty content leaves the view
    /// unchanged).
    /// </summary>
    public void FitToContent(IReadOnlyList<Primitive> primitives, float paddingCss = 40f)
    {
        if (primitives.Count == 0)
        {
            return;
        }

        var bounds = MergeBounds(primitives);
        float viewportW = _viewTransform.ViewWidth / _viewTransform.DevicePixelRatio;
        float viewportH = _viewTransform.ViewHeight / _viewTransform.DevicePixelRatio;
        float contentW = MathF.Max(bounds.Width, 1f);
        float contentH = MathF.Max(bounds.Height, 1f);

        float zoom = MathF.Min(
            viewportW / (contentW + 2f * paddingCss),
            viewportH / (contentH + 2f * paddingCss));
        zoom = Math.Clamp(zoom, 0.05f, 4f);

        float panX = -bounds.CenterX * zoom;
        float panY = bounds.CenterY * zoom;
        _viewTransform = new ViewTransform(
            _viewTransform.ViewWidth,
            _viewTransform.ViewHeight,
            _viewTransform.DevicePixelRatio,
            zoom,
            panX,
            panY);
    }

    /// <summary>
    /// Updates the device pixel ratio used by subsequent hit tests, keeping
    /// pointer coordinates in sync with the most recent display scale without
    /// waiting for the next render frame.
    /// </summary>
    public void SetDevicePixelRatio(float devicePixelRatio)
    {
        if (_viewTransform.DevicePixelRatio == devicePixelRatio)
        {
            return;
        }

        _viewTransform = new ViewTransform(
            _viewTransform.ViewWidth,
            _viewTransform.ViewHeight,
            devicePixelRatio);
    }

    private void EnsureBuffersFor(int width, int height, float devicePixelRatio)
    {
        var safeWidth = width > 0 ? width : FallbackCanvasSize;
        var safeHeight = height > 0 ? height : FallbackCanvasSize;
        if (_drawBitmap.Width != safeWidth || _drawBitmap.Height != safeHeight)
        {
            _drawBitmap.Dispose();
            _hitBitmap.Dispose();
            _drawBitmap = new SKBitmap(new SKImageInfo(safeWidth, safeHeight));
            _hitBitmap = new SKBitmap(new SKImageInfo(safeWidth, safeHeight));
        }

        if (_viewTransform.ViewWidth != safeWidth
            || _viewTransform.ViewHeight != safeHeight
            || _viewTransform.DevicePixelRatio != devicePixelRatio)
        {
            _viewTransform = new ViewTransform(safeWidth, safeHeight, devicePixelRatio);
        }
    }

    /// <summary>
    /// Returns the primitive under a CSS screen pixel, or null when the pixel
    /// is empty. The coordinates are the browser event coordinates over the
    /// canvas (origin at top-left, Y grows down).
    /// </summary>
    public Primitive? HitTest(float screenCssX, float screenCssY)
    {
        var view = _viewTransform.ScreenToView(screenCssX, screenCssY);
        var x = (int)view.X;
        var y = (int)view.Y;
        if ((uint)x >= (uint)_hitBitmap.Width || (uint)y >= (uint)_hitBitmap.Height)
        {
            return null;
        }

        var color = _hitBitmap.GetPixel(x, y);
        var colorKey = (uint)color;
        return ColorKeyManager.TryGetPrimitive(colorKey, out var primitive) ? primitive : null;
    }

    /// <summary>
    /// Tests whether a CSS screen point hits one of the selection box's
    /// corner handles, in screen space. Handles are fixed-size screen
    /// elements, so the test uses CSS pixels independent of world scale.
    /// </summary>
    public ResizeHandle? HitTestHandles(float screenCssX, float screenCssY, Box worldBounds)
    {
        const float hitRadiusCss = 5f;
        foreach (var handle in SelectionBox.AllHandles)
        {
            var screen = ToCss(SelectionBox.HandlePoint(worldBounds, handle));
            if (MathF.Abs(screen.X - screenCssX) <= hitRadiusCss
                && MathF.Abs(screen.Y - screenCssY) <= hitRadiusCss)
            {
                return handle;
            }
        }

        return null;
    }

    /// <summary>
    /// Tests whether a CSS screen point lies inside the world bounds projected
    /// to screen space (the multi-selection union box, used as a group-drag
    /// handle).
    /// </summary>
    public bool HitTestUnionBox(float screenCssX, float screenCssY, Box worldBounds)
    {
        var topLeft = ToCss(new Point(worldBounds.MinX, worldBounds.MaxY));
        var bottomRight = ToCss(new Point(worldBounds.MaxX, worldBounds.MinY));
        return screenCssX >= topLeft.X && screenCssX <= bottomRight.X
            && screenCssY >= topLeft.Y && screenCssY <= bottomRight.Y;
    }

    /// <summary>
    /// Runs the layered hit test for the interaction layer: corner handle
    /// (single selection), then primitive color key, then the multi-selection
    /// union box.
    /// </summary>
    public HitTestResult HitTestSelection(float screenCssX, float screenCssY, IReadOnlyList<Primitive> selection)
    {
        if (selection.Count == 1)
        {
            var bounds = selection[0].GetWorldBoundingBox();
            var handle = HitTestHandles(screenCssX, screenCssY, bounds);
            if (handle is not null)
            {
                return new HitTestResult { Handle = handle, Primitive = selection[0] };
            }
        }

        var primitive = HitTest(screenCssX, screenCssY);
        if (primitive is not null)
        {
            return new HitTestResult
            {
                Primitive = primitive,
                Candidates = PickAll(screenCssX, screenCssY),
            };
        }

        if (selection.Count > 1)
        {
            return new HitTestResult { InUnionBox = HitTestUnionBox(screenCssX, screenCssY, MergeBounds(selection)) };
        }

        return default;
    }

    /// <summary>
    /// Converts a CSS screen point to world coordinates.
    /// </summary>
    public Point CssToWorld(float screenCssX, float screenCssY)
    {
        var view = _viewTransform.ScreenToView(screenCssX, screenCssY);
        var world = _viewTransform.ViewToWorld(view.X, view.Y);
        return new Point(world.X, world.Y);
    }

    /// <summary>
    /// Collects the drill-down candidates at a point: the topmost primitive
    /// from the color-key buffer plus its ancestor chain, ordered from the
    /// outermost container (root) to the deepest leaf. This is the confirmed
    /// drill direction: the first click selects the outermost container and
    /// further clicks descend toward the inner shape.
    /// </summary>
    public IReadOnlyList<Primitive> PickAll(float screenCssX, float screenCssY)
    {
        var leaf = HitTest(screenCssX, screenCssY);
        if (leaf is null)
        {
            return Array.Empty<Primitive>();
        }

        var chain = new List<Primitive>();
        for (var node = leaf; node is not null; node = node.Parent)
        {
            chain.Add(node);
        }

        chain.Reverse();
        return chain;
    }

    private void DrawSelectionOverlay(SKCanvas canvas, IReadOnlyList<Primitive>? selection)
    {
        if (selection is null || selection.Count == 0)
        {
            return;
        }

        canvas.Save();
        try
        {
            // World mode: one world unit equals one CSS pixel, so handles
            // drawn at HandleSizeCss units are fixed-size on screen.
            _viewTransform.ApplyTo(canvas);

            if (selection.Count == 1)
            {
                DrawSelectionBox(canvas, selection[0].GetWorldBoundingBox(), drawHandles: true);
            }
            else
            {
                DrawSelectionBox(canvas, MergeBounds(selection), drawHandles: false);
            }
        }
        finally
        {
            canvas.Restore();
        }
    }

    private void DrawSelectionBox(SKCanvas canvas, Box bounds, bool drawHandles)
    {
        // World mode: one world unit equals one CSS pixel * Zoom, so dividing
        // by Zoom keeps the outline width and handle size fixed on screen.
        float inverseZoom = 1f / _viewTransform.Zoom;
        using var outline = new SKPaint
        {
            Color = SelectionColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f * inverseZoom,
            IsAntialias = true,
        };
        canvas.DrawRect(new SKRect(bounds.MinX, bounds.MinY, bounds.MaxX, bounds.MaxY), outline);

        if (!drawHandles)
        {
            return;
        }

        using var fill = new SKPaint
        {
            Color = SelectionColor,
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
        };
        foreach (var handle in SelectionBox.AllHandles)
        {
            var p = SelectionBox.HandlePoint(bounds, handle);
            float half = HandleSizeCss / 2f * inverseZoom;
            canvas.DrawRect(
                new SKRect(p.X - half, p.Y - half, p.X + half, p.Y + half),
                fill);
        }
    }

    private static Box MergeBounds(IReadOnlyList<Primitive> primitives)
    {
        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;
        foreach (var primitive in primitives)
        {
            var box = primitive.GetWorldBoundingBox();
            minX = MathF.Min(minX, box.MinX);
            minY = MathF.Min(minY, box.MinY);
            maxX = MathF.Max(maxX, box.MaxX);
            maxY = MathF.Max(maxY, box.MaxY);
        }

        return new Box(minX, minY, maxX, maxY);
    }

    private SKPoint ToCss(Point world)
    {
        var view = _viewTransform.WorldToView(world.X, world.Y);
        return _viewTransform.ViewToScreen(view.X, view.Y);
    }

    private const float HandleSizeCss = 8f;
    private static readonly SKColor SelectionColor = new(0x4D, 0x9F, 0xFF);

    private void DrawPrimitives(IReadOnlyList<Primitive> primitives)
    {
        using var canvas = new SKCanvas(_drawBitmap);
        canvas.Clear(SKColors.Black);

        using var hitCanvas = new SKCanvas(_hitBitmap);
        hitCanvas.Clear(SKColors.Black);

        canvas.Save();
        hitCanvas.Save();
        try
        {
            _viewTransform.ApplyTo(canvas);
            _viewTransform.ApplyTo(hitCanvas);

            // Axis indicator sits under the primitives; it never enters the
            // hit buffer so the axes stay non-interactive.
            DrawCoordinateSystem(canvas);

            foreach (var primitive in primitives)
            {
                DrawNode(canvas, hitCanvas, primitive);
            }
        }
        finally
        {
            canvas.Restore();
            hitCanvas.Restore();
        }
    }

    /// <summary>
    /// Draws a primitive tree node. Containers save the canvas state, apply
    /// their transform to BOTH canvases (visible and hit must stay
    /// synchronized so the color-key hit test matches what is drawn), recurse
    /// into children, then restore. Render order equals Z order: later nodes
    /// cover earlier ones.
    /// </summary>
    private static void DrawNode(SKCanvas canvas, SKCanvas hitCanvas, Primitive node)
    {
        if (node is Container container)
        {
            var matrix = ToSkMatrix(container.LocalTransform);
            canvas.Save();
            hitCanvas.Save();
            canvas.Concat(ref matrix);
            hitCanvas.Concat(ref matrix);
            foreach (var child in container.Children)
            {
                DrawNode(canvas, hitCanvas, child);
            }

            canvas.Restore();
            hitCanvas.Restore();
            return;
        }

        switch (node)
        {
            case Circle circle:
                DrawCircle(canvas, circle);
                DrawHitCircle(hitCanvas, circle);
                break;
            case Rectangle rectangle:
                DrawRectangle(canvas, rectangle);
                DrawHitRectangle(hitCanvas, rectangle);
                break;
            case Triangle triangle:
                DrawTriangle(canvas, triangle);
                DrawHitTriangle(hitCanvas, triangle);
                break;
        }
    }

    /// <summary>Converts the Core affine transform to an Skia matrix.</summary>
    private static SKMatrix ToSkMatrix(Transform transform)
    {
        var matrix = new SKMatrix
        {
            ScaleX = transform.A,
            SkewY = transform.B,
            SkewX = transform.C,
            ScaleY = transform.D,
            TransX = transform.Tx,
            TransY = transform.Ty,
        };
        matrix.Persp0 = 0;
        matrix.Persp1 = 0;
        matrix.Persp2 = 1;
        return matrix;
    }

    private static void DrawCircle(SKCanvas canvas, Circle circle)
    {
        using var paint = new SKPaint
        {
            Color = new SKColor(circle.Color.Value),
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
        };
        canvas.DrawCircle(circle.CenterX.Value, circle.CenterY.Value, circle.Radius.Value, paint);
    }

    private static void DrawHitCircle(SKCanvas hitCanvas, Circle circle)
    {
        var color = Color.FromUint(circle.ColorKey.Value);
        using var paint = new SKPaint
        {
            Color = new SKColor(color.R, color.G, color.B, color.A),
            Style = SKPaintStyle.Fill,
            // The hit buffer must stay exact: antialiasing would blend edge
            // pixels into colors that match no registered key, making 1px
            // edges unhittable and breaking top-pixel/candidate consistency.
            IsAntialias = false,
        };
        hitCanvas.DrawCircle(circle.CenterX.Value, circle.CenterY.Value, circle.Radius.Value, paint);
    }

    private static void DrawRectangle(SKCanvas canvas, Rectangle rectangle)
    {
        using var paint = new SKPaint
        {
            Color = new SKColor(rectangle.Color.Value),
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
        };
        var rect = new SKRect(
            rectangle.PosX.Value,
            rectangle.PosY.Value,
            rectangle.PosX.Value + rectangle.Width.Value,
            rectangle.PosY.Value + rectangle.Height.Value);
        canvas.DrawRect(rect, paint);
    }

    private static void DrawHitRectangle(SKCanvas hitCanvas, Rectangle rectangle)
    {
        var color = Color.FromUint(rectangle.ColorKey.Value);
        using var paint = new SKPaint
        {
            Color = new SKColor(color.R, color.G, color.B, color.A),
            Style = SKPaintStyle.Fill,
            IsAntialias = false,
        };
        var rect = new SKRect(
            rectangle.PosX.Value,
            rectangle.PosY.Value,
            rectangle.PosX.Value + rectangle.Width.Value,
            rectangle.PosY.Value + rectangle.Height.Value);
        hitCanvas.DrawRect(rect, paint);
    }

    private static void DrawTriangle(SKCanvas canvas, Triangle triangle)
    {
        using var paint = new SKPaint
        {
            Color = new SKColor(triangle.Color.Value),
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
        };
        using var path = new SKPath();
        path.MoveTo(triangle.Vertex1X.Value, triangle.Vertex1Y.Value);
        path.LineTo(triangle.Vertex2X.Value, triangle.Vertex2Y.Value);
        path.LineTo(triangle.Vertex3X.Value, triangle.Vertex3Y.Value);
        path.Close();
        canvas.DrawPath(path, paint);
    }

    private static void DrawHitTriangle(SKCanvas hitCanvas, Triangle triangle)
    {
        var color = Color.FromUint(triangle.ColorKey.Value);
        using var paint = new SKPaint
        {
            Color = new SKColor(color.R, color.G, color.B, color.A),
            Style = SKPaintStyle.Fill,
            IsAntialias = false,
        };
        using var path = new SKPath();
        path.MoveTo(triangle.Vertex1X.Value, triangle.Vertex1Y.Value);
        path.LineTo(triangle.Vertex2X.Value, triangle.Vertex2Y.Value);
        path.LineTo(triangle.Vertex3X.Value, triangle.Vertex3Y.Value);
        path.Close();
        hitCanvas.DrawPath(path, paint);
    }

    /// <summary>
    /// Draws the coordinate system in world mode: the origin (0,0) is the
    /// canvas center, so X and Y axes span the whole canvas and a center
    /// marker makes the origin explicit.
    /// </summary>
    private void DrawCoordinateSystem(SKCanvas canvas)
    {
        float halfW = _viewTransform.ViewWidth / (2f * _viewTransform.DevicePixelRatio);
        float halfH = _viewTransform.ViewHeight / (2f * _viewTransform.DevicePixelRatio);

        canvas.DrawLine(new SKPoint(-halfW, 0f), new SKPoint(halfW, 0f), _axisPaint);
        canvas.DrawLine(new SKPoint(0f, -halfH), new SKPoint(0f, halfH), _axisPaint);

        const float marker = 2.5f;
        canvas.DrawCircle(0f, 0f, marker, _arrowPaint);

        const float arrowSize = 4f;
        using (var xArrow = new SKPath())
        {
            xArrow.MoveTo(halfW, 0f);
            xArrow.LineTo(halfW - arrowSize, -arrowSize);
            xArrow.LineTo(halfW - arrowSize, arrowSize);
            xArrow.Close();
            canvas.DrawPath(xArrow, _arrowPaint);
        }

        using (var yArrow = new SKPath())
        {
            yArrow.MoveTo(0f, halfH);
            yArrow.LineTo(-arrowSize, halfH - arrowSize);
            yArrow.LineTo(arrowSize, halfH - arrowSize);
            yArrow.Close();
            canvas.DrawPath(yArrow, _arrowPaint);
        }

        // Text renders upside down under the world y-flip; flip once so the
        // labels read normally.
        canvas.Save();
        canvas.Scale(1, -1);
        canvas.DrawText("X", halfW - 16, 12, _textFont, _textPaint);
        canvas.DrawText("Y", 6, -(halfH - 10), _textFont, _textPaint);
        canvas.Restore();
    }
}
