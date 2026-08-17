using Penelopa.Core.Alignment;
using Penelopa.Core.Interaction;
using Penelopa.Core.Primitives;
using Xunit;

namespace Penelopa.Core.Tests;

public class SetBoundsTests
{
    [Fact]
    public void Rectangle_FillsTargetBoundsExactly()
    {
        var rect = new Rectangle { PosX = { Value = 1f }, PosY = { Value = 2f }, Width = { Value = 3f }, Height = { Value = 4f } };
        var target = new Box(5f, 5f, 25f, 15f);

        var actual = rect.SetBounds(target, new Point(0f, 0f));

        Assert.Equal(5f, rect.PosX.Value);
        Assert.Equal(5f, rect.PosY.Value);
        Assert.Equal(20f, rect.Width.Value);
        Assert.Equal(10f, rect.Height.Value);
        Assert.Equal(target, actual);
    }

    [Fact]
    public void Circle_FixedCornerStaysOnTheCircle()
    {
        // Original circle: center (10,10), r 5 → bbox (5,5,15,15).
        var circle = new Circle { CenterX = { Value = 10f }, CenterY = { Value = 10f }, Radius = { Value = 5f } };
        var original = circle.GetWorldBoundingBox();
        var anchor = ResizeMath.FixedCorner(original, ResizeHandle.BottomRight); // (MinX, MaxY) = (5,15)

        var actual = circle.SetBounds(new Box(5f, 5f, 35f, 15f), anchor);

        // Target w=30, h=10 → inscribed radius 5; the anchor (5,15) stays on
        // the circle, so the circle keeps its original extent: only the fixed
        // corner is preserved, the shape does not stretch.
        Assert.Equal(5f, circle.Radius.Value);
        Assert.Equal(10f, circle.CenterX.Value);
        Assert.Equal(10f, circle.CenterY.Value);
        Assert.Equal(new Box(5f, 5f, 15f, 15f), actual);
    }

    [Fact]
    public void Circle_GrowsFromFixedCornerWhenBothAxesMove()
    {
        var circle = new Circle { CenterX = { Value = 10f }, CenterY = { Value = 10f }, Radius = { Value = 5f } };
        var original = circle.GetWorldBoundingBox();
        var anchor = ResizeMath.FixedCorner(original, ResizeHandle.BottomRight); // (5,15)

        // Target (5,-5,35,15): w=30, h=20 → radius 10.
        var actual = circle.SetBounds(new Box(5f, -5f, 35f, 15f), anchor);

        Assert.Equal(10f, circle.Radius.Value);
        Assert.Equal(new Box(5f, -5f, 25f, 15f), actual); // fixed corner (5,15) preserved
    }

    [Fact]
    public void Circle_ResizeBackToOriginalBounds_RestoresExactly()
    {
        var circle = new Circle { CenterX = { Value = 10f }, CenterY = { Value = 10f }, Radius = { Value = 5f } };
        var original = circle.GetWorldBoundingBox();
        var anchor = ResizeMath.FixedCorner(original, ResizeHandle.BottomRight);
        circle.SetBounds(new Box(0f, 0f, 40f, 40f), anchor);

        circle.SetBounds(original, anchor);

        Assert.Equal(10f, circle.CenterX.Value);
        Assert.Equal(10f, circle.CenterY.Value);
        Assert.Equal(5f, circle.Radius.Value);
    }

    [Fact]
    public void Triangle_FillsTargetBoundsAndPreservesShape()
    {
        // Triangle with vertices at (10,10), (30,10), (20,20): bbox (10,10,30,20).
        var triangle = new Triangle();
        triangle.Vertex1X.Value = 10f; triangle.Vertex1Y.Value = 10f;
        triangle.Vertex2X.Value = 30f; triangle.Vertex2Y.Value = 10f;
        triangle.Vertex3X.Value = 20f; triangle.Vertex3Y.Value = 20f;
        var target = new Box(0f, 0f, 40f, 20f); // double width

        var actual = triangle.SetBounds(target, new Point(0f, 0f));

        Assert.Equal(target, actual);
        // Normalized shape preserved: left edge at x=0, right edge at x=40,
        // apex at horizontal center (x=20) and full height (y=20).
        Assert.Equal(0f, triangle.Vertex1X.Value);
        Assert.Equal(0f, triangle.Vertex1Y.Value);
        Assert.Equal(40f, triangle.Vertex2X.Value);
        Assert.Equal(0f, triangle.Vertex2Y.Value);
        Assert.Equal(20f, triangle.Vertex3X.Value);
        Assert.Equal(20f, triangle.Vertex3Y.Value);
    }

    [Fact]
    public void Triangle_ResizeBackToOriginalBounds_RestoresExactly()
    {
        var triangle = new Triangle();
        triangle.Vertex1X.Value = 10f; triangle.Vertex1Y.Value = 10f;
        triangle.Vertex2X.Value = 30f; triangle.Vertex2Y.Value = 10f;
        triangle.Vertex3X.Value = 20f; triangle.Vertex3Y.Value = 20f;
        var original = triangle.GetWorldBoundingBox();
        triangle.SetBounds(new Box(0f, 0f, 100f, 100f), new Point(0f, 0f));

        triangle.SetBounds(original, new Point(0f, 0f));

        Assert.Equal(10f, triangle.Vertex1X.Value);
        Assert.Equal(10f, triangle.Vertex1Y.Value);
        Assert.Equal(30f, triangle.Vertex2X.Value);
        Assert.Equal(10f, triangle.Vertex2Y.Value);
        Assert.Equal(20f, triangle.Vertex3X.Value);
        Assert.Equal(20f, triangle.Vertex3Y.Value);
    }
}

public class ContainsWorldPointTests
{
    [Fact]
    public void Circle_ContainsCenterAndInside()
    {
        var circle = new Circle { CenterX = { Value = 10f }, CenterY = { Value = 10f }, Radius = { Value = 5f } };

        Assert.True(circle.ContainsWorldPoint(new Point(10f, 10f)));
        Assert.True(circle.ContainsWorldPoint(new Point(13f, 14f))); // distance 5
        Assert.False(circle.ContainsWorldPoint(new Point(16f, 10f))); // distance 6
    }

    [Fact]
    public void Rectangle_ContainsInsideAndBoundary()
    {
        var rect = new Rectangle { PosX = { Value = 0f }, PosY = { Value = 0f }, Width = { Value = 10f }, Height = { Value = 10f } };

        Assert.True(rect.ContainsWorldPoint(new Point(5f, 5f)));
        Assert.True(rect.ContainsWorldPoint(new Point(0f, 10f))); // boundary
        Assert.False(rect.ContainsWorldPoint(new Point(-1f, 5f)));
        Assert.False(rect.ContainsWorldPoint(new Point(11f, 5f)));
    }

    [Fact]
    public void Triangle_ContainsInsideAndRejectsOutside()
    {
        var triangle = new Triangle();
        triangle.Vertex1X.Value = 0f; triangle.Vertex1Y.Value = 0f;
        triangle.Vertex2X.Value = 10f; triangle.Vertex2Y.Value = 0f;
        triangle.Vertex3X.Value = 5f; triangle.Vertex3Y.Value = 10f;

        Assert.True(triangle.ContainsWorldPoint(new Point(5f, 2f)));
        Assert.False(triangle.ContainsWorldPoint(new Point(0f, 5f)));   // left of the slanted edge
        Assert.False(triangle.ContainsWorldPoint(new Point(11f, 0f)));  // beyond the right base corner
        Assert.False(triangle.ContainsWorldPoint(new Point(5f, 11f)));  // above the apex
    }

    [Fact]
    public void Triangle_Degenerate_IsNotHittable()
    {
        var triangle = new Triangle();
        triangle.Vertex1X.Value = 0f; triangle.Vertex1Y.Value = 0f;
        triangle.Vertex2X.Value = 5f; triangle.Vertex2Y.Value = 0f;
        triangle.Vertex3X.Value = 10f; triangle.Vertex3Y.Value = 0f; // collinear

        Assert.False(triangle.ContainsWorldPoint(new Point(5f, 0f)));
    }
}
