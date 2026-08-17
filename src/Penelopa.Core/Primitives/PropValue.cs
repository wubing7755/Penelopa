using System.Text.Json;

namespace Penelopa.Core.Primitives;

/// <summary>
/// A named property value attached to a primitive.
/// </summary>
public abstract class PropValue
{
    public string Name { get; set; }

    protected PropValue(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Returns the property value boxed as <see cref="object"/>, for
    /// serialization and history snapshots. Returns null for an unknown
    /// <see cref="PropValue"/> subtype (which callers skip).
    /// </summary>
    public object? GetBoxedValue() => this switch
    {
        FloatPropValue fp => fp.Value,
        DoublePropValue dp => dp.Value,
        IntPropValue ip => ip.Value,
        BoolPropValue bp => bp.Value,
        StringPropValue sp => sp.Value,
        UintPropValue up => up.Value,
        _ => null,
    };

    /// <summary>
    /// Sets the property value from a boxed value. Values read back from JSON
    /// are <see cref="JsonElement"/>, so both those and plain boxed values
    /// (from history snapshots) are accepted.
    /// </summary>
    public void SetBoxedValue(object value)
    {
        switch (this)
        {
            case FloatPropValue fp: fp.Value = ToSingle(value); break;
            case DoublePropValue dp: dp.Value = ToDouble(value); break;
            case IntPropValue ip: ip.Value = ToInt32(value); break;
            case BoolPropValue bp: bp.Value = ToBoolean(value); break;
            case StringPropValue sp: sp.Value = ToText(value); break;
            case UintPropValue up: up.Value = ToUInt32(value); break;
        }
    }

    private static float ToSingle(object value)
        => value is JsonElement element ? element.GetSingle() : Convert.ToSingle(value);

    private static double ToDouble(object value)
        => value is JsonElement element ? element.GetDouble() : Convert.ToDouble(value);

    private static int ToInt32(object value)
        => value is JsonElement element ? element.GetInt32() : Convert.ToInt32(value);

    private static bool ToBoolean(object value)
        => value is JsonElement element ? element.GetBoolean() : Convert.ToBoolean(value);

    private static string ToText(object value)
        => value is JsonElement element ? element.GetString() ?? string.Empty : Convert.ToString(value) ?? string.Empty;

    private static uint ToUInt32(object value)
        => value is JsonElement element ? element.GetUInt32() : Convert.ToUInt32(value);
}

public interface IPropValue<T>
{
    T Value { get; set; }
}

public class IntPropValue : PropValue, IPropValue<int>
{
    public int Value { get; set; }

    public IntPropValue(string name, int value) : base(name)
    {
        Value = value;
    }
}

public class FloatPropValue : PropValue, IPropValue<float>
{
    public float Value { get; set; }

    public FloatPropValue(string name, float value) : base(name)
    {
        Value = value;
    }
}

public class DoublePropValue : PropValue, IPropValue<double>
{
    public double Value { get; set; }

    public DoublePropValue(string name, double value) : base(name)
    {
        Value = value;
    }
}

public class BoolPropValue : PropValue, IPropValue<bool>
{
    public bool Value { get; set; }

    public BoolPropValue(string name, bool value) : base(name)
    {
        Value = value;
    }
}

public class StringPropValue : PropValue, IPropValue<string>
{
    public string Value { get; set; }

    public StringPropValue(string name, string value) : base(name)
    {
        Value = value;
    }
}

public class UintPropValue : PropValue, IPropValue<uint>
{
    public uint Value { get; set; }

    public UintPropValue(string name, uint value) : base(name)
    {
        Value = value;
    }
}
