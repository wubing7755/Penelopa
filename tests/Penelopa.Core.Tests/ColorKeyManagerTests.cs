using Penelopa.Core.Primitives;
using Xunit;

namespace Penelopa.Core.Tests;

public class ColorKeyManagerTests
{
    [Fact]
    public void GenerateColorKey_ReturnsUniqueKeys()
    {
        var a = new Rectangle();
        var b = new Rectangle();

        var keyA = ColorKeyManager.GenerateColorKey(a);
        var keyB = ColorKeyManager.GenerateColorKey(b);

        Assert.NotEqual(keyA, keyB);
    }

    [Fact]
    public void TryGetPrimitive_ReturnsRegisteredPrimitive()
    {
        var rect = new Rectangle();
        var key = ColorKeyManager.GenerateColorKey(rect);

        var found = ColorKeyManager.TryGetPrimitive(key, out var primitive);

        Assert.True(found);
        Assert.Same(rect, primitive);
    }

    [Fact]
    public void ReleaseColorKey_RemovesMapping()
    {
        var rect = new Rectangle();
        var key = ColorKeyManager.GenerateColorKey(rect);

        ColorKeyManager.ReleaseColorKey(key);

        Assert.False(ColorKeyManager.TryGetPrimitive(key, out _));
    }

    [Fact]
    public void ConstructorRegistersColorKey()
    {
        var rect = new Rectangle();

        var found = ColorKeyManager.TryGetPrimitive(rect.ColorKey.Value, out var primitive);

        Assert.True(found);
        Assert.Same(rect, primitive);
    }
}
