using Penelopa.Core.Primitives;

namespace Penelopa.Core.Services;

/// <summary><see cref="IPrimitiveService"/> 的默认实现，基于列表存储。</summary>
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

    /// <summary>将子图元加入容器。子图元从根列表移除，渲染和树面板仅在容器内显示。</summary>
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
            _selection.Remove(primitive);
            OnCollectionChanged?.Invoke();
            return;
        }

        if (!_primitives.Remove(primitive))
        {
            return;
        }

        _selection.Remove(primitive);
        OnCollectionChanged?.Invoke();
    }

    public IEnumerable<Primitive> GetAll()
    {
        return _primitives;
    }

    /// <summary>替换整个文档（加载）。释放旧树的颜色键，安装新根列表，清空选区；调用方按 Id 重新选择。</summary>
    public void ReplaceAll(IReadOnlyList<Primitive> primitives, bool clearHistory = true)
    {
        if (clearHistory)
        {
            _history.Clear();
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

    /// <summary>画布手势开始前捕获当前状态。</summary>
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
