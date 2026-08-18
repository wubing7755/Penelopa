namespace Penelopa.Core.Alignment;

/// <summary>
/// Contract for objects that can be aligned in world space. Alignment only
/// translates, so the interface exposes the anchor position rather than a
/// full affine transform.
/// </summary>
public interface IAlignable
{
    /// <summary>Gets the world-space axis-aligned bounding box (AABB).</summary>
    Box GetWorldBoundingBox();

    /// <summary>Gets the world-space anchor position (the AABB's top-left corner).</summary>
    Point GetWorldPosition();

    /// <summary>Translates the item so its anchor lands at the given world position.</summary>
    void SetWorldPosition(Point position);
}
