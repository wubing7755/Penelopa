using Penelopa.Core.Alignment;
using Penelopa.Core.Primitives;
using Xunit;

namespace Penelopa.Core.Tests;

public class AlignExtensionsTests
{
    private static Rectangle Rect(float x, float y, float w = 10f, float h = 10f)
        => new Rectangle { PosX = { Value = x }, PosY = { Value = y }, Width = { Value = w }, Height = { Value = h } };

    [Fact]
    public void Align_WithSingleItem_ReturnsFalse()
    {
        var items = new List<Primitive> { Rect(0f, 0f) };

        var result = items.Align(AlignType.Left);

        Assert.False(result);
    }

    [Fact]
    public void Align_WithEmptyItems_ReturnsFalse()
    {
        var items = new List<Primitive>();

        var result = items.Align(AlignType.Left);

        Assert.False(result);
    }

    [Fact]
    public void Align_AlreadyAligned_ReturnsFalseAndDoesNotMove()
    {
        var items = new List<Primitive>
        {
            Rect(0f, 0f),
            Rect(0f, 20f),
        };

        var result = items.Align(AlignType.Left);

        Assert.False(result);
        Assert.Equal(0f, ((Rectangle)items[0]).PosX.Value);
        Assert.Equal(0f, ((Rectangle)items[1]).PosX.Value);
    }

    [Fact]
    public void Align_Left_MovesMinXToUnionMin()
    {
        var items = new List<Primitive>
        {
            Rect(0f, 0f),
            Rect(30f, 0f),
        };

        var result = items.Align(AlignType.Left);

        Assert.True(result);
        Assert.Equal(0f, ((Rectangle)items[0]).PosX.Value);
        Assert.Equal(0f, ((Rectangle)items[1]).PosX.Value);
    }

    [Fact]
    public void Align_Right_MovesMaxXToUnionMax()
    {
        var items = new List<Primitive>
        {
            Rect(0f, 0f),
            Rect(30f, 0f),
        };

        var result = items.Align(AlignType.Right);

        Assert.True(result);
        Assert.Equal(30f, ((Rectangle)items[0]).PosX.Value);
        Assert.Equal(30f, ((Rectangle)items[1]).PosX.Value);
    }

    [Fact]
    public void Align_HCenter_AlignsCenterXToUnionCenter()
    {
        var items = new List<Primitive>
        {
            Rect(0f, 0f),    // center 5
            Rect(40f, 0f),   // center 45 → union center 25
        };

        var result = items.Align(AlignType.HCenter);

        Assert.True(result);
        Assert.Equal(20f, ((Rectangle)items[0]).PosX.Value);   // 5 -> 25 => +20
        Assert.Equal(20f, ((Rectangle)items[1]).PosX.Value);   // 45 -> 25 => -20
        Assert.Equal(25f, ((Rectangle)items[0]).GetWorldBoundingBox().CenterX);
        Assert.Equal(25f, ((Rectangle)items[1]).GetWorldBoundingBox().CenterX);
    }

    [Fact]
    public void Align_Top_MovesMaxYToUnionMax()
    {
        var items = new List<Primitive>
        {
            Rect(0f, 0f),
            Rect(0f, 30f),
        };

        var result = items.Align(AlignType.Top);

        Assert.True(result);
        Assert.Equal(30f, ((Rectangle)items[0]).PosY.Value);
        Assert.Equal(30f, ((Rectangle)items[1]).PosY.Value);
    }

    [Fact]
    public void Align_Bottom_MovesMinYToUnionMin()
    {
        var items = new List<Primitive>
        {
            Rect(0f, 0f),
            Rect(0f, 30f),
        };

        var result = items.Align(AlignType.Bottom);

        Assert.True(result);
        Assert.Equal(0f, ((Rectangle)items[0]).PosY.Value);
        Assert.Equal(0f, ((Rectangle)items[1]).PosY.Value);
    }

    [Fact]
    public void Align_VCenter_AlignsCenterYToUnionCenter()
    {
        var items = new List<Primitive>
        {
            Rect(0f, 0f),    // center 5
            Rect(0f, 40f),   // center 45 → union center 25
        };

        var result = items.Align(AlignType.VCenter);

        Assert.True(result);
        Assert.Equal(20f, ((Rectangle)items[0]).PosY.Value);
        Assert.Equal(20f, ((Rectangle)items[1]).PosY.Value);
        Assert.Equal(25f, ((Rectangle)items[0]).GetWorldBoundingBox().CenterY);
        Assert.Equal(25f, ((Rectangle)items[1]).GetWorldBoundingBox().CenterY);
    }

    [Fact]
    public void Align_MixedPrimitives_UsesWorldBoundingBoxes()
    {
        // Circle at (10,10) r=5 → box (5,5,15,15). Rectangle at (30,0,10,10) → box (30,0,40,10).
        var circle = new Circle { CenterX = { Value = 10f }, CenterY = { Value = 10f }, Radius = { Value = 5f } };
        var rect = Rect(30f, 0f);
        var items = new List<Primitive> { circle, rect };

        var result = items.Align(AlignType.Left);

        Assert.True(result);
        Assert.Equal(5f, circle.GetWorldBoundingBox().MinX);
        Assert.Equal(5f, rect.GetWorldBoundingBox().MinX);
    }
}
