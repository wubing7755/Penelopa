using Penelopa.Core.Primitives;

namespace Penelopa.Core.Interaction;

/// <summary>
/// The editing-side surface the interaction controller drives: selection
/// management and geometry-change notification. Implemented by the host
/// component so the controller stays free of Blazor/Skia dependencies and
/// remains unit-testable.
/// </summary>
public interface IEditorInteractionHost
{
    /// <summary>Gets the current selection.</summary>
    IReadOnlyList<Primitive> GetSelection();

    /// <summary>Gets whether the primitive is currently selected.</summary>
    bool IsSelected(Primitive primitive);

    /// <summary>Selects only the given primitive.</summary>
    void SetSelected(Primitive primitive);

    /// <summary>Adds the primitive to the selection.</summary>
    void AppendSelected(Primitive primitive);

    /// <summary>Removes the primitive from the selection (or selects it when absent).</summary>
    void ToggleSelected(Primitive primitive);

    /// <summary>Clears the selection.</summary>
    void ClearSelection();

    /// <summary>
    /// Pans the view by a world-space delta (the host converts to CSS pixels
    /// using the current zoom). Used by the empty-space drag gesture.
    /// </summary>
    void PanByWorld(float deltaX, float deltaY);

    /// <summary>
    /// Announces that the given primitives' geometry changed (drag/resize
    /// commit), so panels can refresh.
    /// </summary>
    void NotifyPrimitivesChanged(IReadOnlyList<Primitive> primitives);
}
