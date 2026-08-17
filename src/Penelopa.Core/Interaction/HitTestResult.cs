using Penelopa.Core.Primitives;

namespace Penelopa.Core.Interaction;

/// <summary>
/// The layered result of a pointer hit test, computed by the renderer before
/// the interaction controller decides what the gesture means:
/// a corner handle (highest priority), a primitive body, or the
/// multi-selection union box (lowest priority, used as a group-drag handle).
/// </summary>
public readonly struct HitTestResult
{
    /// <summary>Gets the corner handle hit, if any.</summary>
    public ResizeHandle? Handle { get; init; }

    /// <summary>Gets the primitive body hit by the color-key buffer, if any.</summary>
    public Primitive? Primitive { get; init; }

    /// <summary>
    /// Gets the drill-down candidates at the point — the hit primitive and
    /// its ancestor chain, ordered root → deepest leaf (the confirmed drill
    /// direction). Null when nothing was hit.
    /// </summary>
    public IReadOnlyList<Primitive>? Candidates { get; init; }

    /// <summary>Gets whether the point is inside the multi-selection union box.</summary>
    public bool InUnionBox { get; init; }
}
