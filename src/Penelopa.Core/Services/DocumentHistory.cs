using Penelopa.Core.Primitives;

namespace Penelopa.Core.Services;

/// <summary>
/// Undo/redo history for the document. A snapshot captures the primitive
/// tree by reference plus every primitive's property values and container
/// children; applying a snapshot restores those values and rebuilds the root
/// list through <see cref="IPrimitiveService.ReplaceAll"/>. Selection is UI
/// state, not document state: it is never captured, and undo/redo preserves
/// the live selection filtered to the primitives that survive the restore.
/// Snapshots are taken only BEFORE a real mutation (a structural change or
/// the start of a drag/resize gesture), so non-mutating clicks never create
/// an undo entry.
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
        _undoStack.Push(DocumentSnapshot.Capture(_service.GetAll().ToList()));
        _redoStack.Clear();
    }

    /// <summary>Undoes the most recent capture.</summary>
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

    /// <summary>Redoes the most recently undone capture.</summary>
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

    /// <summary>Restores this snapshot onto the service (roots and property values).</summary>
    public void Apply(IPrimitiveService service)
    {
        // Reconstruct this snapshot's node set from the CAPTURED structure
        // (_children), not the live tree: container.Children has already
        // diverged from the snapshot by the time Apply runs (the mutation this
        // undo is rolling back already happened), so walking the live children
        // would wrongly include nodes the snapshot does not own.
        var snapshotNodes = new List<Primitive>();
        var snapshotSet = new HashSet<Primitive>();
        foreach (var root in _roots)
        {
            CollectSnapshotNodes(root, snapshotNodes, snapshotSet);
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

        // Preserve the live selection across the restore: selection is UI
        // state and is not part of the snapshot, and ReplaceAll clears it, so
        // remember it first and re-apply only the survivors afterwards.
        var liveSelection = service.GetSelection().ToList();

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
                if (!values.TryGetValue(prop.Name, out var value))
                {
                    continue;
                }

                prop.SetBoxedValue(value);
            }
        }

        // Re-apply the live selection, keeping only primitives that survive
        // this restore (primitives added by the undone mutation drop out).
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
