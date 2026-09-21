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

/// <summary>One document the App has open, as the tab strip shows it.</summary>
internal sealed record AppOpenDocumentView(
    string Id,
    string FileName,
    string ProductId,
    string View,
    string Url,
    bool UploadedCopy,
    bool Active);

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
    string? SessionView,
    IReadOnlyList<AppOpenDocumentView> Documents,
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

/// <summary>Which view of one open document to show.</summary>
internal sealed record AppDocumentViewRequest(string Id, string View);

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

/// <summary>
/// A saved license change. Nothing restarts any more, so the address is
/// simply where the browser continues: the settings it came from.
/// </summary>
internal sealed record AppLicenseSavedResult(bool Ok, string ContinueUrl);
