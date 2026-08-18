namespace Penelopa.Core.Alignment;

/// <summary>轴对齐包围盒（AABB）。</summary>
public readonly struct Box : IEquatable<Box>
{
    public float MinX { get; }
    public float MinY { get; }
    public float MaxX { get; }
    public float MaxY { get; }
    public float Width => MaxX - MinX;
    public float Height => MaxY - MinY;
    public float CenterX => MinX + Width * 0.5f;
    public float CenterY => MinY + Height * 0.5f;
    public Point Center => new(CenterX, CenterY);

    public Box(float minX, float minY, float maxX, float maxY)
    {
        if (minX > maxX) throw new ArgumentException("minX cannot be greater than maxX.");
        if (minY > maxY) throw new ArgumentException("minY cannot be greater than maxY.");

        MinX = minX;
        MinY = minY;
        MaxX = maxX;
        MaxY = maxY;
    }

    public static Box FromSize(float x, float y, float width, float height)
    {
        return new Box(x, y, x + width, y + height);
    }

    public bool Equals(Box other)
    {
        return MinX.Equals(other.MinX) && MinY.Equals(other.MinY)
            && MaxX.Equals(other.MaxX) && MaxY.Equals(other.MaxY);
    }

    public override bool Equals(object? obj)
    {
        return obj is Box other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(MinX, MinY, MaxX, MaxY);
    }

    public static bool operator ==(Box left, Box right) => left.Equals(right);
    public static bool operator !=(Box left, Box right) => !(left == right);

    public override string ToString()
    {
        return $"Box: Min=({MinX}, {MinY}), Max=({MaxX}, {MaxY}), Size=({Width}, {Height})";
    }
}
