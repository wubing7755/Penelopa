namespace Penelopa.Core.Alignment;

/// <summary>二维坐标点。</summary>
public readonly struct Point : IEquatable<Point>
{
    public float X { get; }
    public float Y { get; }

    public Point(float x, float y) => (X, Y) = (x, y);

    public bool Equals(Point other) => X.Equals(other.X) && Y.Equals(other.Y);
    public override bool Equals(object? obj) => obj is Point other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X, Y);
    public static bool operator ==(Point left, Point right) => left.Equals(right);
    public static bool operator !=(Point left, Point right) => !left.Equals(right);
    public override string ToString() => $"({X}, {Y})";
}
