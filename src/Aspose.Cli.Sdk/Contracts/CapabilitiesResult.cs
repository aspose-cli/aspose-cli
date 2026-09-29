namespace Aspose.Cli.Sdk.Contracts;

/// <summary>
/// Machine-readable self-description of the CLI: which products, verbs,
/// formats and ops this build supports. Agents use it for runtime discovery
/// instead of hard-coding command lists.
/// </summary>
public sealed record CapabilitiesResult() : ResultEnvelope(CommonSchemaIds.Capabilities, 2)
{
    /// <summary>CLI version, e.g. <c>0.2.0</c>.</summary>
    public required string CliVersion { get; init; }

    /// <summary>Root source revision embedded by the distribution build.</summary>
    public required string SourceRevision { get; init; }

    /// <summary>Whether tracked source changes were present during the build.</summary>
    public required bool BuildDirty { get; init; }

    /// <summary>Exact engine version or source pin compiled for each product.</summary>
    public required IReadOnlyList<EnginePinCapabilities> EnginePins { get; init; }

    /// <summary>One entry per compiled-in product engine.</summary>
    public required IReadOnlyList<ProductCapabilities> Products { get; init; }

    /// <summary>Schema ids printable via <c>aspose-cli schema &lt;id&gt;</c>.</summary>
    public required IReadOnlyList<string> Schemas { get; init; }

    /// <summary>Final operation-aware routing truth for this compiled distribution.</summary>
    public required RoutingCapabilities Routing { get; init; }

    /// <summary>Complete root command grammar, including global commands and options.</summary>
    public required IReadOnlyList<CommandCapabilities> Commands { get; init; }

    /// <summary>Versioned global resource defaults and hard safety maxima.</summary>
    public required IReadOnlyList<ResourceBudgetCapabilities> ResourceBudgets { get; init; }

    /// <summary>Version of the global/product resource-budget contract.</summary>
    public required int ResourceBudgetContractVersion { get; init; }

    /// <summary>Resolved common and compiled-product diagnostic catalog.</summary>
    public required IReadOnlyList<DiagnosticCapabilities> Diagnostics { get; init; }
}

/// <summary>One compiled product engine identity used for release provenance.</summary>
public sealed record EnginePinCapabilities
{
    public required string Product { get; init; }
    public required string Engine { get; init; }
    public required string Version { get; init; }
}

/// <summary>Machine-readable metadata for one stable CLI diagnostic.</summary>
public sealed record DiagnosticCapabilities
{
    public required string Code { get; init; }
    public required string Owner { get; init; }
    public required string Severity { get; init; }
    public int? ExitCode { get; init; }
    public required string Category { get; init; }
    public required string MessageTemplateId { get; init; }
    public required string HintTemplateId { get; init; }
    public required string DetailsSchemaId { get; init; }
}

/// <summary>Capabilities of one product surface (e.g. <c>cells</c>).</summary>
public sealed record ProductCapabilities
{
    /// <summary>Product id as used on the command line, e.g. <c>cells</c>.</summary>
    public required string Id { get; init; }

    /// <summary>Available verbs, e.g. <c>inspect</c>, <c>convert</c>.</summary>
    public IReadOnlyList<string> Verbs { get; init; } = [];

    /// <summary>Format ids accepted by <c>convert --to</c>.</summary>
    public IReadOnlyList<string> ConvertFormats { get; init; } = [];

    /// <summary>Format ids accepted by <c>render --to</c>.</summary>
    public IReadOnlyList<string> RenderFormats { get; init; } = [];

    /// <summary>Structured mutation operations exposed by this product.</summary>
    public required IReadOnlyList<ProductOperationDescriptor> Operations { get; init; }

    /// <summary>Format ids accepted as input by this product.</summary>
    public IReadOnlyList<string> LoadFormats { get; init; } = [];

    /// <summary>Default engine compiled into this product.</summary>
    public ProductEngineCapabilities? Engine { get; init; }

    /// <summary>Engine ids available in this build.</summary>
    public IReadOnlyList<string> AvailableEngines { get; init; } = [];

    /// <summary>Live-preview views and lifecycle support exposed by this product.</summary>
    public ProductPreviewCapabilities? Preview { get; init; }

    /// <summary>Static review evidence capabilities exposed by this product.</summary>
    public ProductReviewCapabilities? Review { get; init; }

    /// <summary>Canonical input/output format declarations.</summary>
    public IReadOnlyList<ProductFormatCapabilities> Formats { get; init; } = [];

    /// <summary>Complete product command grammar in stable path order.</summary>
    public IReadOnlyList<CommandCapabilities> Commands { get; init; } = [];

    /// <summary>Product-owned domain resource budgets.</summary>
    public IReadOnlyList<ResourceBudgetCapabilities> ResourceBudgets { get; init; } = [];
}

/// <summary>
/// One command that applies a bounded operation document, and the operations it accepts.
/// Every such command is atomic unless --best-effort is given and supports --dry-run.
/// </summary>
public sealed record ProductOperationDescriptor
{
    /// <summary>Product-relative command path, such as <c>edit</c>.</summary>
    public required string Command { get; init; }

    /// <summary>Product-owned schema id printable through <c>schema &lt;id&gt;</c>.</summary>
    public required string InputSchema { get; init; }

    /// <summary>The command that prints one operation's schema, with <c>&lt;op&gt;</c> standing for its name.</summary>
    public required string OperationSchema { get; init; }

    /// <summary>
    /// <c>sha256:</c> and the lowercase hex SHA-256 of the served input schema (UTF-8, <c>\n</c>
    /// line endings); it changes whenever the published schema text changes, descriptions
    /// included. It is computed once, when first read.
    /// </summary>
    public string ContractFingerprint => Schema.Fingerprint;

    /// <summary>Largest accepted number of operations in one document.</summary>
    public required int MaximumOperations { get; init; }

    /// <summary>Operation names in published order.</summary>
    public required IReadOnlyList<string> Ops { get; init; }

    /// <summary>The vocabulary's schema, which the command serves and the fingerprint hashes.</summary>
    internal Operations.GeneratedOperationSchema Schema { get; init; } = null!;
}

/// <summary>One discoverable default and hard maximum for a resource.</summary>
public sealed record ResourceBudgetCapabilities
{
    public required string Kind { get; init; }
    public required long Default { get; init; }
    public required long Maximum { get; init; }
    public required string Unit { get; init; }
    public string? Option { get; init; }
    public string? EnvironmentVariable { get; init; }
    public required string EnforcementStage { get; init; }

    public static ResourceBudgetCapabilities Domain(
        string kind,
        long defaultValue,
        long maximum,
        string unit,
        string enforcementStage,
        string? option = null,
        string? environmentVariable = null) =>
        new()
        {
            Kind = kind,
            Default = defaultValue,
            Maximum = maximum,
            Unit = unit,
            EnforcementStage = enforcementStage,
            Option = option,
            EnvironmentVariable = environmentVariable,
        };
}

/// <summary>Public identity and licensing requirements of one product engine.</summary>
public sealed record ProductEngineCapabilities
{
    public required string Id { get; init; }
    public required string Sdk { get; init; }
    public required string SdkVersion { get; init; }
    public required bool LicenseApplicable { get; init; }
    public required bool LicenseRequired { get; init; }
    public required bool SupportsFontDiagnostics { get; init; }

    /// <summary>Creates the invariant descriptor used by a license-aware engine.</summary>
    public static ProductEngineCapabilities LicenseAware(
        string id,
        string sdk,
        string sdkVersion,
        bool licenseRequired = false,
        bool supportsFontDiagnostics = true) => new()
        {
            Id = id,
            Sdk = sdk,
            SdkVersion = sdkVersion,
            LicenseApplicable = true,
            LicenseRequired = licenseRequired,
            SupportsFontDiagnostics = supportsFontDiagnostics,
        };
}

/// <summary>Live-preview capabilities of one product.</summary>
public sealed record ProductPreviewCapabilities
{
    /// <summary>View selected when the caller requests automatic preview routing.</summary>
    public required string DefaultView { get; init; }

    /// <summary>Stable view identifiers accepted by this product.</summary>
    public required IReadOnlyList<string> Views { get; init; }

    /// <summary>Whether the product can run as a discoverable background preview.</summary>
    public required bool Background { get; init; }
}

/// <summary>Static review evidence capabilities of one product.</summary>
public sealed record ProductReviewCapabilities
{
    public required string DefaultView { get; init; }

    public required IReadOnlyList<string> Views { get; init; }

    public required bool VisualInspectionRequired { get; init; }

    /// <summary>Every check a review can report, ordered by code; <c>review --code</c> filters by them.</summary>
    public required IReadOnlyList<ReviewCheck> Checks { get; init; }
}

/// <summary>Resolved generic-routing contract for this distribution.</summary>
public sealed record RoutingCapabilities
{
    /// <summary>Explicit default product, or null when callers must choose.</summary>
    public string? DefaultProduct { get; init; }

    /// <summary>Where the default came from; null when none is declared.</summary>
    public string? DefaultProductSource { get; init; }

    /// <summary>Absolute shared recognizer budget in milliseconds.</summary>
    public required int TotalProbeMilliseconds { get; init; }

    /// <summary>Maximum bytes read once and shared by all recognizers.</summary>
    public required int TotalProbeBytes { get; init; }

    /// <summary>Maximum in-process recognizer concurrency.</summary>
    public required int MaxConcurrency { get; init; }

    /// <summary>Behavior when content cannot be proven.</summary>
    public required string IndeterminatePolicy { get; init; }

    /// <summary>Final extension/operation ownership after distribution overrides.</summary>
    public required IReadOnlyList<ResolvedRouteCapabilities> Routes { get; init; }
}

/// <summary>One final generic route.</summary>
public sealed record ResolvedRouteCapabilities
{
    public required string Extension { get; init; }
    public required string Product { get; init; }
    public required IReadOnlyList<string> Operations { get; init; }
    public required bool RequiresContentProbe { get; init; }
    public required string RecognizerStrategy { get; init; }
    public required int MaxProbeBytes { get; init; }
}

/// <summary>Resolved capabilities of one product format declaration.</summary>
public sealed record ProductFormatCapabilities
{
    public required string Id { get; init; }
    public required IReadOnlyList<string> Extensions { get; init; }
    public required IReadOnlyList<string> Aliases { get; init; }
    public required IReadOnlyList<string> Uses { get; init; }
    public required IReadOnlyList<string> Operations { get; init; }
    public required string DeclaredOwnership { get; init; }
    public string? FinalOwner { get; init; }
    public required bool GenericAvailable { get; init; }
    public required bool ExplicitAvailable { get; init; }
    public string? RecognizerStrategy { get; init; }
    public int? MaxProbeBytes { get; init; }
}

/// <summary>One command or subcommand in a product grammar.</summary>
public sealed record CommandCapabilities
{
    public required string Path { get; init; }
    public required string Name { get; init; }
    public required IReadOnlyList<string> Aliases { get; init; }
    public string? Description { get; init; }
    public required bool Hidden { get; init; }
    public required IReadOnlyList<CommandOptionCapabilities> Options { get; init; }
    public required IReadOnlyList<CommandArgumentCapabilities> Arguments { get; init; }
}

/// <summary>One option accepted by a command.</summary>
public sealed record CommandOptionCapabilities
{
    public required string Name { get; init; }
    public required IReadOnlyList<string> Aliases { get; init; }
    public required string Type { get; init; }
    public required int MinimumArity { get; init; }
    public required int MaximumArity { get; init; }
    public required bool Required { get; init; }
    public required bool Recursive { get; init; }
    public required bool HasDefault { get; init; }
    public string? Default { get; init; }
    public required IReadOnlyList<string> AllowedValues { get; init; }
    public required bool Secret { get; init; }
    public required string ValueSource { get; init; }
    public required string InputKind { get; init; }
    public string? Description { get; init; }
}

/// <summary>One positional argument accepted by a command.</summary>
public sealed record CommandArgumentCapabilities
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public required string InputKind { get; init; }
    public required string ValueSource { get; init; }
    public required bool Secret { get; init; }
    public required int MinimumArity { get; init; }
    public required int MaximumArity { get; init; }
    public required bool Required { get; init; }
    public required bool HasDefault { get; init; }
    public string? Default { get; init; }
    public required IReadOnlyList<string> AllowedValues { get; init; }
    public string? Description { get; init; }
}
