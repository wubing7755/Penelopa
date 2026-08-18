using System.Text.Json;
using Penelopa.Core.Primitives;

namespace Penelopa.Core.Services;

/// <summary>
/// Serializes the primitive tree to and from JSON. The selection is stored as
/// primitive ids so it can be re-resolved after load. Container children are
/// nested inside their parent.
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
        try
        {
            var dto = JsonSerializer.Deserialize<DocumentDto>(json, Options);
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
        catch (JsonException)
        {
            // Malformed JSON.
            return null;
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException or OverflowException or InvalidCastException or ArgumentException)
        {
            // Valid JSON but a malformed document (unknown type/kind, or a
            // property of the wrong type), e.g. a hand-edited or future-version
            // file. Treat it the same as invalid JSON.
            return null;
        }
    }

    private static PrimitiveDto ToDto(Primitive primitive)
    {
        var props = new Dictionary<string, object>();
        foreach (var prop in primitive.Props)
        {
            if (prop.GetBoxedValue() is { } value)
            {
                props[prop.Name] = value;
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
            if (!props.TryGetValue(prop.Name, out var value))
            {
                continue;
            }

            prop.SetBoxedValue(value);
        }
    }
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
