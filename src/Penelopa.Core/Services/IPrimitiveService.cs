using Penelopa.Core.Primitives;

namespace Penelopa.Core.Services;

/// <summary>
/// Manages the collection of primitives and the current selection.
/// </summary>
public interface IPrimitiveService
{
    void Add(Primitive primitive);
    IEnumerable<Primitive> GetAll();
    void SetSelected(Primitive primitive);
    void SetSelectedRange(IEnumerable<Primitive> primitives);
    void AppendSelected(Primitive primitive);
    void ClearSelection();
    IEnumerable<Primitive> GetSelection();

    event Action<Primitive>? OnChange;
    event Action<IEnumerable<Primitive>>? OnSelectionChanged;
}
