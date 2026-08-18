using Penelopa.Core.Alignment;

namespace Penelopa.Core.Interaction;

/// <summary>
/// 缩放的数学逻辑：给定原始包围盒、拖动的角柄和世界坐标指针位置，
/// 计算新包围盒（固定对角）。拖过固定角会翻转（镜像），最小尺寸防止形状坍塌。
/// </summary>
public static class ResizeMath
{
    /// <summary>缩放最小宽度（世界单位）。</summary>
    public const float MinWidth = 1f;

    /// <summary>缩放最小高度（世界单位）。</summary>
    public const float MinHeight = 1f;

    /// <summary>计算缩放后的新包围盒。</summary>
    /// <param name="original">按下时捕获的包围盒。</param>
    /// <param name="handle">正在拖动的角柄。</param>
    /// <param name="pointer">指针的世界坐标位置。</param>
    /// <returns>缩放后的包围盒；固定角始终不动。</returns>
    public static Box ComputeBounds(Box original, ResizeHandle handle, Point pointer)
    {
        var fixedCorner = FixedCorner(original, handle);

        float minX = MathF.Min(fixedCorner.X, pointer.X);
        float maxX = MathF.Max(fixedCorner.X, pointer.X);
        float minY = MathF.Min(fixedCorner.Y, pointer.Y);
        float maxY = MathF.Max(fixedCorner.Y, pointer.Y);

        // 限制最小尺寸，锚定在固定角
        if (maxX - minX < MinWidth)
        {
            if (pointer.X >= fixedCorner.X)
            {
                maxX = fixedCorner.X + MinWidth;
            }
            else
            {
                minX = fixedCorner.X - MinWidth;
            }
        }

        if (maxY - minY < MinHeight)
        {
            if (pointer.Y >= fixedCorner.Y)
            {
                maxY = fixedCorner.Y + MinHeight;
            }
            else
            {
                minY = fixedCorner.Y - MinHeight;
            }
        }

        return new Box(minX, minY, maxX, maxY);
    }

    /// <summary>获取拖动角柄的对角（固定不动的角）。</summary>
    public static Point FixedCorner(Box bounds, ResizeHandle handle) => handle switch
    {
        ResizeHandle.TopLeft => new Point(bounds.MaxX, bounds.MinY),
        ResizeHandle.TopRight => new Point(bounds.MinX, bounds.MinY),
        ResizeHandle.BottomLeft => new Point(bounds.MaxX, bounds.MaxY),
        ResizeHandle.BottomRight => new Point(bounds.MinX, bounds.MaxY),
        _ => throw new ArgumentOutOfRangeException(nameof(handle)),
    };
}
