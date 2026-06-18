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

// Outgoing event payloads — named types required for source-generated JSON serialization.

public sealed record NotificationWarningPayload(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("body")] string Body,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("message")] string Message);

public sealed record NotificationDefaultPayload(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("body")] string Body);

public sealed record AboutEventPayload(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("message")] string Message);

public sealed record MenuItemClickedPayload(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("label")] string Label);

public sealed record HttpErrorBody(
    [property: JsonPropertyName("error")] string Error);
