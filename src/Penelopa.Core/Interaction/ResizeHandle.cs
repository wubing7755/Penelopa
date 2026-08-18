namespace Penelopa.Core.Interaction;

/// <summary>
/// The four corner handles of a selection box. Resizing keeps the opposite
/// corner fixed while the dragged corner follows the pointer.
/// </summary>
public enum ResizeHandle
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
}
