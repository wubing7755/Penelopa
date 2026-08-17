using Penelopa.Core.Alignment;
using Penelopa.Core.Primitives;
using Xunit;

namespace Penelopa.Core.Tests;

public class ContainerTests
{
    private static Container OffsetContainerWithChild()
    {
        var container = Container.CreateOffset("c", 20f, 20f);
        container.AddChild(new Rectangle { PosX = { Value = 0f }, PosY = { Value = 0f }, Width = { Value = 10f }, Height = { Value = 10f } });
        return container;
    }

    [Fact]
    public void WorldBoundingBox_AppliesContainerOffset()
    {
        var container = OffsetContainerWithChild();

        Assert.Equal(new Box(20f, 20f, 30f, 30f), container.GetWorldBoundingBox());
    }

    [Fact]
    public void ChildWorldBoundingBox_MatchesContainerContentBox()
    {
        var container = OffsetContainerWithChild();
        var child = container.Children[0];

        // The child's world box equals the container's content extent.
        Assert.Equal(container.GetWorldBoundingBox(), child.GetWorldBoundingBox());
    }

    [Fact]
    public void RotationContainer_WorldBoundingBox_IsRotatedAabb()
    {
        var container = Container.CreateRotation("r", 90f);
        container.AddChild(new Rectangle { PosX = { Value = 0f }, PosY = { Value = 0f }, Width = { Value = 10f }, Height = { Value = 10f } });

        var actual = container.GetWorldBoundingBox();
        Assert.Equal(-10f, actual.MinX, 3);
        Assert.Equal(0f, actual.MinY, 3);
        Assert.Equal(0f, actual.MaxX, 3);
        Assert.Equal(10f, actual.MaxY, 3);
    }

    [Fact]
    public void Translate_Container_MovesChildrenInWorld()
    {
        var container = OffsetContainerWithChild();

        container.Translate(10f, 0f);

        Assert.Equal(new Box(30f, 20f, 40f, 30f), container.GetWorldBoundingBox());
    }

    [Fact]
    public void Translate_ChildInsideRotatedContainer_FollowsLocalAxis()
    {
        var container = Container.CreateRotation("r", 90f);
        var child = new Rectangle { PosX = { Value = 0f }, PosY = { Value = 0f }, Width = { Value = 10f }, Height = { Value = 10f } };
        container.AddChild(child);

        // World +Y (up) maps to the container's local +X axis after a 90° rotation.
        // Child local box becomes (10,0,20,10); rotated: (-10,10,0,20).
        child.Translate(0f, 10f);

        var actual = child.GetWorldBoundingBox();
        Assert.Equal(-10f, actual.MinX, 3);
        Assert.Equal(10f, actual.MinY, 3);
        Assert.Equal(0f, actual.MaxX, 3);
        Assert.Equal(20f, actual.MaxY, 3);
    }

    [Fact]
    public void SetBounds_AxisAlignedContainer_FillsTargetAndKeepsAnchor()
    {
        var container = OffsetContainerWithChild();

        // Anchor = world TopLeft of the current box (20, 30).
        var result = container.SetBounds(new Box(20f, 20f, 50f, 30f), new Point(20f, 30f));

        Assert.Equal(new Box(20f, 20f, 50f, 30f), result);
    }

    [Fact]
    public void ContainsWorldPoint_RecursesIntoChildren()
    {
        var container = OffsetContainerWithChild();

        Assert.True(container.ContainsWorldPoint(new Point(25f, 25f)));  // inside the child
        Assert.False(container.ContainsWorldPoint(new Point(50f, 50f))); // outside everything
    }

    [Fact]
    public void NestedContainers_ComposeTransforms()
    {
        var outer = Container.CreateRotation("outer", 90f);
        var inner = Container.CreateOffset("inner", 10f, 0f);
        inner.AddChild(new Rectangle { PosX = { Value = 0f }, PosY = { Value = 0f }, Width = { Value = 5f }, Height = { Value = 5f } });
        outer.AddChild(inner);

        // Inner local box (0,0,5,5) → offset (10,0,15,5) → rotate 90° → (-5,10,0,15).
        var world = inner.GetWorldBoundingBox();
        Assert.Equal(-5f, world.MinX, 3);
        Assert.Equal(10f, world.MinY, 3);
        Assert.Equal(0f, world.MaxX, 3);
        Assert.Equal(15f, world.MaxY, 3);
    }

    [Fact]
    public void RemoveChild_DetachesParent()
    {
        var container = OffsetContainerWithChild();
        var child = container.Children[0];

        container.RemoveChild(child);

        Assert.Null(child.Parent);
        Assert.Empty(container.Children);
    }

    [Fact]
    public void TransformProperties_DriveLocalTransform()
    {
        var container = OffsetContainerWithChild();

        container.OffsetX.Value = 50f;

        Assert.Equal(new Box(50f, 20f, 60f, 30f), container.GetWorldBoundingBox());
    }

    [Fact]
    public void RotationProperty_ChangesContainer()
    {
        var container = Container.CreateRotation("r", 0f);
        container.AddChild(new Rectangle { PosX = { Value = 0f }, PosY = { Value = 0f }, Width = { Value = 10f }, Height = { Value = 10f } });

        container.Rotation.Value = 90f;

        var actual = container.GetWorldBoundingBox();
        Assert.Equal(-10f, actual.MinX, 3);
        Assert.Equal(0f, actual.MinY, 3);
        Assert.Equal(0f, actual.MaxX, 3);
        Assert.Equal(10f, actual.MaxY, 3);
    }

    [Fact]
    public void Container_ExposesOnlyKindRelevantTransformProperties()
    {
        var offset = Container.CreateOffset("o", 1f, 2f);
        Assert.Contains(offset.OffsetX, offset.Props);
        Assert.Contains(offset.OffsetY, offset.Props);
        Assert.DoesNotContain(offset.Rotation, offset.Props);
        Assert.DoesNotContain(offset.ScaleX, offset.Props);

        var rotation = Container.CreateRotation("r", 45f);
        Assert.Contains(rotation.Rotation, rotation.Props);
        Assert.DoesNotContain(rotation.OffsetX, rotation.Props);
        Assert.DoesNotContain(rotation.ScaleX, rotation.Props);

        var flip = Container.CreateFlip("f", flipX: true, flipY: false);
        Assert.Contains(flip.ScaleX, flip.Props);
        Assert.Contains(flip.ScaleY, flip.Props);
        Assert.DoesNotContain(flip.OffsetX, flip.Props);
        Assert.DoesNotContain(flip.Rotation, flip.Props);
    }
}
