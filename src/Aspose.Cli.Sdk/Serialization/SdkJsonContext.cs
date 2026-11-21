using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Sdk.Serialization;

/// <summary>Source-generated JSON metadata for SDK-owned wire contracts.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true)]
[JsonSerializable(typeof(ErrorEnvelope))]
[JsonSerializable(typeof(VersionResult))]
[JsonSerializable(typeof(FontListResult))]
[JsonSerializable(typeof(DryRunResult))]
[JsonSerializable(typeof(Warning))]
[JsonSerializable(typeof(JsonObject))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(System.Text.Json.JsonElement))]
public sealed partial class SdkJsonContext : JsonSerializerContext;
