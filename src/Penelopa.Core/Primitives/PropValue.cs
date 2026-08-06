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
