using System.Text.Json.Serialization;

namespace DamascusUI;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    NumberHandling = JsonNumberHandling.AllowReadingFromString,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
// Inbound messages
[JsonSerializable(typeof(RegisterRequest))]
[JsonSerializable(typeof(SetTitleRequest))]
[JsonSerializable(typeof(SetWindowPropsRequest))]
[JsonSerializable(typeof(OpenWindowRequest))]
[JsonSerializable(typeof(SetMenuRequest))]
[JsonSerializable(typeof(NativeMenuItemDef))]
[JsonSerializable(typeof(SetDockBadgeRequest))]
[JsonSerializable(typeof(SetDockIconRequest))]
[JsonSerializable(typeof(NotificationRequest))]
[JsonSerializable(typeof(SubscribeRequest))]
[JsonSerializable(typeof(PublishRequest))]
// Outbound messages
[JsonSerializable(typeof(AckEnvelope))]
[JsonSerializable(typeof(RegisteredEnvelope))]
[JsonSerializable(typeof(ErrorEnvelope))]
[JsonSerializable(typeof(EventEnvelope))]
// Outbound event payloads
[JsonSerializable(typeof(NotificationWarningPayload))]
[JsonSerializable(typeof(NotificationDefaultPayload))]
[JsonSerializable(typeof(AboutEventPayload))]
[JsonSerializable(typeof(MenuItemClickedPayload))]
// HTTP responses
[JsonSerializable(typeof(HttpErrorBody))]
internal partial class ShellJsonContext : JsonSerializerContext { }
