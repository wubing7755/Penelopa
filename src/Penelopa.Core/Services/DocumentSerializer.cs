using System.Text.Json;
using Penelopa.Core.Primitives;

namespace Penelopa.Core.Services;

/// <summary>
/// Serializes the primitive tree to and from JSON. Color keys are never
/// serialized — loading rebuilds them via <see cref="ColorKeyManager"/> — and
/// the selection is stored as primitive ids so it can be re-resolved after
/// load. Container children are nested inside their parent.
/// </summary>
public static class DocumentSerializer
{
    private sealed class DocumentDto
    {
        public List<PrimitiveDto>? Primitives { get; set; }
        public List<Guid>? SelectionIds { get; set; }
    }

    private sealed class PrimitiveDto
    {
        public Guid Id { get; set; }
        public string? Type { get; set; }
        public string? Kind { get; set; }
        public Dictionary<string, object>? Props { get; set; }
        public List<PrimitiveDto>? Children { get; set; }
    }

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public static string Serialize(IReadOnlyList<Primitive> roots, IEnumerable<Primitive> selection)
    {
        var dto = new DocumentDto
        {
            Primitives = roots.Select(ToDto).ToList(),
            SelectionIds = selection.Select(p => p.Id).ToList(),
        };
        return JsonSerializer.Serialize(dto, Options);
    }

    /// <summary>
    /// Deserializes a document. Returns null when the JSON is invalid. The
    /// caller re-resolves the selection ids against the rebuilt tree.
    /// </summary>
    public static DeserializeResult? Deserialize(string json)
    {
        DocumentDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<DocumentDto>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }

        if (dto?.Primitives is null)
        {
            return null;
        }

        var roots = new List<Primitive>();
        foreach (var primitiveDto in dto.Primitives)
        {
            var primitive = FromDto(primitiveDto);
            if (primitive is not null)
            {
                roots.Add(primitive);
            }
        }

        return new DeserializeResult(roots, dto.SelectionIds ?? new List<Guid>());
    }

    private static PrimitiveDto ToDto(Primitive primitive)
    {
        var props = new Dictionary<string, object>();
        foreach (var prop in primitive.Props)
        {
            if (ReferenceEquals(prop, primitive.ColorKey))
            {
                continue;
            }

            switch (prop)
            {
                case FloatPropValue fp: props[prop.Name] = fp.Value; break;
                case DoublePropValue dp: props[prop.Name] = dp.Value; break;
                case IntPropValue ip: props[prop.Name] = ip.Value; break;
                case BoolPropValue bp: props[prop.Name] = bp.Value; break;
                case StringPropValue sp: props[prop.Name] = sp.Value; break;
                case UintPropValue up: props[prop.Name] = up.Value; break;
            }
        }

        var dto = new PrimitiveDto
        {
            Id = primitive.Id,
            Type = primitive.GetType().Name,
            Props = props,
        };
        if (primitive is Container container)
        {
            dto.Kind = container.Kind.ToString();
            dto.Children = container.Children.Select(ToDto).ToList();
        }

        return dto;
    }

    private static Primitive? FromDto(PrimitiveDto dto)
    {
        Primitive primitive = dto.Type switch
        {
            "Circle" => new Circle(),
            "Rectangle" => new Rectangle(),
            "Triangle" => new Triangle(),
            "Container" => CreateContainer(dto.Kind),
            _ => throw new InvalidOperationException($"Unknown primitive type '{dto.Type}'."),
        };
        primitive.Id = dto.Id;

        if (dto.Props is not null)
        {
            ApplyProps(primitive, dto.Props);
        }

        if (primitive is Container container && dto.Children is not null)
        {
            foreach (var childDto in dto.Children)
            {
                var child = FromDto(childDto);
                if (child is not null)
                {
                    container.AddChild(child);
                }
            }
        }

        return primitive;
    }

    private static Container CreateContainer(string? kind)
    {
        return kind switch
        {
            "Offset" => Container.CreateOffset("Offset Container", 0f, 0f),
            "Rotation" => Container.CreateRotation("Rotate Container", 0f),
            "Flip" => Container.CreateFlip("Flip Container", flipX: false, flipY: false),
            _ => throw new InvalidOperationException($"Unknown container kind '{kind}'."),
        };
    }

    private static void ApplyProps(Primitive primitive, Dictionary<string, object> props)
    {
        foreach (var prop in primitive.Props)
        {
            if (ReferenceEquals(prop, primitive.ColorKey))
            {
                continue;
            }

            if (!props.TryGetValue(prop.Name, out var value))
            {
                continue;
            }

            switch (prop)
            {
                case FloatPropValue fp: fp.Value = ToSingle(value); break;
                case DoublePropValue dp: dp.Value = ToDouble(value); break;
                case IntPropValue ip: ip.Value = ToInt32(value); break;
                case BoolPropValue bp: bp.Value = ToBoolean(value); break;
                case StringPropValue sp: sp.Value = ToString(value); break;
                case UintPropValue up: up.Value = ToUInt32(value); break;
            }
        }
    }

    // System.Text.Json deserializes dictionary numbers as JsonElement, so the
    // plain Convert helpers would throw.

    private static float ToSingle(object value)
        => value is JsonElement element ? element.GetSingle() : Convert.ToSingle(value);

    private static double ToDouble(object value)
        => value is JsonElement element ? element.GetDouble() : Convert.ToDouble(value);

    private static int ToInt32(object value)
        => value is JsonElement element ? element.GetInt32() : Convert.ToInt32(value);

    private static bool ToBoolean(object value)
        => value is JsonElement element ? element.GetBoolean() : Convert.ToBoolean(value);

    private static string ToString(object value)
        => value is JsonElement element ? element.GetString() ?? string.Empty : Convert.ToString(value) ?? string.Empty;

    private static uint ToUInt32(object value)
        => value is JsonElement element ? element.GetUInt32() : Convert.ToUInt32(value);
}

/// <summary>Result of deserializing a document.</summary>
public sealed class DeserializeResult
{
    public DeserializeResult(IReadOnlyList<Primitive> roots, IReadOnlyList<Guid> selectionIds)
    {
        Roots = roots;
        SelectionIds = selectionIds;
    }

    public IReadOnlyList<Primitive> Roots { get; }

    public IReadOnlyList<Guid> SelectionIds { get; }
}
