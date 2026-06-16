using System.Text.Json;
using System.Text.Json.Serialization;

namespace DamascusUI;

public sealed record NativeMenuItemDef(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("label")] string? Label,
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("items")] List<NativeMenuItemDef>? Items
);

public sealed record NotificationRequest(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("body")] string Body,
    [property: JsonPropertyName("topic")] string? Topic,
    [property: JsonPropertyName("payload")] JsonElement? Payload
);
