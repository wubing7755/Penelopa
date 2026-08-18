using SkiaSharp;

namespace Penelopa.Rendering;

/// <summary>
/// 世界坐标（图元模型空间，Y 轴向上）与视口坐标（渲染目标像素，Y 轴向下）之间的映射。
/// 世界原点 (0,0) 在画布中心。视口以物理分辨率渲染：1 世界单位 = 1 CSS 像素，
/// 渲染目标尺寸为 CSS 尺寸 × <c>DevicePixelRatio</c>。渲染和命中测试共享同一实例，
/// 确保所见即所击，在任何 DPI 下一致。
/// </summary>
/// <remarks>
/// 未来视口能力（缩放、平移）扩展此结构：调整 <see cref="WorldToView"/>、<see cref="ViewToWorld"/>
/// 和 <see cref="ApplyTo"/> 中的缩放/偏移项；渲染和命中测试调用点不变。
/// </remarks>
public readonly struct ViewTransform
{
    /// <summary>渲染目标宽度（物理像素）。</summary>
    public int ViewWidth { get; }

    /// <summary>渲染目标高度（物理像素）。</summary>
    public int ViewHeight { get; }

    /// <summary>设备像素比（物理像素 / CSS 像素）。</summary>
    public float DevicePixelRatio { get; }

    /// <summary>缩放因子（1 = 100%，默认视图）。</summary>
    public float Zoom { get; }

    /// <summary>视口平移 X（CSS 像素）：世界原点相对画布中心的偏移。</summary>
    public float PanX { get; }

    /// <summary>视口平移 Y（CSS 像素）。</summary>
    public float PanY { get; }

    public ViewTransform(
        int viewWidth,
        int viewHeight,
        float devicePixelRatio,
        float zoom = 1f,
        float panX = 0f,
        float panY = 0f)
    {
        if (viewWidth <= 0) throw new ArgumentOutOfRangeException(nameof(viewWidth));
        if (viewHeight <= 0) throw new ArgumentOutOfRangeException(nameof(viewHeight));
        if (devicePixelRatio <= 0) throw new ArgumentOutOfRangeException(nameof(devicePixelRatio));
        if (zoom <= 0f) throw new ArgumentOutOfRangeException(nameof(zoom));

        ViewWidth = viewWidth;
        ViewHeight = viewHeight;
        DevicePixelRatio = devicePixelRatio;
        Zoom = zoom;
        PanX = panX;
        PanY = panY;
    }

    /// <summary>世界点 → 视口像素。</summary>
    /// <remarks>世界原点 (0,0) 映射到画布中心 + 平移偏移；<see cref="Zoom"/> 缩放世界。</remarks>
    public SKPoint WorldToView(float worldX, float worldY)
        => new(
            ViewWidth / 2f + (PanX + worldX * Zoom) * DevicePixelRatio,
            ViewHeight / 2f + (PanY - worldY * Zoom) * DevicePixelRatio);

    /// <summary>视口像素 → 世界点。</summary>
    public SKPoint ViewToWorld(float viewX, float viewY)
        => new(
            ((viewX - ViewWidth / 2f) / DevicePixelRatio - PanX) / Zoom,
            ((ViewHeight / 2f - viewY) / DevicePixelRatio + PanY) / Zoom);

    /// <summary>CSS 屏幕像素（浏览器事件坐标）→ 视口像素。</summary>
    public SKPoint ScreenToView(float screenX, float screenY)
        => new(screenX * DevicePixelRatio, screenY * DevicePixelRatio);

    /// <summary>视口像素 → CSS 屏幕坐标。</summary>
    public SKPoint ViewToScreen(float viewX, float viewY)
        => new(viewX / DevicePixelRatio, viewY / DevicePixelRatio);

    /// <summary>
    /// 将画布置为世界坐标绘制模式：世界原点 (0,0) 落在画布中心 + 平移偏移，Y 轴向上，
    /// 设备像素比和 <see cref="Zoom"/> 同时缩放世界。之后以世界坐标绘制即落在正确的物理像素上。
    /// </summary>
    public void ApplyTo(SKCanvas canvas)
    {
        canvas.Translate(
            ViewWidth / 2f + PanX * DevicePixelRatio,
            ViewHeight / 2f + PanY * DevicePixelRatio);
        canvas.Scale(DevicePixelRatio * Zoom, -DevicePixelRatio * Zoom);
    }
}
