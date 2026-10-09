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
[JsonSerializable(typeof(CapabilitiesResult))]
[JsonSerializable(typeof(CapabilitiesSummaryResult))]
[JsonSerializable(typeof(LicenseStatusResult))]
[JsonSerializable(typeof(DoctorResult))]
[JsonSerializable(typeof(VersionResult))]
[JsonSerializable(typeof(SchemaListResult))]
[JsonSerializable(typeof(UpdateResult))]
[JsonSerializable(typeof(SkillInstallResult))]
[JsonSerializable(typeof(SkillListResult))]
[JsonSerializable(typeof(FontListResult))]
[JsonSerializable(typeof(FontCheckResult))]
[JsonSerializable(typeof(AppResult))]
[JsonSerializable(typeof(ProductPreviewStartResult))]
[JsonSerializable(typeof(ProductPreviewStatusResult))]
[JsonSerializable(typeof(ReviewResult))]
[JsonSerializable(typeof(Aspose.Cli.Sdk.Views.ViewManifest))]
[JsonSerializable(typeof(Warning))]
[JsonSerializable(typeof(BackupInfo))]
[JsonSerializable(typeof(VerificationIssue))]
[JsonSerializable(typeof(JsonObject))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(System.Text.Json.JsonElement))]
public sealed partial class SdkJsonContext : JsonSerializerContext;
