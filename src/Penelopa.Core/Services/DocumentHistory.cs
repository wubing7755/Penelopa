using Penelopa.Core.Primitives;

namespace Penelopa.Core.Services;

/// <summary>
/// 文档撤销/重做历史。快照按引用捕获图元树及每个图元的属性值和容器子元素；
/// 应用快照时恢复这些值并通过 <see cref="IPrimitiveService.ReplaceAll"/> 重建根列表。
/// 选区是 UI 状态而非文档状态：不被捕获，撤销/重做保留恢复后仍存活的选区。
/// 快照仅在真实修改前捕获（结构变更或拖拽/缩放手势开始），非修改性点击不产生撤销条目。
/// </summary>
public sealed class DocumentHistory
{
    private readonly IPrimitiveService _service;
    private readonly Stack<DocumentSnapshot> _undoStack = new();
    private readonly Stack<DocumentSnapshot> _redoStack = new();

    public DocumentHistory(IPrimitiveService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    /// <summary>是否可撤销。</summary>
    public bool CanUndo => _undoStack.Count > 0;

    /// <summary>是否可重做。</summary>
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>捕获当前文档状态以供撤销。在修改前调用。</summary>
    public void Capture()
    {
        _undoStack.Push(DocumentSnapshot.Capture(_service.GetAll().ToList()));
        _redoStack.Clear();
    }

    /// <summary>撤销最近的捕获。</summary>
    public void Undo()
    {
        if (_undoStack.Count == 0)
        {
            return;
        }

        var current = DocumentSnapshot.Capture(_service.GetAll().ToList());
        var target = _undoStack.Pop();
        _redoStack.Push(current);
        target.Apply(_service);
    }

    /// <summary>重做最近撤销的捕获。</summary>
    public void Redo()
    {
        if (_redoStack.Count == 0)
        {
            return;
        }

        var current = DocumentSnapshot.Capture(_service.GetAll().ToList());
        var target = _redoStack.Pop();
        _undoStack.Push(current);
        target.Apply(_service);
    }

    /// <summary>清空两个栈（文档被外部替换时）。</summary>
    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
    }
}

/// <summary>图元树的某一时刻快照。</summary>
public sealed class DocumentSnapshot
{
    private readonly List<Primitive> _roots;
    private readonly Dictionary<Guid, Dictionary<string, object>> _props;
    private readonly Dictionary<Guid, List<Primitive>> _children;

    private DocumentSnapshot(
        List<Primitive> roots,
        Dictionary<Guid, Dictionary<string, object>> props,
        Dictionary<Guid, List<Primitive>> children)
    {
        _roots = roots;
        _props = props;
        _children = children;
    }

    public static DocumentSnapshot Capture(IReadOnlyList<Primitive> roots)
    {
        var props = new Dictionary<Guid, Dictionary<string, object>>();
        var children = new Dictionary<Guid, List<Primitive>>();
        foreach (var root in roots)
        {
            CaptureNode(root, props, children);
        }

        return new DocumentSnapshot(roots.ToList(), props, children);
    }

    private static void CaptureNode(
        Primitive primitive,
        Dictionary<Guid, Dictionary<string, object>> props,
        Dictionary<Guid, List<Primitive>> children)
    {
        var values = new Dictionary<string, object>();
        foreach (var prop in primitive.Props)
        {
            if (prop.GetBoxedValue() is { } value)
            {
                values[prop.Name] = value;
            }
        }

        props[primitive.Id] = values;

        if (primitive is Container container)
        {
            children[primitive.Id] = container.Children.ToList();
            foreach (var child in container.Children)
            {
                CaptureNode(child, props, children);
            }
        }
    }

    /// <summary>将此快照恢复到服务上（根列表和属性值）。</summary>
    public void Apply(IPrimitiveService service)
    {
        // 从捕获的结构（_children）重建快照的节点集，而非活跃树：
        // container.Children 在 Apply 运行时已偏离快照（本次撤销回退的修改已发生），
        // 遍历活跃子元素会错误地包含快照不拥有的节点。
        var snapshotNodes = new List<Primitive>();
        var snapshotSet = new HashSet<Primitive>();
        foreach (var root in _roots)
        {
            CollectSnapshotNodes(root, snapshotNodes, snapshotSet);
        }

        // 恢复容器子元素（结构可能已变更）
        foreach (var (containerId, savedChildren) in _children)
        {
            var container = snapshotNodes.FirstOrDefault(p => p is Container c && c.Id == containerId) as Container;
            if (container is null)
            {
                continue;
            }

            foreach (var existing in container.Children.ToList())
            {
                container.RemoveChild(existing);
            }

            foreach (var child in savedChildren)
            {
                container.AddChild(child);
            }
        }

        // 保留活跃选区跨恢复：选区是 UI 状态，不在快照中，ReplaceAll 会清空它，
        // 因此先记住，恢复后仅重新应用仍存活的项。
        var liveSelection = service.GetSelection().ToList();

        service.ReplaceAll(_roots, clearHistory: false);

        // 恢复属性值
        foreach (var primitive in snapshotNodes)
        {
            if (!_props.TryGetValue(primitive.Id, out var values))
            {
                continue;
            }

            foreach (var prop in primitive.Props)
            {
                if (!values.TryGetValue(prop.Name, out var value))
                {
                    continue;
                }

                prop.SetBoxedValue(value);
            }
        }

        // 重新应用活跃选区，仅保留本次恢复后仍存活的图元（被撤销修改新增的图元会丢弃）
        service.SetSelectedRange(liveSelection.Where(snapshotSet.Contains));
    }

    private void CollectSnapshotNodes(Primitive root, List<Primitive> nodes, HashSet<Primitive> set)
    {
        nodes.Add(root);
        set.Add(root);
        if (root is Container container && _children.TryGetValue(container.Id, out var savedChildren))
        {
            foreach (var child in savedChildren)
            {
                CollectSnapshotNodes(child, nodes, set);
            }
        }
    }
}
