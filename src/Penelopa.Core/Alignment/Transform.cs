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
}
