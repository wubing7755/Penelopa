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
    public void Render(
        SKCanvas canvas,
        SKImageInfo info,
        float devicePixelRatio,
        IReadOnlyList<Primitive> primitives)
    {
        EnsureBuffersFor(info.Width, info.Height, devicePixelRatio);
        canvas.Clear(SKColors.Black);
        DrawPrimitives(primitives);
        canvas.DrawBitmap(_drawBitmap, 0, 0);
        DrawCoordinateSystem(canvas);
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

            foreach (var primitive in primitives)
            {
                switch (primitive)
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
        }
        finally
        {
            canvas.Restore();
            hitCanvas.Restore();
        }
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
            IsAntialias = true,
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
            IsAntialias = true,
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
            IsAntialias = true,
        };
        using var path = new SKPath();
        path.MoveTo(triangle.Vertex1X.Value, triangle.Vertex1Y.Value);
        path.LineTo(triangle.Vertex2X.Value, triangle.Vertex2Y.Value);
        path.LineTo(triangle.Vertex3X.Value, triangle.Vertex3Y.Value);
        path.Close();
        hitCanvas.DrawPath(path, paint);
    }

    /// <summary>
    /// Draws a small X/Y axis indicator at the bottom-left of the canvas.
    /// </summary>
    private void DrawCoordinateSystem(SKCanvas canvas)
    {
        const float size = 30f;
        const float offset = 10f;

        canvas.Save();
        try
        {
            _viewTransform.ApplyTo(canvas);

            canvas.DrawLine(offset, offset, offset + size, offset, _axisPaint);
            canvas.DrawLine(offset, offset, offset, offset + size, _axisPaint);

            const float arrowSize = 4f;
            using (var path = new SKPath())
            {
                path.MoveTo(offset + size, offset);
                path.LineTo(offset + size - arrowSize, offset - arrowSize);
                path.LineTo(offset + size - arrowSize, offset + arrowSize);
                path.Close();
                canvas.DrawPath(path, _arrowPaint);
            }

            using (var path = new SKPath())
            {
                path.MoveTo(offset, offset + size);
                path.LineTo(offset - arrowSize, offset + size - arrowSize);
                path.LineTo(offset + arrowSize, offset + size - arrowSize);
                path.Close();
                canvas.DrawPath(path, _arrowPaint);
            }

            canvas.DrawText("X", offset + size + 2, offset, _textFont, _textPaint);

            canvas.Save();
            canvas.Scale(1, -1);
            canvas.DrawText("Y", offset, -(offset + size + 12), _textFont, _textPaint);
            canvas.Restore();
        }
        finally
        {
            canvas.Restore();
        }
    }
}
