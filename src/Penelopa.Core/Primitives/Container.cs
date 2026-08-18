using Penelopa.Core.Alignment;

namespace Penelopa.Core.Primitives;

/// <summary>The container's editing semantics, which decides which transform
/// properties are editable in the property panel.</summary>
public enum ContainerKind
{
    Offset,
    Rotation,
    Flip,
}

/// <summary>
/// A container groups child primitives and applies an affine transform to
/// them — the A661-style rotation / offset / flip container variants are all
/// factory forms of the same type. The transform is a TRS (translate ·
/// rotate · scale) combination driven by editable properties, so the
/// property panel can change the container's offset, rotation, and scale
/// like any other primitive. Only the properties relevant to the
/// <see cref="ContainerKind"/> are exposed for editing. The container itself
/// has no visible shape; children render inside its transform and hit
/// testing follows the rendered pixels. Its world bounding box is the AABB
/// of the transformed children, so selection boxes and alignment work
/// unchanged.
/// </summary>
public sealed class Container : Primitive
{
    private readonly List<Primitive> _children = new();

    public Container(
        string name,
        ContainerKind kind,
        float offsetX,
        float offsetY,
        float rotation,
        float scaleX,
        float scaleY)
        : base(name)
    {
        Kind = kind;
        OffsetX = new FloatPropValue("Offset X", offsetX);
        OffsetY = new FloatPropValue("Offset Y", offsetY);
        Rotation = new FloatPropValue("Rotation", rotation);
        ScaleX = new FloatPropValue("Scale X", scaleX);
        ScaleY = new FloatPropValue("Scale Y", scaleY);

        switch (kind)
        {
            case ContainerKind.Offset:
                AddProp(OffsetX);
                AddProp(OffsetY);
                break;
            case ContainerKind.Rotation:
                AddProp(Rotation);
                break;
            case ContainerKind.Flip:
                AddProp(ScaleX);
                AddProp(ScaleY);
                break;
        }
    }

    /// <summary>Gets the container's editing kind (decides the exposed props).</summary>
    public ContainerKind Kind { get; }

    /// <summary>Gets or sets the X offset property (editable in the panel).</summary>
    public FloatPropValue OffsetX { get; }

    /// <summary>Gets or sets the Y offset property (editable in the panel).</summary>
    public FloatPropValue OffsetY { get; }

    /// <summary>Gets or sets the rotation in degrees (editable in the panel).</summary>
    public FloatPropValue Rotation { get; }

    /// <summary>Gets or sets the X scale property (negative flips X, editable).</summary>
    public FloatPropValue ScaleX { get; }

    /// <summary>Gets or sets the Y scale property (negative flips Y, editable).</summary>
    public FloatPropValue ScaleY { get; }

    /// <summary>
    /// Gets the transform applied to children (container space → parent
    /// space). The properties are the source of truth; the matrix is the
    /// TRS composition, so panel edits take effect immediately.
    /// </summary>
    public Transform LocalTransform
    {
        get
        {
            // Floor the scale away from zero: a zero or near-zero scale makes
            // the transform singular, so Transform.Invert() throws and crashes
            // child drag/resize. The property panel may still hold the raw
            // value; the effective transform is always invertible.
            return Transform.Translate(OffsetX.Value, OffsetY.Value)
                .Multiply(Transform.Rotate(Rotation.Value))
                .Multiply(Transform.Scale(ClampScale(ScaleX.Value), ClampScale(ScaleY.Value)));
        }
    }

    /// <summary>Floors a scale magnitude so the local transform stays invertible.</summary>
    private static float ClampScale(float value)
        => value >= 0f
            ? MathF.Max(value, MinScaleMagnitude)
            : MathF.Min(value, -MinScaleMagnitude);

    private const float MinScaleMagnitude = 0.01f;

    /// <summary>Gets the child primitives in render order (first = bottom).</summary>
    public IReadOnlyList<Primitive> Children => _children;

    /// <summary>Creates a container that offsets its children.</summary>
    public static Container CreateOffset(string name, float offsetX, float offsetY)
        => new(name, ContainerKind.Offset, offsetX, offsetY, 0f, 1f, 1f);

    /// <summary>Creates a container that rotates its children (degrees).</summary>
    public static Container CreateRotation(string name, float degrees)
        => new(name, ContainerKind.Rotation, 0f, 0f, degrees, 1f, 1f);

    /// <summary>Creates a container that mirrors its children on the given axes.</summary>
    public static Container CreateFlip(string name, bool flipX, bool flipY)
        => new(name, ContainerKind.Flip, 0f, 0f, 0f, flipX ? -1f : 1f, flipY ? -1f : 1f);

    /// <summary>Adds a child, re-parenting it into this container's local space.</summary>
    public void AddChild(Primitive child)
    {
        if (child.Parent is not null)
        {
            throw new InvalidOperationException("A primitive can only have one parent.");
        }

        child.Parent = this;
        _children.Add(child);
    }

    /// <summary>Removes a child, detaching it to the root level.</summary>
    public void RemoveChild(Primitive child)
    {
        if (_children.Remove(child))
        {
            child.Parent = null;
        }
    }

    /// <summary>Transforms a bounds from this container's space to its parent's space.</summary>
    public Box TransformBoundsToParent(Box local)
        => TransformBox(local, LocalTransform);

    /// <summary>Transforms a bounds from parent space into this container's space.</summary>
    public Box ToLocalBounds(Box parent)
        => TransformBox(parent, LocalTransform.Invert());

    /// <summary>Transforms a bounds from this container's space to world space.</summary>
    public Box ToWorldBounds(Box local)
    {
        var box = local;
        for (Primitive? node = this; node is not null; node = node.Parent)
        {
            if (node is Container container)
            {
                box = container.TransformBoundsToParent(box);
            }
        }

        return box;
    }

    /// <summary>Converts a world-space delta into a delta in this container's space.</summary>
    public Point ToLocalVector(float worldDx, float worldDy)
    {
        var vector = Parent is Container parent
            ? parent.ToLocalVector(worldDx, worldDy)
            : new Point(worldDx, worldDy);
        return LocalTransform.Invert().ApplyToVector(vector.X, vector.Y);
    }

    /// <summary>Converts a world-space point into this container's space.</summary>
    public Point ToLocalPoint(Point world)
    {
        var point = Parent is Container parent ? parent.ToLocalPoint(world) : world;
        return LocalTransform.Invert().ApplyToPoint(point);
    }

    /// <inheritdoc/>
    protected override Box GetLocalBoundingBox()
    {
        // Children live in this container's space; applying the local
        // transform yields the content's extent in that space.
        return TransformBox(UnionChildrenBounds(), LocalTransform);
    }

    /// <inheritdoc/>
    protected override void TranslateLocal(float deltaX, float deltaY)
    {
        OffsetX.Value += deltaX;
        OffsetY.Value += deltaY;
    }

    /// <inheritdoc/>
    protected override Box SetBoundsLocal(Box bounds, Point anchor)
    {
        var current = GetLocalBoundingBox();
        float scaleX = ScaleFactor(current.Width, bounds.Width);
        float scaleY = ScaleFactor(current.Height, bounds.Height);

        if (!LocalTransform.IsAxisAligned)
        {
            // Rotated/flipped containers scale uniformly to avoid injecting
            // shear into the matrix.
            float uniform = MathF.Min(MathF.Abs(scaleX), MathF.Abs(scaleY));
            scaleX = MathF.Sign(scaleX) * uniform;
            scaleY = MathF.Sign(scaleY) * uniform;
        }

        // Scaling happens around the origin; translate so the anchor point
        // (the resize gesture's fixed corner) stays put. With the TRS model:
        // Scale(s) ∘ Translate(t) = Translate(t·s) ∘ Scale(s).
        float deltaX = anchor.X - anchor.X * scaleX;
        float deltaY = anchor.Y - anchor.Y * scaleY;
        OffsetX.Value = deltaX + OffsetX.Value * scaleX;
        OffsetY.Value = deltaY + OffsetY.Value * scaleY;
        ScaleX.Value *= scaleX;
        ScaleY.Value *= scaleY;
        return GetLocalBoundingBox();
    }

    /// <inheritdoc/>
    protected override bool ContainsLocalPoint(Point point)
    {
        // Children share this container's space.
        foreach (var child in _children)
        {
            if (child.ContainsParentLocalPoint(point))
            {
                return true;
            }
        }

        return false;
    }

    private Box UnionChildrenBounds()
    {
        if (_children.Count == 0)
        {
            return new Box(0f, 0f, 0f, 0f);
        }

        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;
        foreach (var child in _children)
        {
            var box = child.GetParentLocalBoundingBox();
            minX = MathF.Min(minX, box.MinX);
            minY = MathF.Min(minY, box.MinY);
            maxX = MathF.Max(maxX, box.MaxX);
            maxY = MathF.Max(maxY, box.MaxY);
        }

        return new Box(minX, minY, maxX, maxY);
    }

    private static float ScaleFactor(float current, float target)
    {
        if (current < 1e-6f)
        {
            return 1f;
        }

        float scale = target / current;
        return MathF.Max(MathF.Abs(scale), 0.01f) * MathF.Sign(scale);
    }

    private static Box TransformBox(Box box, Transform transform)
    {
        var p1 = transform.ApplyToPoint(new Point(box.MinX, box.MinY));
        var p2 = transform.ApplyToPoint(new Point(box.MaxX, box.MinY));
        var p3 = transform.ApplyToPoint(new Point(box.MinX, box.MaxY));
        var p4 = transform.ApplyToPoint(new Point(box.MaxX, box.MaxY));

        float minX = MathF.Min(MathF.Min(p1.X, p2.X), MathF.Min(p3.X, p4.X));
        float maxX = MathF.Max(MathF.Max(p1.X, p2.X), MathF.Max(p3.X, p4.X));
        float minY = MathF.Min(MathF.Min(p1.Y, p2.Y), MathF.Min(p3.Y, p4.Y));
        float maxY = MathF.Max(MathF.Max(p1.Y, p2.Y), MathF.Max(p3.Y, p4.Y));
        return new Box(minX, minY, maxX, maxY);
    }
}
