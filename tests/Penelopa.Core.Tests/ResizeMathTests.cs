using Penelopa.Core.Alignment;
using Penelopa.Core.Interaction;
using Xunit;

namespace Penelopa.Core.Tests;

public class SelectionBoxTests
{
    private static readonly Box Bounds = new(10f, 20f, 50f, 80f);

    [Fact]
    public void HandlePoint_ReturnsTheFourWorldCorners()
    {
        // World Y grows up: Top corners use MaxY, Bottom corners use MinY.
        Assert.Equal(new Point(10f, 80f), SelectionBox.HandlePoint(Bounds, ResizeHandle.TopLeft));
        Assert.Equal(new Point(50f, 80f), SelectionBox.HandlePoint(Bounds, ResizeHandle.TopRight));
        Assert.Equal(new Point(10f, 20f), SelectionBox.HandlePoint(Bounds, ResizeHandle.BottomLeft));
        Assert.Equal(new Point(50f, 20f), SelectionBox.HandlePoint(Bounds, ResizeHandle.BottomRight));
    }
}

public class ResizeMathTests
{
    // Box(10,10,30,30): fixed corners are the diagonal opposites.
    private static readonly Box Bounds = new(10f, 10f, 30f, 30f);

    [Fact]
    public void ComputeBounds_BottomRightDrag_GrowsFromFixedTopLeft()
    {
        // BottomRight's fixed corner is (MinX, MaxY) = (10, 30).
        var result = ResizeMath.ComputeBounds(Bounds, ResizeHandle.BottomRight, new Point(50f, 0f));

        Assert.Equal(new Box(10f, 0f, 50f, 30f), result);
    }

    [Fact]
    public void ComputeBounds_TopLeftDrag_ShrinksTowardFixedBottomRight()
    {
        // TopLeft's fixed corner is (MaxX, MinY) = (30, 10).
        var result = ResizeMath.ComputeBounds(Bounds, ResizeHandle.TopLeft, new Point(20f, 20f));

        Assert.Equal(new Box(20f, 10f, 30f, 20f), result);
    }

    [Fact]
    public void ComputeBounds_CrossingFixedCorner_FlipsTheBox()
    {
        // Drag BottomRight far past the fixed top-left corner: the box flips.
        var result = ResizeMath.ComputeBounds(Bounds, ResizeHandle.BottomRight, new Point(0f, 40f));

        Assert.Equal(new Box(0f, 30f, 10f, 40f), result);
    }

    [Fact]
    public void ComputeBounds_ClampsToMinimumSize_AnchoredOnFixedCorner()
    {
        // Pointer very close to the fixed corner (10, 30): width clamps to 1,
        // height stays at its current extent.
        var result = ResizeMath.ComputeBounds(Bounds, ResizeHandle.BottomRight, new Point(10.2f, 10.1f));

        Assert.Equal(new Box(10f, 10.1f, 11f, 30f), result);
    }

    [Fact]
    public void ComputeBounds_LeftEdgeClamp_KeepsFixedCorner()
    {
        // Pointer near the fixed corner (30, 10) from the other side: both
        // axes clamp to the minimum, fixed corner stays put.
        var result = ResizeMath.ComputeBounds(Bounds, ResizeHandle.TopLeft, new Point(29.8f, 10.1f));

        Assert.Equal(new Box(29f, 10f, 30f, 11f), result);
    }
}
