namespace Penelopa.Core.Alignment;

/// <summary>
/// A two-dimensional affine transform matrix.
/// </summary>
/// <remarks>
/// <para>
/// 3x3 matrix, column-vector convention:
/// <code>
/// | a  c  tx |
/// | b  d  ty |
/// | 0  0  1  |
/// </code>
/// </para>
/// <para>
/// For an input point (x, y) the transformed point (x', y') is:
/// <code>
/// x' = a * x + c * y + tx
/// y' = b * x + d * y + ty
/// </code>
/// </para>
/// <para>
/// Common forms:
/// <code>
/// Translation: Transform(1, 0, 0, 1, tx, ty)
/// Scale:       Transform(sx, 0, 0, sy, 0, 0)
/// Rotation:    Transform(cos, sin, -sin, cos, 0, 0)
/// </code>
/// </para>
/// </remarks>
public struct Transform
{
    /// <summary>Gets or sets the X scale factor.</summary>
    public float A { get; set; }

    /// <summary>Gets or sets the Y-affecting skew factor.</summary>
    public float B { get; set; }

    /// <summary>Gets or sets the X-affecting skew factor.</summary>
    public float C { get; set; }

    /// <summary>Gets or sets the Y scale factor.</summary>
    public float D { get; set; }

    /// <summary>Gets or sets the X translation.</summary>
    public float Tx { get; set; }

    /// <summary>Gets or sets the Y translation.</summary>
    public float Ty { get; set; }

    public Transform(float a, float b, float c, float d, float tx, float ty)
        => (A, B, C, D, Tx, Ty) = (a, b, c, d, tx, ty);

    public static Transform Translate(float tx, float ty) => new(1, 0, 0, 1, tx, ty);

    /// <summary>Creates a rotation transform (degrees, counter-clockwise in world space).</summary>
    public static Transform Rotate(float degrees)
    {
        float radians = degrees * MathF.PI / 180f;
        float cos = MathF.Cos(radians);
        float sin = MathF.Sin(radians);
        return new Transform(cos, sin, -sin, cos, 0, 0);
    }

    /// <summary>Creates a scaling transform around the origin.</summary>
    public static Transform Scale(float scaleX, float scaleY) => new(scaleX, 0, 0, scaleY, 0, 0);

    /// <summary>Gets the identity transform.</summary>
    public static Transform Identity => new(1, 0, 0, 1, 0, 0);

    /// <summary>
    /// Returns this transform composed with <paramref name="other"/>:
    /// <c>this ∘ other</c>, which applies <paramref name="other"/> first.
    /// </summary>
    public Transform Multiply(Transform other)
    {
        return new Transform(
            A * other.A + C * other.B,
            B * other.A + D * other.B,
            A * other.C + C * other.D,
            B * other.C + D * other.D,
            A * other.Tx + C * other.Ty + Tx,
            B * other.Tx + D * other.Ty + Ty);
    }

    /// <summary>
    /// Returns the inverse transform. Throws when the determinant is zero
    /// (the transform collapses the plane).
    /// </summary>
    public Transform Invert()
    {
        float det = A * D - B * C;
        if (MathF.Abs(det) < 1e-6f)
        {
            throw new InvalidOperationException("Cannot invert a singular transform.");
        }

        float invDet = 1f / det;
        return new Transform(
            D * invDet,
            -B * invDet,
            -C * invDet,
            A * invDet,
            (C * Ty - D * Tx) * invDet,
            (B * Tx - A * Ty) * invDet);
    }

    /// <summary>Applies the transform (including translation) to a point.</summary>
    public Point ApplyToPoint(Point point)
        => new(A * point.X + C * point.Y + Tx, B * point.X + D * point.Y + Ty);

    /// <summary>Applies only the linear part of the transform to a vector.</summary>
    public Point ApplyToVector(float dx, float dy)
        => new(A * dx + C * dy, B * dx + D * dy);

    /// <summary>
    /// Gets whether the transform contains no rotation or skew, so world-axis
    /// scaling maps to local-axis scaling without introducing shear.
    /// </summary>
    public bool IsAxisAligned => MathF.Abs(B) < 1e-6f && MathF.Abs(C) < 1e-6f;
}
