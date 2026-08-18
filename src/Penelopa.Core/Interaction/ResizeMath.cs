using Penelopa.Core.Alignment;

namespace Penelopa.Core.Interaction;

/// <summary>
/// Resize math shared by the interaction layer: given the original bounds,
/// the dragged handle, and the pointer position in world space, computes the
/// new bounds with the opposite corner fixed. Dragging across the fixed
/// corner flips the box (mirror), and a minimum size keeps the shape from
/// collapsing.
/// </summary>
public static class ResizeMath
{
    /// <summary>Minimum resized width in world units.</summary>
    public const float MinWidth = 1f;

    /// <summary>Minimum resized height in world units.</summary>
    public const float MinHeight = 1f;

    /// <summary>
    /// Computes the new bounds for a resize gesture.
    /// </summary>
    /// <param name="original">The bounds captured at pointer-down.</param>
    /// <param name="handle">The handle being dragged.</param>
    /// <param name="pointer">The pointer position in world space.</param>
    /// <returns>The resized bounds; the fixed corner never moves.</returns>
    public static Box ComputeBounds(Box original, ResizeHandle handle, Point pointer)
    {
        var fixedCorner = FixedCorner(original, handle);

        float minX = MathF.Min(fixedCorner.X, pointer.X);
        float maxX = MathF.Max(fixedCorner.X, pointer.X);
        float minY = MathF.Min(fixedCorner.Y, pointer.Y);
        float maxY = MathF.Max(fixedCorner.Y, pointer.Y);

        // Clamp to the minimum size, anchored on the fixed corner.
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

    /// <summary>Gets the corner opposite the dragged handle, which stays fixed.</summary>
    public static Point FixedCorner(Box bounds, ResizeHandle handle) => handle switch
    {
        ResizeHandle.TopLeft => new Point(bounds.MaxX, bounds.MinY),
        ResizeHandle.TopRight => new Point(bounds.MinX, bounds.MinY),
        ResizeHandle.BottomLeft => new Point(bounds.MaxX, bounds.MaxY),
        ResizeHandle.BottomRight => new Point(bounds.MinX, bounds.MaxY),
        _ => throw new ArgumentOutOfRangeException(nameof(handle)),
    };
}
