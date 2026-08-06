namespace Penelopa.Core.Alignment;

/// <summary>
/// Contract for objects that can be aligned in world space.
/// </summary>
public interface IAlignable
{
    /// <summary>Gets the world-space axis-aligned bounding box (AABB).</summary>
    Box GetWorldBoundingBox();

    /// <summary>Gets the world-space transform.</summary>
    Transform GetWorldTransform();

    /// <summary>Sets the world-space transform.</summary>
    void SetWorldTransform(Transform transform);
}
