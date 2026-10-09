namespace Aspose.Cli.Sdk.Contracts;

/// <summary>
/// Machine-readable self-description of the CLI: which products, verbs,
/// formats and ops this build supports. Agents use it for runtime discovery
/// instead of hard-coding command lists.
/// </summary>
public sealed record CapabilitiesResult() : ResultEnvelope("capabilities", 2)
{
    /// <summary>CLI version, e.g. <c>0.2.0</c>.</summary>
    public required string CliVersion { get; init; }

    /// <summary>Root source revision embedded by the distribution build.</summary>
    [MinLength(1)]
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
    [AllowedValues(2)]
    public required int ResourceBudgetContractVersion { get; init; }

    /// <summary>Resolved common and compiled-product diagnostic catalog.</summary>
    public required IReadOnlyList<DiagnosticCapabilities> Diagnostics { get; init; }
}

/// <summary>One compiled product engine identity used for release provenance.</summary>
public sealed record EnginePinCapabilities
{
    /// <summary>Product id, e.g. <c>cells</c>.</summary>
    [MinLength(1)]
    public required string Product { get; init; }

    /// <summary>Engine package compiled for the product, e.g. <c>Aspose.Cells</c>.</summary>
    [MinLength(1)]
    public required string Engine { get; init; }

    /// <summary>Exact engine package version.</summary>
    [MinLength(1)]
    public required string Version { get; init; }
}

/// <summary>Machine-readable metadata for one stable CLI diagnostic.</summary>
public sealed record DiagnosticCapabilities
{
    /// <summary>Stable SCREAMING_SNAKE_CASE code, e.g. <c>FILE_NOT_FOUND</c>.</summary>
    [Pattern("^[A-Z][A-Z0-9_]*$")]
    public required string Code { get; init; }

    /// <summary>What declares the code: <c>common</c> or a product id.</summary>
    [MinLength(1)]
    public required string Owner { get; init; }

    /// <summary>Whether the code names an error or a warning.</summary>
    [AllowedValues("error", "warning")]
    public required string Severity { get; init; }

    /// <summary>Process exit code of an error; omitted for a warning.</summary>
    [Minimum(1)]
    [Maximum(9)]
    public int? ExitCode { get; init; }

    /// <summary>What kind of problem the code reports, such as <c>validation</c> or <c>verification</c>.</summary>
    [MinLength(1)]
    public required string Category { get; init; }

    /// <summary>Stable identifier of the message wording.</summary>
    [MinLength(1)]
    public required string MessageTemplateId { get; init; }

    /// <summary>Stable identifier of the hint wording.</summary>
    [MinLength(1)]
    public required string HintTemplateId { get; init; }

    /// <summary>Schema id of the code's <c>details</c> object, printable via <c>aspose-cli schema &lt;id&gt;</c>.</summary>
    [MinLength(1)]
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
    [Pattern("^[a-z][a-z0-9_-]*( [a-z][a-z0-9_-]*)*$")]
    public required string Command { get; init; }

    /// <summary>Product-owned schema id printable through <c>schema &lt;id&gt;</c>.</summary>
    [Pattern("^v2/[a-z][a-z0-9_-]*/[a-z][a-z0-9_/-]*$")]
    public required string InputSchema { get; init; }

    /// <summary>The command that prints one operation's schema, with <c>&lt;op&gt;</c> standing for its name.</summary>
    [Pattern(" schema v2/[a-z][a-z0-9_-]*/[a-z][a-z0-9_/-]* --operation <op>$")]
    public required string OperationSchema { get; init; }

    /// <summary>
    /// <c>sha256:</c> and the lowercase hex SHA-256 of the served input schema (UTF-8, <c>\n</c>
    /// line endings); it changes whenever the published schema text changes, descriptions
    /// included. It is computed once, when first read.
    /// </summary>
    [Pattern("^sha256:[0-9a-f]{64}$")]
    public string ContractFingerprint => Fingerprint.Value;

    /// <summary>Largest accepted number of operations in one document.</summary>
    [Minimum(1)]
    public required int MaximumOperationCount { get; init; }

    /// <summary>Operation names in published order.</summary>
    [UniqueItems]
    [MinItems(1)]
    [Pattern("^[a-z][a-z0-9_-]*$")]
    public required IReadOnlyList<string> Ops { get; init; }

    /// <summary>The fingerprint of the vocabulary's schema, computed once and shared with the schema.</summary>
    internal Lazy<string> Fingerprint { get; init; } = null!;
}

/// <summary>One discoverable default and hard maximum for a resource.</summary>
public sealed record ResourceBudgetCapabilities
{
    /// <summary>What the budget limits, such as <c>input-bytes</c>.</summary>
    [MinLength(1)]
    public required string Kind { get; init; }

    /// <summary>The limit applied when nothing overrides it.</summary>
    [Minimum(1)]
    public required long Default { get; init; }

    /// <summary>The hard safety maximum no override may exceed.</summary>
    [Minimum(1)]
    public required long Maximum { get; init; }

    /// <summary>The unit of <c>default</c> and <c>maximum</c>, such as <c>bytes</c>.</summary>
    [MinLength(1)]
    public required string Unit { get; init; }

    /// <summary>The command-line option that overrides the budget, when there is one.</summary>
    public string? Option { get; init; }

    /// <summary>The environment variable that overrides the budget, when there is one.</summary>
    public string? EnvironmentVariable { get; init; }

    /// <summary>When the budget is enforced, such as before or while the input is read.</summary>
    [MinLength(1)]
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
    /// <summary>Engine id, as <c>availableEngines</c> lists it.</summary>
    [MinLength(1)]
    public required string Id { get; init; }

    /// <summary>The engine's SDK package, e.g. <c>Aspose.Cells</c>.</summary>
    [MinLength(1)]
    public required string Sdk { get; init; }

    /// <summary>The engine SDK package version.</summary>
    [MinLength(1)]
    public required string SdkVersion { get; init; }

    /// <summary>Whether an Aspose license changes what the engine produces.</summary>
    public required bool LicenseApplicable { get; init; }

    /// <summary>Whether the engine refuses to run without a license.</summary>
    public required bool LicenseRequired { get; init; }

    /// <summary>Whether <c>doctor</c> and <c>fonts</c> report the fonts the engine resolves.</summary>
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
    /// <summary>View a review renders when the caller names none.</summary>
    public required string DefaultView { get; init; }

    /// <summary>Stable view identifiers a review accepts.</summary>
    public required IReadOnlyList<string> Views { get; init; }

    /// <summary>Whether the rendered evidence must be looked at, because the checks alone cannot judge it.</summary>
    public required bool VisualInspectionRequired { get; init; }

    /// <summary>Every check a review can report, ordered by code; <c>review --code</c> filters by them.</summary>
    public required IReadOnlyList<ReviewCheck> Checks { get; init; }
}

/// <summary>Resolved generic-routing contract for this distribution.</summary>
public sealed record RoutingCapabilities
{
    /// <summary>Explicit default product; omitted when callers must choose.</summary>
    public string? DefaultProduct { get; init; }

    /// <summary>Where the default came from; omitted when none is declared.</summary>
    public string? DefaultProductSource { get; init; }

    /// <summary>Absolute shared recognizer budget in milliseconds.</summary>
    [Minimum(1)]
    public required int TotalProbeMilliseconds { get; init; }

    /// <summary>Maximum bytes read once and shared by all recognizers.</summary>
    [Minimum(1)]
    public required int TotalProbeBytes { get; init; }

    /// <summary>Maximum in-process recognizer concurrency.</summary>
    [Minimum(1)]
    public required int MaxConcurrency { get; init; }

    /// <summary>Behavior when content cannot be proven.</summary>
    public required string IndeterminatePolicy { get; init; }

    /// <summary>Final extension/operation ownership after distribution overrides.</summary>
    public required IReadOnlyList<ResolvedRouteCapabilities> Routes { get; init; }
}

/// <summary>One final generic route.</summary>
public sealed record ResolvedRouteCapabilities
{
    /// <summary>File extension the route matches, e.g. <c>.xlsx</c>.</summary>
    public required string Extension { get; init; }

    /// <summary>Product that owns files with this extension.</summary>
    public required string Product { get; init; }

    /// <summary>Generic commands that route such files to the product.</summary>
    public required IReadOnlyList<string> Operations { get; init; }

    /// <summary>Whether the file's content is read to confirm its format before routing.</summary>
    public required bool RequiresContentProbe { get; init; }

    /// <summary>How the content is recognized.</summary>
    public required string RecognizerStrategy { get; init; }

    /// <summary>Most bytes the recognizer reads.</summary>
    [Minimum(1)]
    public required int MaxProbeBytes { get; init; }
}

/// <summary>Resolved capabilities of one product format declaration.</summary>
public sealed record ProductFormatCapabilities
{
    /// <summary>Format id, e.g. <c>xlsx</c>.</summary>
    public required string Id { get; init; }

    /// <summary>File extensions of the format.</summary>
    public required IReadOnlyList<string> Extensions { get; init; }

    /// <summary>Other names the format id accepts.</summary>
    public required IReadOnlyList<string> Aliases { get; init; }

    /// <summary>What the product does with the format, such as <c>load</c>, <c>convert</c> or <c>render</c>.</summary>
    public required IReadOnlyList<string> Uses { get; init; }

    /// <summary>Generic commands the format takes part in.</summary>
    public required IReadOnlyList<string> Operations { get; init; }

    /// <summary>Whether the product claims files of the format by default or only when it is named.</summary>
    [AllowedValues("explicit", "default")]
    public required string DeclaredOwnership { get; init; }

    /// <summary>Product that owns the format's files after distribution overrides; omitted when none does.</summary>
    public string? FinalOwner { get; init; }

    /// <summary>Whether generic commands route the format to this product.</summary>
    public required bool GenericAvailable { get; init; }

    /// <summary>Whether the product's own commands accept the format.</summary>
    public required bool ExplicitAvailable { get; init; }

    /// <summary>How the format's content is recognized, when it is.</summary>
    public string? RecognizerStrategy { get; init; }

    /// <summary>Most bytes the recognizer reads, when it reads any.</summary>
    [Minimum(1)]
    public int? MaxProbeBytes { get; init; }
}

/// <summary>One command or subcommand in a product grammar.</summary>
public sealed record CommandCapabilities
{
    /// <summary>Full command path, e.g. <c>cells query range</c>.</summary>
    public required string Path { get; init; }

    /// <summary>The command's own name, the last word of its path.</summary>
    public required string Name { get; init; }

    /// <summary>Other names the command answers to.</summary>
    public required IReadOnlyList<string> Aliases { get; init; }

    /// <summary>The command's help description.</summary>
    public string? Description { get; init; }

    /// <summary>Whether help leaves the command out.</summary>
    public required bool Hidden { get; init; }

    /// <summary>Options the command accepts, in help order.</summary>
    public required IReadOnlyList<CommandOptionCapabilities> Options { get; init; }

    /// <summary>Positional arguments the command accepts, in order.</summary>
    public required IReadOnlyList<CommandArgumentCapabilities> Arguments { get; init; }
}

/// <summary>One option accepted by a command.</summary>
public sealed record CommandOptionCapabilities
{
    /// <summary>The option's name, e.g. <c>--output</c>.</summary>
    public required string Name { get; init; }

    /// <summary>Other names the option answers to.</summary>
    public required IReadOnlyList<string> Aliases { get; init; }

    /// <summary>The option's value type, e.g. <c>string</c> or <c>bool</c>.</summary>
    public required string Type { get; init; }

    /// <summary>Fewest values the option takes.</summary>
    [Minimum(0)]
    public required int MinimumArity { get; init; }

    /// <summary>Most values the option takes.</summary>
    [Minimum(0)]
    public required int MaximumArity { get; init; }

    /// <summary>Whether the command needs the option.</summary>
    public required bool Required { get; init; }

    /// <summary>Whether the option applies to the command's subcommands too.</summary>
    public required bool Recursive { get; init; }

    /// <summary>Whether help leaves the option out; a hidden option is never suggested for a mistyped one.</summary>
    public required bool Hidden { get; init; }

    /// <summary>Whether the option has a default value.</summary>
    public required bool HasDefault { get; init; }

    /// <summary>The default value as text, when there is one.</summary>
    public string? Default { get; init; }

    /// <summary>The only values the option accepts; empty when it accepts any.</summary>
    public required IReadOnlyList<string> AllowedValues { get; init; }

    /// <summary>Whether the value is a secret that is never echoed.</summary>
    public required bool Secret { get; init; }

    /// <summary>Where the value comes from: the command line, the environment variable it names, or standard input.</summary>
    [AllowedValues("command-line", "environment-variable-name", "stdin")]
    public required string ValueSource { get; init; }

    /// <summary>What the value names: nothing to read, a file, or a JSON source.</summary>
    [AllowedValues("none", "file", "json-source")]
    public required string InputKind { get; init; }

    /// <summary>The option's help description.</summary>
    public string? Description { get; init; }
}

/// <summary>One positional argument accepted by a command.</summary>
public sealed record CommandArgumentCapabilities
{
    /// <summary>The argument's name in help.</summary>
    public required string Name { get; init; }

    /// <summary>The argument's value type, e.g. <c>string</c>.</summary>
    public required string Type { get; init; }

    /// <summary>What the value names: nothing to read, a file, or a JSON source.</summary>
    [AllowedValues("none", "file", "json-source")]
    public required string InputKind { get; init; }

    /// <summary>Where the value comes from: the command line, the environment variable it names, or standard input.</summary>
    [AllowedValues("command-line", "environment-variable-name", "stdin")]
    public required string ValueSource { get; init; }

    /// <summary>Whether the value is a secret that is never echoed.</summary>
    public required bool Secret { get; init; }

    /// <summary>Fewest values the argument takes.</summary>
    [Minimum(0)]
    public required int MinimumArity { get; init; }

    /// <summary>Most values the argument takes.</summary>
    [Minimum(0)]
    public required int MaximumArity { get; init; }

    /// <summary>Whether the command needs the argument.</summary>
    public required bool Required { get; init; }

    /// <summary>Whether the argument has a default value.</summary>
    public required bool HasDefault { get; init; }

    /// <summary>The default value as text, when there is one.</summary>
    public string? Default { get; init; }

    /// <summary>The only values the argument accepts; empty when it accepts any.</summary>
    public required IReadOnlyList<string> AllowedValues { get; init; }

    /// <summary>The argument's help description.</summary>
    public string? Description { get; init; }
}
