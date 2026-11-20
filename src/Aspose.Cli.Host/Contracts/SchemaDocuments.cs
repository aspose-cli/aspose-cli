using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Host.Contracts;

/// <summary>
/// A <c>&lt;command&gt;-command</c> document of <c>aspose-cli schema</c>: one command's options and
/// arguments, as the command line parses them.
/// </summary>
public sealed record CommandDocument() : ResultEnvelope("command")
{
    /// <summary>Full command path, e.g. <c>aspose-cli cells read</c>.</summary>
    [MinLength(1)]
    public required string Path { get; init; }

    /// <summary>The command's help description.</summary>
    public string? Description { get; init; }

    /// <summary>Options the command accepts, in ordinal order of their names.</summary>
    public required IReadOnlyList<CommandOption> Options { get; init; }

    /// <summary>Positional arguments the command accepts, in order.</summary>
    public required IReadOnlyList<CommandArgument> Arguments { get; init; }
}

/// <summary>One option accepted by a command.</summary>
public sealed record CommandOption
{
    /// <summary>The option's name, e.g. <c>--output-format</c>.</summary>
    public required string Name { get; init; }

    /// <summary>Other names the option answers to.</summary>
    public required IReadOnlyList<string> Aliases { get; init; }

    /// <summary>The option's value type, e.g. <c>string</c> or <c>boolean</c>.</summary>
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

    /// <summary>What the value names: a file or a JSON source; omitted when it names nothing to read.</summary>
    [AllowedValues("file", "json-source")]
    public string? InputKind { get; init; }

    /// <summary>The option's help description.</summary>
    public string? Description { get; init; }
}

/// <summary>One positional argument accepted by a command.</summary>
public sealed record CommandArgument
{
    /// <summary>The argument's name in help.</summary>
    public required string Name { get; init; }

    /// <summary>The argument's value type, e.g. <c>string</c>.</summary>
    public required string Type { get; init; }

    /// <summary>What the value names: a file or a JSON source; omitted when it names nothing to read.</summary>
    [AllowedValues("file", "json-source")]
    public string? InputKind { get; init; }

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

/// <summary>A product's <c>formats</c> document of <c>aspose-cli schema</c>: the formats the product declares.</summary>
public sealed record FormatListResult() : ResultEnvelope("format-list")
{
    /// <summary>Product id, e.g. <c>cells</c>.</summary>
    [MinLength(1)]
    public required string Product { get; init; }

    /// <summary>The product's formats, in ordinal order of their ids.</summary>
    public required IReadOnlyList<FormatInfo> Formats { get; init; }
}

/// <summary>One format a product opens, converts to or renders to.</summary>
public sealed record FormatInfo
{
    /// <summary>Format id, e.g. <c>xlsx</c>.</summary>
    [MinLength(1)]
    public required string Id { get; init; }

    /// <summary>File extensions of the format.</summary>
    public required IReadOnlyList<string> Extensions { get; init; }

    /// <summary>Other names the format id accepts.</summary>
    public required IReadOnlyList<string> Aliases { get; init; }

    /// <summary>What the product does with the format: <c>input</c>, <c>convert</c> or <c>render</c>.</summary>
    public required IReadOnlyList<string> Uses { get; init; }
}

/// <summary>The <c>diagnostics</c> document of <c>aspose-cli schema</c>: every error and warning code.</summary>
public sealed record DiagnosticListResult() : ResultEnvelope("diagnostic-list")
{
    /// <summary>The codes, in ordinal order.</summary>
    public required IReadOnlyList<DiagnosticInfo> Diagnostics { get; init; }
}

/// <summary>One error or warning code.</summary>
public sealed record DiagnosticInfo
{
    /// <summary>Stable SCREAMING_SNAKE_CASE code, e.g. <c>FILE_NOT_FOUND</c>.</summary>
    [Pattern("^[A-Z][A-Z0-9_]*$")]
    public required string Code { get; init; }

    /// <summary>What declares the code: <c>common</c> (the SDK), <c>host</c> or a product id.</summary>
    [MinLength(1)]
    public required string Owner { get; init; }

    /// <summary>Whether the code names an error or a warning.</summary>
    [AllowedValues("error", "warning")]
    public required string Severity { get; init; }

    /// <summary>Process exit code of an error; omitted for a warning.</summary>
    [Minimum(1)]
    [Maximum(9)]
    public int? ExitCode { get; init; }

    /// <summary>What kind of problem the code reports, such as <c>validation</c>.</summary>
    [MinLength(1)]
    public required string Category { get; init; }

    /// <summary>Id of the schema of the code's <c>details</c> object.</summary>
    [MinLength(1)]
    public required string DetailsSchemaId { get; init; }
}

/// <summary>The <c>budgets</c> document of <c>aspose-cli schema</c>: the resource budgets and their limits.</summary>
public sealed record BudgetListResult() : ResultEnvelope("budget-list")
{
    /// <summary>The budgets every command applies, in ordinal order of their kinds.</summary>
    public required IReadOnlyList<ResourceBudget> ResourceBudgets { get; init; }

    /// <summary>Each product's own budgets, in product order.</summary>
    public required IReadOnlyList<ProductBudgets> Products { get; init; }
}

/// <summary>The budgets one product applies to what it loads.</summary>
public sealed record ProductBudgets
{
    /// <summary>Product id, e.g. <c>cells</c>.</summary>
    [MinLength(1)]
    public required string Product { get; init; }

    /// <summary>The product's budgets.</summary>
    public required IReadOnlyList<ResourceBudget> ResourceBudgets { get; init; }
}
