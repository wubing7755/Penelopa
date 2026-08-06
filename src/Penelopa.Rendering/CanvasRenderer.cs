using Penelopa.Core.Primitives;
using SkiaSharp;

namespace Penelopa.Rendering;

/// <summary>
/// Renders primitives onto a fixed-size canvas and performs color-key hit
/// testing. The visible canvas and the off-screen hit buffer share the same
/// y-flip transform (origin at bottom-left, y grows up), so a screen pixel
/// from the mouse maps directly into the hit buffer.
/// </summary>
public sealed class CanvasRenderer
{
    private const int FallbackCanvasSize = 512;

    private SKBitmap _drawBitmap = new(new SKImageInfo(FallbackCanvasSize, FallbackCanvasSize));
    private SKBitmap _hitBitmap = new(new SKImageInfo(FallbackCanvasSize, FallbackCanvasSize));
    private int _width = FallbackCanvasSize;
    private int _height = FallbackCanvasSize;

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
    public void Render(SKCanvas canvas, IReadOnlyList<Primitive> primitives)
    {
        EnsureBuffersFor(canvas);
        canvas.Clear(SKColors.Black);
        DrawPrimitives(primitives);
        canvas.DrawBitmap(_drawBitmap, 0, 0);
        DrawCoordinateSystem(canvas);
    }

    private void EnsureBuffersFor(SKCanvas canvas)
    {
        var bounds = canvas.DeviceClipBounds;
        var width = bounds.Width > 0 ? bounds.Width : FallbackCanvasSize;
        var height = bounds.Height > 0 ? bounds.Height : FallbackCanvasSize;
        if (_drawBitmap.Width == width && _drawBitmap.Height == height)
        {
            return;
        }

        _drawBitmap.Dispose();
        _hitBitmap.Dispose();
        _drawBitmap = new SKBitmap(new SKImageInfo(width, height));
        _hitBitmap = new SKBitmap(new SKImageInfo(width, height));
        _width = width;
        _height = height;
    }

    /// <summary>
    /// Returns the primitive under a screen pixel, or null when the pixel is
    /// empty. The screen coordinates are the same ones reported by a mouse
    /// event over the canvas (origin at top-left, y grows down).
    /// </summary>
    public Primitive? HitTest(int screenX, int screenY)
    {
        if ((uint)screenX >= (uint)_hitBitmap.Width || (uint)screenY >= (uint)_hitBitmap.Height)
        {
            return null;
        }

        var color = _hitBitmap.GetPixel(screenX, screenY);
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
            // Flip to world coordinates: origin at bottom-left, y grows up.
            canvas.Translate(0, _height);
            canvas.Scale(1, -1);
            hitCanvas.Translate(0, _height);
            hitCanvas.Scale(1, -1);

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
        const int size = 30;
        const int offset = 10;

        canvas.Save();
        try
        {
            canvas.Translate(0, _height);
            canvas.Scale(1, -1);

            float originX = offset;
            float originY = offset;

            canvas.DrawLine(originX, originY, originX + size, originY, _axisPaint);
            canvas.DrawLine(originX, originY, originX, originY + size, _axisPaint);

            float arrowSize = 4;
            using (var path = new SKPath())
            {
                path.MoveTo(originX + size, originY);
                path.LineTo(originX + size - arrowSize, originY - arrowSize);
                path.LineTo(originX + size - arrowSize, originY + arrowSize);
                path.Close();
                canvas.DrawPath(path, _arrowPaint);
            }

            using (var path = new SKPath())
            {
                path.MoveTo(originX, originY + size);
                path.LineTo(originX - arrowSize, originY + size - arrowSize);
                path.LineTo(originX + arrowSize, originY + size - arrowSize);
                path.Close();
                canvas.DrawPath(path, _arrowPaint);
            }

            canvas.DrawText("X", originX + size + 2, originY, _textFont, _textPaint);

            canvas.Save();
            canvas.Scale(1, -1);
            canvas.DrawText("Y", originX, -(originY + size + 12), _textFont, _textPaint);
            canvas.Restore();
        }
        finally
        {
            canvas.Restore();
        }
    }
}
