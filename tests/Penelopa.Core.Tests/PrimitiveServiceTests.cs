using Penelopa.Core.Primitives;
using Penelopa.Core.Services;
using Xunit;

namespace Penelopa.Core.Tests;

public class PrimitiveServiceTests
{
    [Fact]
    public void Add_StoresPrimitive()
    {
        var service = new PrimitiveService();
        var rect = new Rectangle();

        service.Add(rect);

        Assert.Contains(rect, service.GetAll());
    }

    [Fact]
    public void Add_RaisesOnChange()
    {
        var service = new PrimitiveService();
        var rect = new Rectangle();
        Primitive? raised = null;
        service.OnChange += p => raised = p;

        service.Add(rect);

        Assert.Same(rect, raised);
    }

    [Fact]
    public void SetSelected_ReplacesSelection()
    {
        var service = new PrimitiveService();
        var a = new Rectangle();
        var b = new Circle();
        service.Add(a);
        service.Add(b);

        service.SetSelected(a);
        service.SetSelected(b);

        Assert.Single(service.GetSelection());
        Assert.Contains(b, service.GetSelection());
    }

    [Fact]
    public void SetSelectedRange_ReplacesSelectionWithRange()
    {
        var service = new PrimitiveService();
        var a = new Rectangle();
        var b = new Circle();
        var c = new Triangle();

        service.SetSelected(a);
        service.SetSelectedRange(new Primitive[] { b, c });

        Assert.Equal(2, service.GetSelection().Count());
        Assert.DoesNotContain(a, service.GetSelection());
    }

    [Fact]
    public void AppendSelected_AddsToSelection()
    {
        var service = new PrimitiveService();
        var a = new Rectangle();
        var b = new Circle();

        service.SetSelected(a);
        service.AppendSelected(b);

        Assert.Equal(2, service.GetSelection().Count());
    }

    [Fact]
    public void ClearSelection_EmptiesSelection()
    {
        var service = new PrimitiveService();
        var a = new Rectangle();
        service.SetSelected(a);

        service.ClearSelection();

        Assert.Empty(service.GetSelection());
    }

    [Fact]
    public void SelectionChanges_RaiseOnSelectionChanged()
    {
        var service = new PrimitiveService();
        var rect = new Rectangle();
        var events = new List<IEnumerable<Primitive>>();
        service.OnSelectionChanged += s => events.Add(s.ToList());

        service.SetSelected(rect);

        Assert.Single(events);
        Assert.Contains(rect, events[0]);
    }
}
