using System.Text.Json.Serialization;

namespace Aspose.Cli.Host.App;

/// <summary>Deterministic JSON metadata for the loopback App API.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(AppStatusView))]
[JsonSerializable(typeof(AppApiResult))]
[JsonSerializable(typeof(AppHealthResult))]
[JsonSerializable(typeof(AppRestartResult))]
[JsonSerializable(typeof(AppPreferenceRequest))]
[JsonSerializable(typeof(AppIdRequest))]
internal sealed partial class AppJsonContext : JsonSerializerContext;
