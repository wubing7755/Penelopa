using Penelopa.Core.Primitives;

namespace Penelopa.Core.Services;

/// <summary>
/// Undo/redo history for the document. A snapshot captures the primitive
/// tree by reference plus every primitive's property values, container
/// children, and the selection; applying a snapshot restores those values
/// and rebuilds the root list through <see cref="IPrimitiveService.ReplaceAll"/>.
/// Snapshots are taken BEFORE a mutation (gesture start, structural change),
/// so undo restores the pre-gesture state.
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

    /// <summary>Gets whether an undo is available.</summary>
    public bool CanUndo => _undoStack.Count > 0;

    /// <summary>Gets whether a redo is available.</summary>
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>
    /// Captures the current document state for undo. Call before a mutation.
    /// </summary>
    public void Capture()
    {
        _undoStack.Push(DocumentSnapshot.Capture(_service.GetAll().ToList(), _service.GetSelection().ToList()));
        _redoStack.Clear();
    }

    /// <summary>Undoes the most recent capture.</summary>
    public void Undo()
    {
        if (_undoStack.Count == 0)
        {
            return;
        }

        var current = DocumentSnapshot.Capture(_service.GetAll().ToList(), _service.GetSelection().ToList());
        var target = _undoStack.Pop();
        _redoStack.Push(current);
        target.Apply(_service);
    }

    /// <summary>Redoes the most recently undone capture.</summary>
    public void Redo()
    {
        if (_redoStack.Count == 0)
        {
            return;
        }

        var current = DocumentSnapshot.Capture(_service.GetAll().ToList(), _service.GetSelection().ToList());
        var target = _redoStack.Pop();
        _undoStack.Push(current);
        target.Apply(_service);
    }

    /// <summary>Clears both stacks (document replaced externally).</summary>
    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
    }
}

/// <summary>A point-in-time capture of the primitive tree.</summary>
public sealed class DocumentSnapshot
{
    private readonly List<Primitive> _roots;
    private readonly Dictionary<Guid, Dictionary<string, object>> _props;
    private readonly Dictionary<Guid, List<Primitive>> _children;
    private readonly List<Primitive> _selection;

    private DocumentSnapshot(
        List<Primitive> roots,
        Dictionary<Guid, Dictionary<string, object>> props,
        Dictionary<Guid, List<Primitive>> children,
        List<Primitive> selection)
    {
        _roots = roots;
        _props = props;
        _children = children;
        _selection = selection;
    }

    public static DocumentSnapshot Capture(IReadOnlyList<Primitive> roots, IReadOnlyList<Primitive> selection)
    {
        var props = new Dictionary<Guid, Dictionary<string, object>>();
        var children = new Dictionary<Guid, List<Primitive>>();
        foreach (var root in roots)
        {
            CaptureNode(root, props, children);
        }

        return new DocumentSnapshot(roots.ToList(), props, children, selection.ToList());
    }

    private static void CaptureNode(
        Primitive primitive,
        Dictionary<Guid, Dictionary<string, object>> props,
        Dictionary<Guid, List<Primitive>> children)
    {
        var values = new Dictionary<string, object>();
        foreach (var prop in primitive.Props)
        {
            if (ReferenceEquals(prop, primitive.ColorKey))
            {
                continue;
            }

            switch (prop)
            {
                case FloatPropValue fp: values[prop.Name] = fp.Value; break;
                case DoublePropValue dp: values[prop.Name] = dp.Value; break;
                case IntPropValue ip: values[prop.Name] = ip.Value; break;
                case BoolPropValue bp: values[prop.Name] = bp.Value; break;
                case StringPropValue sp: values[prop.Name] = sp.Value; break;
                case UintPropValue up: values[prop.Name] = up.Value; break;
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

    /// <summary>Restores this snapshot onto the service (roots, values, keys, selection).</summary>
    public void Apply(IPrimitiveService service)
    {
        // Free keys of primitives that are in the live tree but not in this
        // snapshot (undoing an Add drops those objects for good).
        var snapshotNodes = new List<Primitive>();
        var snapshotSet = new HashSet<Primitive>();
        foreach (var root in _roots)
        {
            CollectNodes(root, snapshotNodes, snapshotSet);
        }

        foreach (var live in service.GetAll().ToList())
        {
            if (!snapshotSet.Contains(live))
            {
                ReleaseKeys(live);
            }
        }

        // Restore container children (structure may have changed).
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

        service.ReplaceAll(_roots, clearHistory: false);

        // Restore property values.
        foreach (var primitive in snapshotNodes)
        {
            if (!_props.TryGetValue(primitive.Id, out var values))
            {
                continue;
            }

            foreach (var prop in primitive.Props)
            {
                if (ReferenceEquals(prop, primitive.ColorKey))
                {
                    continue;
                }

                if (!values.TryGetValue(prop.Name, out var value))
                {
                    continue;
                }

                switch (prop)
                {
                    case FloatPropValue fp: fp.Value = Convert.ToSingle(value); break;
                    case DoublePropValue dp: dp.Value = Convert.ToDouble(value); break;
                    case IntPropValue ip: ip.Value = Convert.ToInt32(value); break;
                    case BoolPropValue bp: bp.Value = Convert.ToBoolean(value); break;
                    case StringPropValue sp: sp.Value = Convert.ToString(value) ?? string.Empty; break;
                    case UintPropValue up: up.Value = Convert.ToUInt32(value); break;
                }
            }
        }

        // Re-register color keys (ReplaceAll released the live tree's keys;
        // restored primitives need fresh ones).
        foreach (var primitive in snapshotNodes)
        {
            primitive.ColorKey.Value = ColorKeyManager.GenerateColorKey(primitive);
        }

        // Restore the selection by reference (the objects are the same).
        service.SetSelectedRange(_selection.Where(snapshotSet.Contains));
    }

    private static void CollectNodes(Primitive root, List<Primitive> nodes, HashSet<Primitive> set)
    {
        nodes.Add(root);
        set.Add(root);
        if (root is Container container)
        {
            foreach (var child in container.Children)
            {
                CollectNodes(child, nodes, set);
            }
        }
    }

    private static void ReleaseKeys(Primitive root)
    {
        if (root is Container container)
        {
            foreach (var child in container.Children)
            {
                ReleaseKeys(child);
            }
        }

        ColorKeyManager.ReleaseColorKey(root.ColorKey.Value);
    }
}
