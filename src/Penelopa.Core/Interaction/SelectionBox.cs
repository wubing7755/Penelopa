using Penelopa.Core.Alignment;

namespace Penelopa.Core.Interaction;

/// <summary>
/// 选中图元周围的选区框几何：包围盒轮廓 + 四个角柄。
/// 角柄在世界空间定位，渲染层通过视口变换转换为屏幕像素并保持固定大小。
/// </summary>
public static class SelectionBox
{
    /// <summary>获取角柄的世界坐标锚点。</summary>
    /// <remarks>世界 Y 轴向上增长，因此顶部角使用 <see cref="Box.MaxY"/>。</remarks>
    public static Point HandlePoint(Box bounds, ResizeHandle handle) => handle switch
    {
        ResizeHandle.TopLeft => new Point(bounds.MinX, bounds.MaxY),
        ResizeHandle.TopRight => new Point(bounds.MaxX, bounds.MaxY),
        ResizeHandle.BottomLeft => new Point(bounds.MinX, bounds.MinY),
        ResizeHandle.BottomRight => new Point(bounds.MaxX, bounds.MinY),
        _ => throw new ArgumentOutOfRangeException(nameof(handle)),
    };

    /// <summary>所有角柄（从左上角开始顺时针排列）。</summary>
    public static IReadOnlyList<ResizeHandle> AllHandles { get; } = new[]
    {
        ResizeHandle.TopLeft,
        ResizeHandle.TopRight,
        ResizeHandle.BottomRight,
        ResizeHandle.BottomLeft,
    };
}
