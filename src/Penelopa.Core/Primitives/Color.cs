namespace Penelopa.Core.Primitives;

/// <summary>ARGB 颜色值，可与 uint 打包键互转。</summary>
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

    /// <summary>从打包键重建颜色：0xAARRGGBB。</summary>
    public static Color FromUint(uint colorKey) => new(
        (byte)((colorKey >> 16) & 0xFF),
        (byte)((colorKey >> 8) & 0xFF),
        (byte)(colorKey & 0xFF),
        (byte)((colorKey >> 24) & 0xFF)
    );

    /// <summary>打包为 uint：0xAARRGGBB。</summary>
    public uint ToUint() => (uint)(
        (A << 24) |
        (R << 16) |
        (G << 8) |
        B
    );
}
