namespace Penelopa.Core.Primitives;

/// <summary>
/// An ARGB color value that can be converted to and from a packed uint key.
/// </summary>
public readonly struct Color
{
    public static readonly Color Transparent = new(0, 0, 0, 0);
    public static readonly Color Black = new(0, 0, 0);
    public static readonly Color White = new(255, 255, 255);

    public byte R { get; }
    public byte G { get; }
    public byte B { get; }
    public byte A { get; }

    public Color(byte r, byte g, byte b, byte a = 255)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    /// <summary>
    /// Reconstructs a color from a packed key: 0xAARRGGBB.
    /// </summary>
    public static Color FromUint(uint colorKey) => new(
        (byte)((colorKey >> 16) & 0xFF),
        (byte)((colorKey >> 8) & 0xFF),
        (byte)(colorKey & 0xFF),
        (byte)((colorKey >> 24) & 0xFF)
    );

    /// <summary>
    /// Packs the color into a uint: 0xAARRGGBB.
    /// </summary>
    public uint ToUint() => (uint)(
        (A << 24) |
        (R << 16) |
        (G << 8) |
        B
    );
}
