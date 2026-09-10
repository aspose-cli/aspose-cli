using System.Text.Json;
using System.Text.Json.Serialization;
using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Host.App;

[JsonSourceGenerationOptions(
    JsonSerializerDefaults.Web,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(AppResult))]
[JsonSerializable(typeof(AppInstance))]
[JsonSerializable(typeof(AppInstanceSecrets))]
internal sealed partial class AppLocalServiceJsonContext
    : JsonSerializerContext;
