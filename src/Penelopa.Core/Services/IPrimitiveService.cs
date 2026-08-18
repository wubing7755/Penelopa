using Penelopa.Core.Primitives;

namespace Penelopa.Core.Services;

/// <summary>管理图元集合和当前选区。</summary>
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

    /// <summary>通知指定图元的几何已变更。</summary>
    void NotifyPrimitivesChanged(IEnumerable<Primitive> primitives);

    event Action<Primitive>? OnChange;
    event Action? OnCollectionChanged;
    event Action<IEnumerable<Primitive>>? OnSelectionChanged;
    event Action<IEnumerable<Primitive>>? OnPrimitiveChanged;
}
