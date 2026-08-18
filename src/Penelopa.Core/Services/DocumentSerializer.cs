using System.Text.Json;
using Penelopa.Core.Primitives;

namespace Penelopa.Core.Services;

/// <summary>
/// 图元树的 JSON 序列化/反序列化。选区以图元 Id 存储，加载后可重新解析。
/// 容器子元素嵌套在父节点内。
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

    /// <summary>反序列化文档。JSON 无效时返回 null。调用方根据重建的树重新解析选区 Id。</summary>
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
            return null;
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException or OverflowException or InvalidCastException or ArgumentException)
        {
            // JSON 合法但文档结构异常（未知类型/种类或属性类型不匹配），
            // 如手工编辑或未来版本文件。与无效 JSON 同等处理。
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

/// <summary>文档反序列化结果。</summary>
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
