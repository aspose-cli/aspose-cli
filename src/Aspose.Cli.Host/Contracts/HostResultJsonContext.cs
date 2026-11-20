using System.Text.Json.Serialization;

namespace Aspose.Cli.Host.Contracts;

/// <summary>
/// Source-generated JSON metadata for the results only the host writes. The options are the
/// SDK's, so these results serialize exactly as the SDK's own; their schemas are published
/// under <c>common</c> beside the SDK's.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true)]
[JsonSerializable(typeof(BudgetListResult))]
[JsonSerializable(typeof(CommandDocument))]
[JsonSerializable(typeof(DiagnosticListResult))]
[JsonSerializable(typeof(FormatListResult))]
[JsonSerializable(typeof(LicenseStatusResult))]
[JsonSerializable(typeof(SchemaCatalogResult))]
[JsonSerializable(typeof(SkillInstallResult))]
[JsonSerializable(typeof(SkillListResult))]
internal sealed partial class HostResultJsonContext : JsonSerializerContext;
