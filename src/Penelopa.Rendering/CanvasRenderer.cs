using Penelopa.Core.Alignment;
using Penelopa.Core.Interaction;
using Penelopa.Core.Primitives;
using SkiaSharp;

namespace Penelopa.Rendering;

/// <summary>
/// 将图元渲染到渲染目标尺寸的位图，并执行颜色键命中测试。可见画布与屏下命中缓冲区共享同一个
/// <see cref="ViewTransform"/>（原点在左下角，Y 向上，按设备像素比缩放），因此鼠标的 CSS 屏幕像素
/// 通过与定位可见内容相同的变换映射到命中缓冲区。渲染目标尺寸由宿主从 SKGLView 事件
/// (<c>e.Info</c>) 提供，不从画布设备边界推断。
/// </summary>
public sealed class CanvasRenderer
{
    private const int FallbackCanvasSize = 512;

    private SKBitmap _drawBitmap = new(new SKImageInfo(FallbackCanvasSize, FallbackCanvasSize));
    private SKBitmap _hitBitmap = new(new SKImageInfo(FallbackCanvasSize, FallbackCanvasSize));
    private ViewTransform _viewTransform = new(FallbackCanvasSize, FallbackCanvasSize, 1f);

    // 颜色键按帧分配（见 DrawPrimitives）：命中缓冲区每帧重建，
    // 因此键只需在单次 渲染→命中 周期内一致。不需要持久全局注册表。
    private readonly Dictionary<uint, Primitive> _colorMap = new();
    private uint _nextColorKey = FirstColorKey;

    private const uint FirstColorKey = 0xFF000001;

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

    /// <summary>将图元渲染到画布并刷新命中缓冲区。</summary>
    /// <param name="canvas">SKGLView 绘制事件的目标画布。</param>
    /// <param name="info">用户可见的渲染目标尺寸
    /// (<c>SKPaintGLSurfaceEventArgs.Info</c>，<c>IgnorePixelScaling</c> 为 false 时为物理像素)。
    /// 不要传缩放表面的原始 info，否则坐标空间会不一致。</param>
    /// <param name="devicePixelRatio">CSS 到物理像素的比率（<c>window.devicePixelRatio</c>）。</param>
    /// <param name="primitives">要绘制的图元。</param>
    /// <param name="selection">当前选区；非空时在最上层绘制选区框（单项时含角柄）。</param>
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

    /// <summary>当前视口变换（缩放/平移状态）。</summary>
    public ViewTransform CurrentViewTransform => _viewTransform;

    /// <summary>替换视口变换（缩放/平移）。</summary>
    public void SetViewTransform(ViewTransform transform) => _viewTransform = transform;

    /// <summary>缩放到 <paramref name="newZoom"/>，保持 CSS 光标下的世界点固定（围绕指针滚轮缩放）。</summary>
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

    /// <summary>按 CSS 像素增量平移视口。</summary>
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

    /// <summary>将内容边界适配到视口（带内边距），居中显示内容（缩放可缩小或放大；空内容不变）。</summary>
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

    /// <summary>更新后续命中测试使用的设备像素比，使指针坐标无需等待下一渲染帧即可与最新显示比例同步。</summary>
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

    /// <summary>返回 CSS 屏幕像素下的图元，像素为空时返回 null。坐标为浏览器事件在画布上的坐标（原点在左上角，Y 向下）。</summary>
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
        return _colorMap.TryGetValue(colorKey, out var primitive) ? primitive : null;
    }

    /// <summary>测试 CSS 屏幕点是否命中选区框的角柄（屏幕空间）。角柄为固定大小的屏幕元素，测试使用 CSS 像素，与世界缩放无关。</summary>
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

    /// <summary>测试 CSS 屏幕点是否在投影到屏幕空间的世界包围盒内（多选并集框，用作整组拖拽柄）。</summary>
    public bool HitTestUnionBox(float screenCssX, float screenCssY, Box worldBounds)
    {
        var topLeft = ToCss(new Point(worldBounds.MinX, worldBounds.MaxY));
        var bottomRight = ToCss(new Point(worldBounds.MaxX, worldBounds.MinY));
        return screenCssX >= topLeft.X && screenCssX <= bottomRight.X
            && screenCssY >= topLeft.Y && screenCssY <= bottomRight.Y;
    }

    /// <summary>交互层的分层命中测试：角柄（单选）→ 图元颜色键 → 多选并集框。</summary>
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

    /// <summary>CSS 屏幕点 → 世界坐标。</summary>
    public Point CssToWorld(float screenCssX, float screenCssY)
    {
        var view = _viewTransform.ScreenToView(screenCssX, screenCssY);
        var world = _viewTransform.ViewToWorld(view.X, view.Y);
        return new Point(world.X, world.Y);
    }

    /// <summary>
    /// 收集某点的钻取候选：颜色键缓冲区的最顶层图元 + 其祖先链，
    /// 从最外层容器（根）到最深叶子排序。确认的钻取方向：首击选最外层容器，后续点击向内层形状深入。
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
            // 世界模式：1 世界单位 = 1 CSS 像素，因此以 HandleSizeCss 单位绘制的角柄在屏幕上固定大小
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
        // 世界模式：1 世界单位 = 1 CSS 像素 × Zoom，除以 Zoom 保持轮廓宽度和角柄大小在屏幕上固定
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

            // 坐标轴指示器在图元下方；不进入命中缓冲区，保持轴不可交互
            DrawCoordinateSystem(canvas);

            // 绘制前重建每帧颜色键映射，使命中缓冲区与查找表在本帧内保持同步
            _colorMap.Clear();
            _nextColorKey = FirstColorKey;

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
    /// 绘制图元树节点。容器保存画布状态，将变换应用到两个画布（可见画布和命中画布必须同步，
    /// 使颜色键命中测试与绘制内容一致），递归子元素后恢复。渲染顺序即 Z 序：后绘制覆盖先绘制。
    /// </summary>
    private void DrawNode(SKCanvas canvas, SKCanvas hitCanvas, Primitive node)
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
                DrawHitCircle(hitCanvas, circle, RegisterHitKey(circle));
                break;
            case Rectangle rectangle:
                DrawRectangle(canvas, rectangle);
                DrawHitRectangle(hitCanvas, rectangle, RegisterHitKey(rectangle));
                break;
            case Triangle triangle:
                DrawTriangle(canvas, triangle);
                DrawHitTriangle(hitCanvas, triangle, RegisterHitKey(triangle));
                break;
        }
    }

    /// <summary>
    /// 为图元分配下一个每帧颜色键并记录 <see cref="HitTest"/> 使用的映射。
    /// 键仅在下次渲染前有效，即命中缓冲区的生命周期。
    /// </summary>
    private uint RegisterHitKey(Primitive node)
    {
        var key = _nextColorKey++;
        _colorMap[key] = node;
        return key;
    }

    /// <summary>Core 仿射变换 → Skia 矩阵。</summary>
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

    private static void DrawHitCircle(SKCanvas hitCanvas, Circle circle, uint colorKey)
    {
        var color = Color.FromUint(colorKey);
        using var paint = new SKPaint
        {
            Color = new SKColor(color.R, color.G, color.B, color.A),
            Style = SKPaintStyle.Fill,
            // 命中缓冲区必须精确：抗锯齿会将边缘像素混入不匹配任何注册键的颜色，
            // 使 1px 边缘不可命中并破坏顶部像素/候选一致性
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

    private static void DrawHitRectangle(SKCanvas hitCanvas, Rectangle rectangle, uint colorKey)
    {
        var color = Color.FromUint(colorKey);
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

    private static void DrawHitTriangle(SKCanvas hitCanvas, Triangle triangle, uint colorKey)
    {
        var color = Color.FromUint(colorKey);
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

    /// <summary>世界模式下绘制坐标系：原点 (0,0) 在画布中心，X/Y 轴跨越整个画布，中心标记显式显示原点。</summary>
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

        // 文本在世界 Y 翻转下倒置；翻转一次使标签正常阅读
        canvas.Save();
        canvas.Scale(1, -1);
        canvas.DrawText("X", halfW - 16, 12, _textFont, _textPaint);
        canvas.DrawText("Y", 6, -(halfH - 10), _textFont, _textPaint);
        canvas.Restore();
    }
}
