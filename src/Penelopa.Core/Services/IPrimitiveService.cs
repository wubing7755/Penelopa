using Penelopa.Core.Primitives;

namespace Penelopa.Core.Services;

/// <summary>
/// Manages the collection of primitives and the current selection.
/// </summary>
public interface IPrimitiveService
{
    void Add(Primitive primitive);
    void Remove(Primitive primitive);
    IEnumerable<Primitive> GetAll();
    void AddToContainer(Container container, Primitive child);
    void ReplaceAll(IReadOnlyList<Primitive> primitives, bool clearHistory = true);
    void Undo();
    void Redo();
    bool CanUndo { get; }
    bool CanRedo { get; }
    void CaptureForGesture();
    void SetSelected(Primitive primitive);
    void SetSelectedRange(IEnumerable<Primitive> primitives);
    void AppendSelected(Primitive primitive);
    void ClearSelection();
    IEnumerable<Primitive> GetSelection();

    /// <summary>Announces that the given primitives' geometry changed.</summary>
    void NotifyPrimitivesChanged(IEnumerable<Primitive> primitives);

    event Action<Primitive>? OnChange;
    event Action? OnCollectionChanged;
    event Action<IEnumerable<Primitive>>? OnSelectionChanged;
    event Action<IEnumerable<Primitive>>? OnPrimitiveChanged;
}
