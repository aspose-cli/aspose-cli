using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aspose.Cli.Host.Preview;

[JsonSourceGenerationOptions(
    JsonSerializerDefaults.Web,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(PreviewSessionMarker))]
[JsonSerializable(typeof(PreviewSessionSecrets))]
[JsonSerializable(typeof(PreviewInteractiveState))]
internal sealed partial class PreviewLocalServiceJsonContext
    : JsonSerializerContext;
