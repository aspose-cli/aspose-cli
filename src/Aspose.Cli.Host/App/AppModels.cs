using System.Text.Json.Serialization;
using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Host.App;

internal sealed record AppRecentView(
    string Id,
    string Name,
    string? ProductId,
    string? ProductName,
    string? View);

internal sealed record AppDiagnosticView(
    string Name,
    string Status,
    string Detail,
    string? Hint = null);

internal sealed record AppPreviewView(string Id, string DisplayName);

internal sealed record AppSkillView(
    string Product,
    string Name,
    string Description);

internal sealed record AppSkillSummary(
    string Name,
    string Description);

internal sealed record AppFormatView(
    string Id,
    IReadOnlyList<string> Extensions,
    IReadOnlyList<string> Uses);

internal sealed record AppPreviewCapabilityView(
    string Fidelity,
    string DefaultView,
    IReadOnlyList<AppPreviewView> Views);

internal sealed record AppReviewCapabilityView(
    string Fidelity,
    string DefaultView,
    IReadOnlyList<AppPreviewView> Views,
    bool VisualInspectionRequired);

internal sealed record AppProductView(
    string Id,
    string Name,
    IReadOnlyList<AppFormatView> Formats,
    IReadOnlyList<string> Verbs,
    AppPreviewCapabilityView Preview,
    AppReviewCapabilityView Review,
    AppSkillSummary? Skill);

internal sealed record AppStatusView(
    string Version,
    string Edition,
    string EditionName,
    string Experience,
    string Route,
    bool OnboardingCompleted,
    string Product,
    string DefaultView,
    IReadOnlyList<string> AvailableViews,
    IReadOnlyList<AppPreviewView> PreviewViews,
    IReadOnlyList<string> SupportedExtensions,
    IReadOnlyList<AppSkillView> Skills,
    bool RememberRecentFiles,
    LicenseStatusResult License,
    string? File,
    bool UploadedCopy,
    string? PreviewUrl,
    IReadOnlyList<AppRecentView> RecentFiles,
    IReadOnlyList<AppDiagnosticView> Diagnostics,
    IReadOnlyList<AppProductView> Products)
{
    [JsonPropertyOrder(-100)]
    public string Schema => CommonSchemaIds.AppStatus;

    [JsonPropertyOrder(-99)]
    public int SchemaVersion => 2;
}

internal sealed record AppIdRequest(string Id);

internal sealed record AppPreferenceRequest(
    string? Product,
    string DefaultView,
    bool RememberRecentFiles);

internal sealed record AppApiResult(
    bool Ok,
    string? Code = null,
    string? Message = null,
    bool FallbackUpload = false);

internal sealed record AppHealthResult(bool Ok, int Pid);

internal sealed record AppRestartResult(bool Ok, string RestartUrl);
