using System.Text.Json.Serialization;
using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Host.App;

/// <summary>One recently opened file the App offers to reopen.</summary>
/// <param name="Id">Opaque id of the entry.</param>
/// <param name="Name">The file's name.</param>
/// <param name="ProductId">Product that opened the file, when known.</param>
/// <param name="ProductName">Human-readable name of that product, when known.</param>
/// <param name="View">View the file was shown in, when known.</param>
internal sealed record AppRecentView(
    [property: Pattern("^[0-9a-f]{16}$")] string Id,
    [property: MinLength(1)] string Name,
    [property: MinLength(1)] string? ProductId,
    [property: MinLength(1)] string? ProductName,
    [property: MinLength(1)] string? View);

/// <summary>One check the App's settings show.</summary>
/// <param name="Name">What was checked, e.g. <c>Runtime</c>.</param>
/// <param name="Status">The outcome: ok, warn or fail.</param>
/// <param name="Detail">What was found.</param>
/// <param name="Hint">How to improve a warn or fail outcome.</param>
internal sealed record AppDiagnosticView(
    [property: MinLength(1)] string Name,
    [property: AllowedValues("ok", "warn", "fail")] string Status,
    [property: MinLength(1)] string Detail,
    [property: MinLength(1)] string? Hint = null);

/// <summary>One view a document can be shown in.</summary>
/// <param name="Id">Stable view id.</param>
/// <param name="DisplayName">Label people see.</param>
internal sealed record AppPreviewView(
    [property: MinLength(1)] string Id,
    [property: MinLength(1)] string DisplayName);

/// <summary>One document the App has open, as the tab strip shows it.</summary>
/// <param name="Id">Opaque id of the open document.</param>
/// <param name="FileName">The document's file name; full paths are never exposed.</param>
/// <param name="ProductId">Product that opened the document.</param>
/// <param name="View">View the document is shown in.</param>
/// <param name="Url">Opaque URL of the document's session.</param>
/// <param name="UploadedCopy">Whether the App shows an uploaded copy rather than the file itself.</param>
/// <param name="Active">Whether the document is the one shown.</param>
internal sealed record AppOpenDocumentView(
    [property: Pattern("^[0-9a-f]{32}$")] string Id,
    [property: MinLength(1)] string FileName,
    [property: Pattern("^[a-z0-9][a-z0-9-]*$")] string ProductId,
    [property: MinLength(1)] string View,
    string Url,
    bool UploadedCopy,
    bool Active);

/// <summary>One installable Agent Skill.</summary>
/// <param name="Name">The Skill's name.</param>
/// <param name="Description">What the Skill is for.</param>
/// <param name="Product">Product that owns the Skill; absent for the platform Skill every product builds on.</param>
internal sealed record AppSkillView(
    [property: MinLength(1)] string Name,
    [property: MinLength(1)] string Description,
    [property: MinLength(1)] string? Product);

/// <summary>One format a product reads or writes.</summary>
/// <param name="Id">Format id, e.g. <c>xlsx</c>.</param>
/// <param name="Extensions">File extensions of the format.</param>
/// <param name="Uses">What the product does with the format, such as <c>load</c> or <c>convert</c>.</param>
internal sealed record AppFormatView(
    [property: MinLength(1)] string Id,
    [property: Pattern("^\\.[a-z0-9]+$")] IReadOnlyList<string> Extensions,
    [property: MinLength(1)] IReadOnlyList<string> Uses);

/// <summary>How a product previews documents in the App.</summary>
/// <param name="Fidelity">Whether the preview is rendered by the engine or a semantic projection.</param>
/// <param name="DefaultView">View shown when none is chosen.</param>
/// <param name="Views">Views a document can be previewed in.</param>
internal sealed record AppPreviewCapabilityView(
    [property: AllowedValues("rendered", "semantic")] string Fidelity,
    [property: MinLength(1)] string DefaultView,
    [property: MinItems(1)] IReadOnlyList<AppPreviewView> Views);

/// <summary>How a product renders review evidence.</summary>
/// <param name="Fidelity">Whether the evidence is rendered by the engine or a semantic projection.</param>
/// <param name="DefaultView">View a review renders when none is chosen.</param>
/// <param name="Views">Views a review can render.</param>
/// <param name="VisualInspectionRequired">Whether the rendered evidence must be looked at.</param>
internal sealed record AppReviewCapabilityView(
    [property: AllowedValues("rendered", "semantic")] string Fidelity,
    [property: MinLength(1)] string DefaultView,
    [property: MinItems(1)] IReadOnlyList<AppPreviewView> Views,
    bool VisualInspectionRequired);

/// <summary>One product the App can open documents with.</summary>
/// <param name="Id">Product id, e.g. <c>cells</c>.</param>
/// <param name="Name">Human-readable product name.</param>
/// <param name="Formats">Formats the product reads or writes.</param>
/// <param name="Verbs">Commands the product offers.</param>
/// <param name="Preview">How the product previews documents.</param>
/// <param name="Review">How the product renders review evidence.</param>
internal sealed record AppProductView(
    [property: MinLength(1)] string Id,
    [property: MinLength(1)] string Name,
    [property: MinItems(1)] IReadOnlyList<AppFormatView> Formats,
    [property: MinItems(1), MinLength(1)] IReadOnlyList<string> Verbs,
    AppPreviewCapabilityView Preview,
    AppReviewCapabilityView Review);

/// <summary>Deterministic state the loopback-only browser workspace of <c>aspose-cli app</c> reads.</summary>
/// <param name="Version">CLI version.</param>
/// <param name="DisplayName">The distribution's display name.</param>
/// <param name="Experience">Whether every product is license-free or a license applies.</param>
/// <param name="Route">The App route to show, e.g. <c>home</c>.</param>
/// <param name="OnboardingCompleted">Whether the first-run choice was made.</param>
/// <param name="Product">Product of the open document, or the default product.</param>
/// <param name="DefaultView">View documents of that product open in.</param>
/// <param name="AvailableViews">Views that product offers.</param>
/// <param name="PreviewViews">Those views with their labels.</param>
/// <param name="SupportedExtensions">File extensions the App can open.</param>
/// <param name="Skills">Every installable Agent Skill: the platform Skill first, then one per product that ships one.</param>
/// <param name="RememberRecentFiles">Whether the App keeps a list of recent files.</param>
/// <param name="License">The license status of every product.</param>
/// <param name="File">File name of the active document, when one is open.</param>
/// <param name="UploadedCopy">Whether the active document is an uploaded copy.</param>
/// <param name="PreviewUrl">Opaque URL for the committed App document session; stable until that session is replaced.</param>
/// <param name="SessionView">View the open document is rendered in, which a saved preference reaches only when the document reopens.</param>
/// <param name="Documents">Documents the App has open, in the order they were opened; one of them is active.</param>
/// <param name="RecentFiles">Recently opened files, newest first.</param>
/// <param name="Diagnostics">Checks the App's settings show.</param>
/// <param name="Products">Every product the App can open documents with.</param>
[SchemaId(Id)]
internal sealed record AppStatusView(
    string Version,
    [property: MinLength(1)] string DisplayName,
    [property: AllowedValues("license-free", "licensed")] string Experience,
    [property: MinLength(1)] string Route,
    bool OnboardingCompleted,
    [property: MinLength(1)] string Product,
    [property: MinLength(1)] string DefaultView,
    [property: MinLength(1)] IReadOnlyList<string> AvailableViews,
    IReadOnlyList<AppPreviewView> PreviewViews,
    [property: Pattern("^\\.[a-z0-9]+$")] IReadOnlyList<string> SupportedExtensions,
    [property: MinItems(1)] IReadOnlyList<AppSkillView> Skills,
    bool RememberRecentFiles,
    LicenseStatusResult License,
    [property: MinLength(1)] string? File,
    bool UploadedCopy,
    string? PreviewUrl,
    [property: MinLength(1)] string? SessionView,
    IReadOnlyList<AppOpenDocumentView> Documents,
    [property: MaxItems(8)] IReadOnlyList<AppRecentView> RecentFiles,
    IReadOnlyList<AppDiagnosticView> Diagnostics,
    [property: MinItems(1)] IReadOnlyList<AppProductView> Products)
{
    /// <summary>The relative id the status schema is published under.</summary>
    public const string Id = "app-status";

    /// <summary>URI of the JSON schema the status conforms to.</summary>
    [JsonPropertyOrder(-100)]
    public string Schema => ResultEnvelope.SchemaUri("common", Id);

    /// <summary>Version of the status contract.</summary>
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
/// A saved license change. Nothing restarts, so the address is simply where
/// the browser continues: the settings it came from.
/// </summary>
internal sealed record AppLicenseSavedResult(bool Ok, string ContinueUrl);
