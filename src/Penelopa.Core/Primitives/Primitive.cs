using Penelopa.Core.Alignment;

namespace Penelopa.Core.Primitives;

/// <summary>
/// Base class for geometric primitives that expose a set of named properties
/// and can be translated in world space.
/// </summary>
public abstract class Primitive : IAlignable
{
    protected Primitive(string name)
    {
        Id = Guid.NewGuid();
        Props = new();

        Name = new StringPropValue("Name", name);
        AddProp(Name);
        ColorKey = new UintPropValue("ColorKey", ColorKeyManager.GenerateColorKey(this));
        AddProp(ColorKey);
    }

    public Guid Id { get; }

    public UintPropValue ColorKey { get; }

    public StringPropValue Name { get; }

    public List<PropValue> Props { get; }

    protected void AddProp(PropValue propValue)
    {
        Props.Add(propValue);
    }

    /// <inheritdoc/>
    public abstract Box GetWorldBoundingBox();

    /// <summary>Translates the primitive by a delta.</summary>
    public abstract void Translate(float deltaX, float deltaY);

    /// <summary>
    /// Fits the primitive's geometry into the given world-space bounding box.
    /// </summary>
    /// <param name="bounds">The target world-space axis-aligned bounds.</param>
    /// <param name="anchor">The fixed corner of a resize gesture. Shapes that
    /// re-center themselves (such as a circle) keep this point on their
    /// boundary so the opposite corner follows the pointer; shapes that fill
    /// the box directly ignore it.</param>
    /// <returns>The actual fitted bounds. Shapes that preserve an aspect
    /// ratio (such as a circle) may not fill the target exactly, so callers
    /// must use the returned box to correct the selection overlay.</returns>
    public abstract Box SetBounds(Box bounds, Point anchor);

    /// <summary>
    /// Returns whether the given world point lies inside the primitive's
    /// shape. Used by hit-through candidate collection and marquee tests.
    /// </summary>
    public abstract bool ContainsWorldPoint(Point point);

    /// <inheritdoc/>
    public Transform GetWorldTransform()
    {
        var currentPos = GetCurrentPosition();
        return Transform.Translate(currentPos.X, currentPos.Y);
    }

    /// <inheritdoc/>
    public void SetWorldTransform(Transform transform)
    {
        var currentPos = GetCurrentPosition();
        var deltaX = transform.Tx - currentPos.X;
        var deltaY = transform.Ty - currentPos.Y;
        Translate(deltaX, deltaY);
    }

    /// <summary>
    /// Gets the top-left anchor of the bounding box in screen coordinates
    /// (x grows right, y grows down, so the anchor is (MinX, MaxY)).
    /// </summary>
    protected Point GetCurrentPosition()
    {
        var bbox = GetWorldBoundingBox();
        return new Point(bbox.MinX, bbox.MaxY);
    }
}

/// <summary>
/// A circle defined by a center and a radius.
/// </summary>
public class Circle : Primitive
{
    public Circle() : base("Circle")
    {
        CenterX = new FloatPropValue("CenterX", 10.0f);
        CenterY = new FloatPropValue("CenterY", 10.0f);
        Radius = new FloatPropValue("Radius", 5.0f);
        Color = new UintPropValue("Color", 0xFFFFFFFF);

        AddProp(CenterX);
        AddProp(CenterY);
        AddProp(Radius);
        AddProp(Color);
    }

    public FloatPropValue CenterX { get; }
    public FloatPropValue CenterY { get; }
    public FloatPropValue Radius { get; }
    public UintPropValue Color { get; }

    public override Box GetWorldBoundingBox()
    {
        float left = CenterX.Value - Radius.Value;
        float top = CenterY.Value - Radius.Value;
        float right = CenterX.Value + Radius.Value;
        float bottom = CenterY.Value + Radius.Value;
        return new Box(left, top, right, bottom);
    }

    public override void Translate(float deltaX, float deltaY)
    {
        CenterX.Value += deltaX;
        CenterY.Value += deltaY;
    }

    public override Box SetBounds(Box bounds, Point anchor)
    {
        // A circle keeps its aspect. The anchor is the resize gesture's
        // fixed corner and stays on the circle: the center is derived from
        // the anchor plus the inscribed radius, so the circle grows from the
        // fixed corner instead of re-centering in the target box.
        float radius = MathF.Max(0f, MathF.Min(bounds.Width, bounds.Height) * 0.5f);

        const float eps = 1e-4f;
        float centerX = anchor.X <= bounds.MinX + eps ? bounds.MinX + radius : bounds.MaxX - radius;
        float centerY = anchor.Y <= bounds.MinY + eps ? bounds.MinY + radius : bounds.MaxY - radius;

        CenterX.Value = centerX;
        CenterY.Value = centerY;
        Radius.Value = radius;
        return GetWorldBoundingBox();
    }

    public override bool ContainsWorldPoint(Point point)
    {
        float dx = point.X - CenterX.Value;
        float dy = point.Y - CenterY.Value;
        return dx * dx + dy * dy <= Radius.Value * Radius.Value;
    }
}

/// <summary>
/// A rectangle defined by a top-left position and a size.
/// </summary>
public class Rectangle : Primitive
{
    public Rectangle() : base("Rectangle")
    {
        PosX = new FloatPropValue("PosX", 10.0f);
        PosY = new FloatPropValue("PosY", 10.0f);
        Width = new FloatPropValue("Width", 10.0f);
        Height = new FloatPropValue("Height", 10.0f);
        Color = new UintPropValue("Color", 0xFFFFFFFF);

        AddProp(PosX);
        AddProp(PosY);
        AddProp(Width);
        AddProp(Height);
        AddProp(Color);
    }

    public FloatPropValue PosX { get; }
    public FloatPropValue PosY { get; }
    public FloatPropValue Width { get; }
    public FloatPropValue Height { get; }
    public UintPropValue Color { get; }

    public override Box GetWorldBoundingBox()
    {
        float left = PosX.Value;
        float top = PosY.Value;
        float right = PosX.Value + Width.Value;
        float bottom = PosY.Value + Height.Value;
        return new Box(left, top, right, bottom);
    }

    public override void Translate(float deltaX, float deltaY)
    {
        PosX.Value += deltaX;
        PosY.Value += deltaY;
    }

    public override Box SetBounds(Box bounds, Point anchor)
    {
        PosX.Value = bounds.MinX;
        PosY.Value = bounds.MinY;
        Width.Value = bounds.Width;
        Height.Value = bounds.Height;
        return bounds;
    }

    public override bool ContainsWorldPoint(Point point)
    {
        var box = GetWorldBoundingBox();
        return point.X >= box.MinX && point.X <= box.MaxX
            && point.Y >= box.MinY && point.Y <= box.MaxY;
    }
}

/// <summary>
/// A triangle defined by three vertices.
/// </summary>
public class Triangle : Primitive
{
    public Triangle() : base("Triangle")
    {
        Vertex1X = new FloatPropValue("Vertex1X", 50.0f);
        Vertex1Y = new FloatPropValue("Vertex1Y", 50.0f);
        Vertex2X = new FloatPropValue("Vertex2X", 70.0f);
        Vertex2Y = new FloatPropValue("Vertex2Y", 50.0f);
        Vertex3X = new FloatPropValue("Vertex3X", 60.0f);
        Vertex3Y = new FloatPropValue("Vertex3Y", 60.0f);
        Color = new UintPropValue("Color", 0xFFFFFFFF);

        AddProp(Vertex1X);
        AddProp(Vertex1Y);
        AddProp(Vertex2X);
        AddProp(Vertex2Y);
        AddProp(Vertex3X);
        AddProp(Vertex3Y);
        AddProp(Color);
    }

    public FloatPropValue Vertex1X { get; }
    public FloatPropValue Vertex1Y { get; }
    public FloatPropValue Vertex2X { get; }
    public FloatPropValue Vertex2Y { get; }
    public FloatPropValue Vertex3X { get; }
    public FloatPropValue Vertex3Y { get; }
    public UintPropValue Color { get; }

    public override Box GetWorldBoundingBox()
    {
        float minX = Math.Min(Math.Min(Vertex1X.Value, Vertex2X.Value), Vertex3X.Value);
        float maxX = Math.Max(Math.Max(Vertex1X.Value, Vertex2X.Value), Vertex3X.Value);
        float minY = Math.Min(Math.Min(Vertex1Y.Value, Vertex2Y.Value), Vertex3Y.Value);
        float maxY = Math.Max(Math.Max(Vertex1Y.Value, Vertex2Y.Value), Vertex3Y.Value);
        return new Box(minX, minY, maxX, maxY);
    }

    public override void Translate(float deltaX, float deltaY)
    {
        Vertex1X.Value += deltaX; Vertex1Y.Value += deltaY;
        Vertex2X.Value += deltaX; Vertex2Y.Value += deltaY;
        Vertex3X.Value += deltaX; Vertex3Y.Value += deltaY;
    }

    public override Box SetBounds(Box bounds, Point anchor)
    {
        // Map the vertices by their normalized position inside the current
        // bounds, so the triangle's shape is preserved while filling the
        // target box exactly. The anchor is implicit in the box's corners.
        var current = GetWorldBoundingBox();
        float scaleX = current.Width > 1e-6f ? bounds.Width / current.Width : 0f;
        float scaleY = current.Height > 1e-6f ? bounds.Height / current.Height : 0f;

        Vertex1X.Value = bounds.MinX + (Vertex1X.Value - current.MinX) * scaleX;
        Vertex1Y.Value = bounds.MinY + (Vertex1Y.Value - current.MinY) * scaleY;
        Vertex2X.Value = bounds.MinX + (Vertex2X.Value - current.MinX) * scaleX;
        Vertex2Y.Value = bounds.MinY + (Vertex2Y.Value - current.MinY) * scaleY;
        Vertex3X.Value = bounds.MinX + (Vertex3X.Value - current.MinX) * scaleX;
        Vertex3Y.Value = bounds.MinY + (Vertex3Y.Value - current.MinY) * scaleY;
        return GetWorldBoundingBox();
    }

    public override bool ContainsWorldPoint(Point point)
    {
        var box = GetWorldBoundingBox();
        if (box.Width < 1e-6f || box.Height < 1e-6f)
        {
            // Degenerate (zero-area) triangles are not hittable.
            return false;
        }

        // Cross-product sign test: the point is inside (or on the boundary)
        // when all three edge tests share a sign.
        float d1 = Cross(point, new Point(Vertex1X.Value, Vertex1Y.Value), new Point(Vertex2X.Value, Vertex2Y.Value));
        float d2 = Cross(point, new Point(Vertex2X.Value, Vertex2Y.Value), new Point(Vertex3X.Value, Vertex3Y.Value));
        float d3 = Cross(point, new Point(Vertex3X.Value, Vertex3Y.Value), new Point(Vertex1X.Value, Vertex1Y.Value));
        bool hasNegative = d1 < 0f || d2 < 0f || d3 < 0f;
        bool hasPositive = d1 > 0f || d2 > 0f || d3 > 0f;
        return !(hasNegative && hasPositive);
    }

    private static float Cross(Point p, Point a, Point b)
        => (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);
}
