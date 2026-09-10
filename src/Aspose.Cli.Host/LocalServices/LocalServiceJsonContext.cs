using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aspose.Cli.Host.LocalServices;

[JsonSourceGenerationOptions(
    JsonSerializerDefaults.Web,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(LocalServiceControlRequest))]
[JsonSerializable(typeof(LocalServiceControlResponse))]
internal sealed partial class LocalServiceJsonContext : JsonSerializerContext;
