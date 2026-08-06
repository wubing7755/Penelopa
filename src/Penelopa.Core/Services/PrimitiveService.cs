using Penelopa.Core.Primitives;

namespace Penelopa.Core.Services;

/// <summary>
/// Default <see cref="IPrimitiveService"/> implementation backed by lists.
/// </summary>
public class PrimitiveService : IPrimitiveService
{
    private readonly List<Primitive> _primitives = new();
    private readonly HashSet<Primitive> _selection = new();

    public void Add(Primitive primitive)
    {
        _primitives.Add(primitive);
        OnChange?.Invoke(primitive);
    }

    public IEnumerable<Primitive> GetAll()
    {
        return _primitives;
    }

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

    public event Action<Primitive>? OnChange;
    public event Action<IEnumerable<Primitive>>? OnSelectionChanged;
}
