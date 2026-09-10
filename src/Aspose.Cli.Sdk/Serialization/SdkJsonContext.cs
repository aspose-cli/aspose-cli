using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Preview;

namespace Aspose.Cli.Sdk.Serialization;

/// <summary>Source-generated JSON metadata for SDK-owned wire contracts.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true)]
[JsonSerializable(typeof(ErrorEnvelope))]
[JsonSerializable(typeof(CapabilitiesResult))]
[JsonSerializable(typeof(LicenseStatusResult))]
[JsonSerializable(typeof(DoctorResult))]
[JsonSerializable(typeof(SchemaListResult))]
[JsonSerializable(typeof(UpdateResult))]
[JsonSerializable(typeof(SkillInstallResult))]
[JsonSerializable(typeof(SkillListResult))]
[JsonSerializable(typeof(FontListResult))]
[JsonSerializable(typeof(FontCheckResult))]
[JsonSerializable(typeof(FontProfileInfo))]
[JsonSerializable(typeof(AppResult))]
[JsonSerializable(typeof(ProductPreviewStartResult))]
[JsonSerializable(typeof(ProductPreviewStatusResult))]
[JsonSerializable(typeof(ProductPreviewStopResult))]
[JsonSerializable(typeof(ProductPreviewPayload))]
[JsonSerializable(typeof(ReviewResult))]
[JsonSerializable(typeof(Warning))]
[JsonSerializable(typeof(BackupInfo))]
[JsonSerializable(typeof(MutationReceipt))]
[JsonSerializable(typeof(PackageMutationReceipt))]
[JsonSerializable(typeof(PackagePartChange))]
[JsonSerializable(typeof(JsonObject))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(System.Text.Json.JsonElement))]
public sealed partial class SdkJsonContext : JsonSerializerContext;
