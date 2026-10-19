using System.Text.Json.Serialization;

namespace Aspose.Cli.Host.Contracts;

/// <summary>
/// Source-generated JSON metadata for the results only the host writes. The options are the
/// SDK's, so these results serialize exactly as the SDK's own; their schemas are published
/// under <c>v2/common</c> beside the SDK's.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true)]
[JsonSerializable(typeof(CapabilitiesSummaryResult))]
[JsonSerializable(typeof(LicenseStatusResult))]
[JsonSerializable(typeof(DoctorResult))]
[JsonSerializable(typeof(SchemaListResult))]
[JsonSerializable(typeof(UpdateResult))]
[JsonSerializable(typeof(SkillInstallResult))]
[JsonSerializable(typeof(SkillListResult))]
[JsonSerializable(typeof(AppResult))]
[JsonSerializable(typeof(ProductPreviewStartResult))]
[JsonSerializable(typeof(ProductPreviewStatusResult))]
internal sealed partial class HostResultJsonContext : JsonSerializerContext;
