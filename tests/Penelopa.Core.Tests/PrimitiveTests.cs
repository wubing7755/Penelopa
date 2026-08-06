using Penelopa.Core.Alignment;
using Penelopa.Core.Primitives;
using Xunit;

namespace Penelopa.Core.Tests;

public class PrimitiveTests
{
    [Fact]
    public void Primitive_Constructor_RegistersNameColorKeyAndProps()
    {
        var rect = new Rectangle();

        Assert.NotEqual(Guid.Empty, rect.Id);
        Assert.Equal("Rectangle", rect.Name.Value);
        Assert.NotNull(rect.ColorKey);
        Assert.Contains(rect.Props, p => p == rect.Name);
        Assert.Contains(rect.Props, p => p == rect.ColorKey);
        Assert.Contains(rect.Props, p => p == rect.PosX);
        Assert.Contains(rect.Props, p => p == rect.Color);
    }

    [Fact]
    public void Circle_BoundingBox_IsCenteredSquare()
    {
        var circle = new Circle { CenterX = { Value = 10f }, CenterY = { Value = 20f }, Radius = { Value = 5f } };

        var box = circle.GetWorldBoundingBox();

        Assert.Equal(5f, box.MinX);
        Assert.Equal(15f, box.MinY);
        Assert.Equal(15f, box.MaxX);
        Assert.Equal(25f, box.MaxY);
    }

    [Fact]
    public void Circle_Translate_MovesCenter()
    {
        var circle = new Circle { CenterX = { Value = 10f }, CenterY = { Value = 20f } };

        circle.Translate(3f, -4f);

        Assert.Equal(13f, circle.CenterX.Value);
        Assert.Equal(16f, circle.CenterY.Value);
    }

    [Fact]
    public void Rectangle_BoundingBox_IsPositionPlusSize()
    {
        var rect = Rect(10f, 20f, 30f, 40f);

        var box = rect.GetWorldBoundingBox();

        Assert.Equal(10f, box.MinX);
        Assert.Equal(20f, box.MinY);
        Assert.Equal(40f, box.MaxX);
        Assert.Equal(60f, box.MaxY);
    }

    [Fact]
    public void Rectangle_Translate_MovesPosition()
    {
        var rect = Rect(10f, 20f);

        rect.Translate(5f, 6f);

        Assert.Equal(15f, rect.PosX.Value);
        Assert.Equal(26f, rect.PosY.Value);
    }

    [Fact]
    public void Triangle_BoundingBox_CoversAllVertices()
    {
        var tri = new Triangle
        {
            Vertex1X = { Value = 0f },
            Vertex1Y = { Value = 0f },
            Vertex2X = { Value = 10f },
            Vertex2Y = { Value = 5f },
            Vertex3X = { Value = 5f },
            Vertex3Y = { Value = 15f },
        };

        var box = tri.GetWorldBoundingBox();

        Assert.Equal(0f, box.MinX);
        Assert.Equal(0f, box.MinY);
        Assert.Equal(10f, box.MaxX);
        Assert.Equal(15f, box.MaxY);
    }

    [Fact]
    public void Triangle_Translate_MovesAllVertices()
    {
        var tri = new Triangle
        {
            Vertex1X = { Value = 0f },
            Vertex1Y = { Value = 0f },
            Vertex2X = { Value = 10f },
            Vertex2Y = { Value = 5f },
            Vertex3X = { Value = 5f },
            Vertex3Y = { Value = 15f },
        };

        tri.Translate(2f, 3f);

        Assert.Equal(2f, tri.Vertex1X.Value);
        Assert.Equal(3f, tri.Vertex1Y.Value);
        Assert.Equal(12f, tri.Vertex2X.Value);
        Assert.Equal(8f, tri.Vertex2Y.Value);
        Assert.Equal(7f, tri.Vertex3X.Value);
        Assert.Equal(18f, tri.Vertex3Y.Value);
    }

    [Fact]
    public void Primitive_SetWorldTransform_AppliesDeltaTranslation()
    {
        // SetWorldTransform applies a delta from the current anchor (MinX, MaxY),
        // so Translate(30,40) moves the box so MinX=30 and MaxY=40.
        var rect = Rect(10f, 20f);
        var target = Transform.Translate(30f, 40f);

        rect.SetWorldTransform(target);

        Assert.Equal(30f, rect.PosX.Value);
        Assert.Equal(30f, rect.PosY.Value);
    }

    [Fact]
    public void Primitive_GetWorldTransform_ReturnsCurrentAnchor()
    {
        // The anchor is the bounding box top-left in (x, y-down): (MinX, MaxY).
        var rect = Rect(10f, 20f);

        var t = rect.GetWorldTransform();

        Assert.Equal(10f, t.Tx);
        Assert.Equal(30f, t.Ty);
    }

    private static Rectangle Rect(float x, float y, float w = 10f, float h = 10f)
        => new Rectangle { PosX = { Value = x }, PosY = { Value = y }, Width = { Value = w }, Height = { Value = h } };
}
