using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Host.Contracts;

/// <summary>
/// Result of <c>aspose-cli schema</c> without an id: the products and their commands and formats,
/// the platform commands, and the id of every document <c>aspose-cli schema &lt;id&gt;</c> prints.
/// </summary>
public sealed record SchemaCatalogResult() : ResultEnvelope("schema-catalog")
{
    /// <summary>CLI version without build metadata, e.g. <c>2.0.0</c>.</summary>
    [MinLength(1)]
    public required string CliVersion { get; init; }

    /// <summary>The products of this build, in the order the command line lists them.</summary>
    public required IReadOnlyList<CatalogProduct> Products { get; init; }

    /// <summary>
    /// The platform commands, such as <c>license</c> and <c>license install</c>: each one's words after
    /// the executable name, in ordinal order. A command that only groups others has no document.
    /// </summary>
    public required IReadOnlyList<CatalogCommand> Commands { get; init; }

    /// <summary>
    /// The document ids <c>aspose-cli schema &lt;id&gt;</c> prints, in ordinal order: a command's options
    /// (<c>&lt;command&gt;-command</c>), a result's JSON Schema (<c>&lt;command&gt;-result</c>), an object table
    /// (<c>ops</c>) and its directory (<c>ops/directory</c>), a product's formats, and the diagnostics and
    /// budgets. Each name the directory lists is a part of the table, <c>ops/&lt;name&gt;</c>.
    /// </summary>
    [UniqueItems]
    [MinItems(1)]
    public required IReadOnlyList<string> Ids { get; init; }
}

/// <summary>One product: its engine, its commands and the formats it reads and writes.</summary>
public sealed record CatalogProduct
{
    /// <summary>Product id, the command-line word, e.g. <c>cells</c>.</summary>
    [MinLength(1)]
    public required string Id { get; init; }

    /// <summary>What the product does.</summary>
    [MinLength(1)]
    public required string Description { get; init; }

    /// <summary>The engine's SDK package, e.g. <c>Aspose.Cells</c>.</summary>
    [MinLength(1)]
    public required string Engine { get; init; }

    /// <summary>The engine SDK package version.</summary>
    [MinLength(1)]
    public required string EngineVersion { get; init; }

    /// <summary>The product's commands, each one's words after the product id, in ordinal order.</summary>
    public required IReadOnlyList<CatalogCommand> Commands { get; init; }

    /// <summary>Format ids every command of the product that opens a document reads; the formats document names those only convert reads.</summary>
    public required IReadOnlyList<string> LoadFormats { get; init; }

    /// <summary>Format ids <c>convert --to</c> writes.</summary>
    public required IReadOnlyList<string> ConvertFormats { get; init; }

    /// <summary>Format ids <c>render --to</c> writes.</summary>
    public required IReadOnlyList<string> RenderFormats { get; init; }
}

/// <summary>One command and what it does.</summary>
public sealed record CatalogCommand
{
    /// <summary>The command's words, such as <c>convert</c> or <c>license install</c>.</summary>
    [MinLength(1)]
    public required string Name { get; init; }

    /// <summary>The command's help description.</summary>
    [MinLength(1)]
    public required string Description { get; init; }
}
