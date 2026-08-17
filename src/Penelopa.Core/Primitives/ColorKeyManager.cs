using System.Collections.Concurrent;

namespace Penelopa.Core.Primitives;

/// <summary>
/// Maps color keys to primitives for hit testing on a SkiaSharp canvas:
/// each primitive is painted with a unique color and the pixel under the
/// pointer identifies the primitive.
/// </summary>
public static class ColorKeyManager
{
    private static readonly ConcurrentDictionary<uint, Primitive> ColorMap = new();

    // Keys start away from black to avoid colliding with a painted black pixel.
    private static uint _currentKey = 0xFF000001;

    /// <summary>
    /// Generates the next unique color key and registers the primitive under it.
    /// Wrap-around skips keys that are still in use so a live primitive never
    /// shares a key with a newly created one.
    /// </summary>
    public static uint GenerateColorKey(Primitive primitive)
    {
        lock (ColorMap)
        {
            for (int attempts = 0; attempts < MaxUsableKeys; attempts++)
            {
                // Handle key overflow (0xFFFFFFFF is the uint maximum).
                if (_currentKey >= 0xFFFFFFFE || _currentKey == 0xFF000000)
                    _currentKey = 0xFF000001;

                var colorKey = _currentKey++;
                if (!ColorMap.ContainsKey(colorKey))
                {
                    ColorMap[colorKey] = primitive;
                    return colorKey;
                }
            }

            throw new InvalidOperationException("Color key space exhausted.");
        }
    }

    private const int MaxUsableKeys = 0xFFFFFF; // 0xFF000001 .. 0xFFFFFFFD

    /// <summary>Looks up the primitive registered under a color key.</summary>
    public static bool TryGetPrimitive(uint colorKey, out Primitive? primitive) =>
        ColorMap.TryGetValue(colorKey, out primitive);

    /// <summary>Removes the mapping for a color key.</summary>
    public static void ReleaseColorKey(uint colorKey) =>
        ColorMap.TryRemove(colorKey, out _);
}
