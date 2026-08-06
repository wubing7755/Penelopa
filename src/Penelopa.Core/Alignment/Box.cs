namespace Penelopa.Core.Alignment;

/// <summary>
/// An axis-aligned bounding box.
/// </summary>
public readonly struct Box : IEquatable<Box>
{
    /// <summary>Gets the minimum X coordinate.</summary>
    public float MinX { get; }

    /// <summary>Gets the minimum Y coordinate.</summary>
    public float MinY { get; }

    /// <summary>Gets the maximum X coordinate.</summary>
    public float MaxX { get; }

    /// <summary>Gets the maximum Y coordinate.</summary>
    public float MaxY { get; }

    /// <summary>Gets the box width.</summary>
    public float Width => MaxX - MinX;

    /// <summary>Gets the box height.</summary>
    public float Height => MaxY - MinY;

    /// <summary>Gets the X coordinate of the box center.</summary>
    public float CenterX => MinX + Width * 0.5f;

    /// <summary>Gets the Y coordinate of the box center.</summary>
    public float CenterY => MinY + Height * 0.5f;

    /// <summary>Gets the box center.</summary>
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

    public static bool operator ==(Box left, Box right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Box left, Box right)
    {
        return !(left == right);
    }

    public override string ToString()
    {
        return $"Box: Min=({MinX}, {MinY}), Max=({MaxX}, {MaxY}), Size=({Width}, {Height})";
    }
}
