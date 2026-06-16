using System.Text.Json.Serialization;

namespace DamascusUI;

public sealed record NativeMenuItemDef(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("label")] string? Label,
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("items")] List<NativeMenuItemDef>? Items
);
