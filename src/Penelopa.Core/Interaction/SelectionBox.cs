using Penelopa.Core.Alignment;

namespace Penelopa.Core.Interaction;

/// <summary>
/// Geometry of the selection box shown around a selected primitive: the
/// bounding-box outline plus the four corner handles. Handles are positioned
/// in world space; rendering converts them to screen space with the view
/// transform and gives them a fixed pixel size.
/// </summary>
public static class SelectionBox
{
    /// <summary>Gets the world-space anchor point of a corner handle.</summary>
    /// <remarks>World Y grows up, so Top corners use <see cref="Box.MaxY"/>.</remarks>
    public static Point HandlePoint(Box bounds, ResizeHandle handle) => handle switch
    {
        ResizeHandle.TopLeft => new Point(bounds.MinX, bounds.MaxY),
        ResizeHandle.TopRight => new Point(bounds.MaxX, bounds.MaxY),
        ResizeHandle.BottomLeft => new Point(bounds.MinX, bounds.MinY),
        ResizeHandle.BottomRight => new Point(bounds.MaxX, bounds.MinY),
        _ => throw new ArgumentOutOfRangeException(nameof(handle)),
    };

    /// <summary>Gets all handles in clockwise order starting at the top-left.</summary>
    public static IReadOnlyList<ResizeHandle> AllHandles { get; } = new[]
    {
        ResizeHandle.TopLeft,
        ResizeHandle.TopRight,
        ResizeHandle.BottomRight,
        ResizeHandle.BottomLeft,
    };
}
