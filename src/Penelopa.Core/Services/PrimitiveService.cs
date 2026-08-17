using Penelopa.Core.Primitives;

namespace Penelopa.Core.Services;

/// <summary>
/// Default <see cref="IPrimitiveService"/> implementation backed by lists.
/// </summary>
public class PrimitiveService : IPrimitiveService
{
    private readonly List<Primitive> _primitives = new();
    private readonly HashSet<Primitive> _selection = new();
    private readonly DocumentHistory _history;

    public PrimitiveService()
    {
        _history = new DocumentHistory(this);
    }

    public void Add(Primitive primitive)
    {
        _history.Capture();
        _primitives.Add(primitive);
        OnChange?.Invoke(primitive);
    }

    /// <summary>
    /// Adds a child into a container. The child leaves the root list, so
    /// rendering and the tree panel see it only inside its container.
    /// </summary>
    public void AddToContainer(Container container, Primitive child)
    {
        _history.Capture();
        _primitives.Remove(child);
        container.AddChild(child);
        OnCollectionChanged?.Invoke();
    }

    public void Remove(Primitive primitive)
    {
        _history.Capture();
        if (primitive.Parent is Container parent)
        {
            parent.RemoveChild(primitive);
            ReleaseSubtreeKeys(primitive);
            OnCollectionChanged?.Invoke();
            return;
        }

        if (!_primitives.Remove(primitive))
        {
            return;
        }

        _selection.Remove(primitive);
        ReleaseSubtreeKeys(primitive);
        OnCollectionChanged?.Invoke();
    }

    private static void ReleaseSubtreeKeys(Primitive root)
    {
        if (root is Container container)
        {
            foreach (var child in container.Children)
            {
                ReleaseSubtreeKeys(child);
            }
        }

        ColorKeyManager.ReleaseColorKey(root.ColorKey.Value);
    }

    public IEnumerable<Primitive> GetAll()
    {
        return _primitives;
    }

    /// <summary>
    /// Replaces the whole document (load). Releases color keys of the old
    /// tree, installs the new roots, and clears the selection; the caller
    /// re-selects via ids against the new tree.
    /// </summary>
    public void ReplaceAll(IReadOnlyList<Primitive> primitives, bool clearHistory = true)
    {
        if (clearHistory)
        {
            _history.Clear();
        }

        foreach (var primitive in _primitives)
        {
            ReleaseSubtreeKeys(primitive);
        }

        _primitives.Clear();
        _primitives.AddRange(primitives);
        _selection.Clear();
        OnCollectionChanged?.Invoke();
    }

    public void Undo() => _history.Undo();

    public void Redo() => _history.Redo();

    public bool CanUndo => _history.CanUndo;

    public bool CanRedo => _history.CanRedo;

    /// <summary>Captures the current state before a canvas gesture starts.</summary>
    public void CaptureForGesture() => _history.Capture();

    public void SetSelected(Primitive primitive)
    {
        _selection.Clear();
        _selection.Add(primitive);
        OnSelectionChanged?.Invoke(_selection);
    }

    public void SetSelectedRange(IEnumerable<Primitive> primitives)
    {
        _selection.Clear();
        foreach (var p in primitives)
        {
            _selection.Add(p);
        }
        OnSelectionChanged?.Invoke(_selection);
    }

    public void AppendSelected(Primitive primitive)
    {
        _selection.Add(primitive);
        OnSelectionChanged?.Invoke(_selection);
    }

    public void ClearSelection()
    {
        _selection.Clear();
        OnSelectionChanged?.Invoke(_selection);
    }

    public IEnumerable<Primitive> GetSelection() => _selection;

    public void NotifyPrimitivesChanged(IEnumerable<Primitive> primitives)
        => OnPrimitiveChanged?.Invoke(primitives);

    public event Action<Primitive>? OnChange;
    public event Action? OnCollectionChanged;
    public event Action<IEnumerable<Primitive>>? OnSelectionChanged;
    public event Action<IEnumerable<Primitive>>? OnPrimitiveChanged;
}
