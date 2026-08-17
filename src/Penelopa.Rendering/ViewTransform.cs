using SkiaSharp;

namespace Penelopa.Rendering;

/// <summary>
/// Maps between world coordinates (primitive model space, Y grows up) and
/// view coordinates (the render target's pixels, Y grows down). The world
/// origin (0,0) is the canvas center. The view is rendered at physical
/// resolution: one world unit equals one CSS pixel, so the render target is
/// <c>DevicePixelRatio</c> times the CSS size. Rendering and hit testing
/// share the same instance so what the user sees is always what the pointer
/// hits, at any display DPI.
/// </summary>
/// <remarks>
/// Future view capabilities (zoom, pan) extend this structure: adjust the
/// scale/offset terms in <see cref="WorldToView"/>, <see cref="ViewToWorld"/>
/// and <see cref="ApplyTo"/>; render and hit-test call sites do not change.
/// </remarks>
public readonly struct ViewTransform
{
    /// <summary>Gets the render target width in physical pixels.</summary>
    public int ViewWidth { get; }

    /// <summary>Gets the render target height in physical pixels.</summary>
    public int ViewHeight { get; }

    /// <summary>Gets the device pixel ratio (CSS pixels per physical pixel).</summary>
    public float DevicePixelRatio { get; }

    /// <summary>Gets the zoom factor (1 = 100%, the default view).</summary>
    public float Zoom { get; }

    /// <summary>
    /// Gets the view pan in CSS pixels: the world origin's offset from the
    /// canvas center.
    /// </summary>
    public float PanX { get; }

    /// <summary>Gets the view pan in CSS pixels (Y axis).</summary>
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

    /// <summary>Maps a world point to a view (render target) pixel.</summary>
    /// <remarks>The world origin (0,0) maps to the canvas center plus the pan
    /// offset; <see cref="Zoom"/> scales the world.</remarks>
    public SKPoint WorldToView(float worldX, float worldY)
        => new(
            ViewWidth / 2f + (PanX + worldX * Zoom) * DevicePixelRatio,
            ViewHeight / 2f + (PanY - worldY * Zoom) * DevicePixelRatio);

    /// <summary>Maps a view (render target) pixel back to world space.</summary>
    public SKPoint ViewToWorld(float viewX, float viewY)
        => new(
            ((viewX - ViewWidth / 2f) / DevicePixelRatio - PanX) / Zoom,
            ((ViewHeight / 2f - viewY) / DevicePixelRatio + PanY) / Zoom);

    /// <summary>Maps a CSS screen pixel (browser event coordinates) to a view pixel.</summary>
    public SKPoint ScreenToView(float screenX, float screenY)
        => new(screenX * DevicePixelRatio, screenY * DevicePixelRatio);

    /// <summary>Maps a view pixel back to CSS screen coordinates.</summary>
    public SKPoint ViewToScreen(float viewX, float viewY)
        => new(viewX / DevicePixelRatio, viewY / DevicePixelRatio);

    /// <summary>
    /// Puts the canvas into world-coordinate drawing mode: the world origin
    /// (0,0) lands at the canvas center plus the pan offset, Y grows up, and
    /// both the device pixel ratio and <see cref="Zoom"/> scale the world.
    /// Drawing in world coordinates then lands on the correct physical
    /// pixels.
    /// </summary>
    public void ApplyTo(SKCanvas canvas)
    {
        canvas.Translate(
            ViewWidth / 2f + PanX * DevicePixelRatio,
            ViewHeight / 2f + PanY * DevicePixelRatio);
        canvas.Scale(DevicePixelRatio * Zoom, -DevicePixelRatio * Zoom);
    }
}
