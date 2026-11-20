using System.CommandLine;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Sdk;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Formats;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.Tables;

namespace Aspose.Cli.Host.Commands;

/// <summary>
/// What <c>aspose-cli schema</c> prints, built once from the executable command tree: without an
/// id the catalog, with one the document it names. A document is a command's options and
/// arguments, a product's formats, the diagnostics, the budgets, a JSON Schema, or one part of an
/// object table's schema.
/// </summary>
internal sealed class SchemaDirectory
{
    private const string CommandSuffix = "-command";
    private readonly HostSchemaCatalog _schemas;
    private readonly IReadOnlyDictionary<string, ResultEnvelope> _documents;

    private SchemaDirectory(
        HostSchemaCatalog schemas,
        IReadOnlyDictionary<string, ResultEnvelope> documents,
        SchemaCatalogResult catalog)
    {
        _schemas = schemas;
        _documents = documents;
        Catalog = catalog;
    }

    /// <summary>The catalog <c>aspose-cli schema</c> prints without an id.</summary>
    public SchemaCatalogResult Catalog { get; }

    public static SchemaDirectory Create(Command root, ProductCatalog catalog, HostSchemaCatalog schemas)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(schemas);
        IReadOnlyList<OwnedCommand> tree = CommandGrammar.Describe(root);
        var documents = new Dictionary<string, ResultEnvelope>(StringComparer.Ordinal);
        foreach (OwnedCommand owned in tree.Where(static owned => !owned.Group || Words(owned).Length == 0))
        {
            documents.Add(CommandId(owned), owned.Command);
        }

        foreach (ProductDefinition product in catalog.Products)
        {
            documents.Add(ResultEnvelope.CatalogId(product.Manifest.Id, "formats"), Formats(product));
        }

        documents.Add(ResultEnvelope.CatalogId("common", "diagnostics"), Diagnostics(catalog));
        documents.Add(ResultEnvelope.CatalogId("common", "budgets"), Budgets(catalog));
        string[] wholeIds = [.. schemas.Ids.Concat(documents.Keys).Order(StringComparer.Ordinal)];
        if (wholeIds.Zip(wholeIds.Skip(1)).FirstOrDefault(static pair => pair.First == pair.Second).First is { } shared)
        {
            throw new InvalidOperationException($"Document '{shared}' is published twice.");
        }

        // An object table's directory names its other parts; listing them here would load every
        // product's engine types, which no other document of the catalog needs.
        string[] ids =
        [
            .. wholeIds
                .Concat(catalog.Products
                    .Where(static product => product.Tables.Count > 0)
                    .Select(static product => $"{ResultEnvelope.CatalogId(product.Manifest.Id, DocumentTable.SchemaId)}/{TableSchema.DirectoryView}"))
                .Order(StringComparer.Ordinal),
        ];
        var result = new SchemaCatalogResult
        {
            CliVersion = VersionInfo.CliVersion,
            Products = [.. catalog.Products.Select(product => Product(product, tree))],
            Commands = [.. Commands(tree, product: null)],
            Ids = ids,
        };
        return new SchemaDirectory(schemas, documents, result);
    }

    /// <summary>The document <paramref name="id"/> names when it is a result, such as a command's options.</summary>
    public bool TryGetDocument(string id, out ResultEnvelope? document) =>
        _documents.TryGetValue(id, out document);

    /// <summary>The JSON Schema <paramref name="id"/> names, or one part of an object table's schema.</summary>
    /// <exception cref="CliException"><c>OPTION_INVALID</c>: no document has the id.</exception>
    public string ReadSchema(string id)
    {
        if (_schemas.TryRead(id, out string? document))
        {
            return document;
        }

        int slash = id.LastIndexOf('/');
        string whole = slash < 0 ? string.Empty : id[..slash];
        IReadOnlyList<string> parts = whole.Length == 0 ? [] : _schemas.GetOperations(whole);
        if (parts.Count > 0 && _schemas.TryReadOperation(whole, id[(slash + 1)..], out string? part))
        {
            return part;
        }

        // An unknown part of a table lists the table's parts; any other id the catalog's ids.
        IEnumerable<string> available = parts.Count > 0 ? parts.Select(name => $"{whole}/{name}") : Catalog.Ids;
        throw CliErrors.OptionInvalid("schema id", $"unknown schema id '{id}'", Mistake.Of(id, available));
    }

    private static CatalogProduct Product(ProductDefinition product, IReadOnlyList<OwnedCommand> tree)
    {
        string id = product.Manifest.Id;
        return new CatalogProduct
        {
            Id = id,
            Description = tree.Single(owned => owned.Product == id && Words(owned).Length == 1).Command.Description ?? string.Empty,
            Engine = product.Manifest.Engine.Sdk,
            EngineVersion = product.Manifest.Engine.SdkVersion,
            Commands = [.. Commands(tree, id)],
            LoadFormats = product.Formats.IdsFor(FormatUse.Input),
            ConvertFormats = product.Formats.IdsFor(FormatUse.Convert),
            RenderFormats = product.Formats.IdsFor(FormatUse.Render),
        };
    }

    /// <summary>
    /// The commands of a product, without the product's own command, or, for null, the platform
    /// commands below the executable's.
    /// </summary>
    private static IEnumerable<CatalogCommand> Commands(IReadOnlyList<OwnedCommand> tree, string? product) =>
        tree
            .Where(owned => owned.Product == product && Words(owned).Length > (product is null ? 0 : 1))
            .Select(owned => new CatalogCommand
            {
                Name = string.Join(' ', Words(owned).Skip(product is null ? 0 : 1)),
                Description = owned.Command.Description ?? string.Empty,
            });

    /// <summary>The words of a command's path after the executable name.</summary>
    private static string[] Words(OwnedCommand owned) => owned.Command.Path.Split(' ')[1..];

    /// <summary>
    /// The id of a command's document: <c>&lt;product&gt;/&lt;words&gt;-command</c> for a product's command,
    /// <c>common/&lt;words&gt;-command</c> for a platform command, and the executable's name for the root.
    /// </summary>
    private static string CommandId(OwnedCommand owned)
    {
        string[] words = Words(owned);
        string name = owned.Product is not null
            ? string.Join('-', words[1..])
            : words.Length == 0 ? DistributionInfo.CommandName : string.Join('-', words);
        return ResultEnvelope.CatalogId(owned.Product ?? "common", name + CommandSuffix);
    }

    private static FormatListResult Formats(ProductDefinition product) => new()
    {
        Product = product.Manifest.Id,
        Formats =
        [
            .. product.Formats
                .OrderBy(static format => format.Id, StringComparer.Ordinal)
                .Select(static format => new FormatInfo
                {
                    Id = format.Id,
                    Extensions = [.. format.Extensions.Select(static extension => extension.ToLowerInvariant()).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)],
                    Aliases = [.. format.Aliases.Order(StringComparer.Ordinal)],
                    Uses = [.. UseNames.Where(use => format.Uses.HasFlag(use.Use)).Select(static use => use.Name)],
                }),
        ],
    };

    /// <summary>The name the formats document gives each use, in the order it lists them.</summary>
    private static readonly (FormatUse Use, string Name)[] UseNames =
    [
        (FormatUse.Input, "input"),
        (FormatUse.ConvertInput, "convert-input"),
        (FormatUse.Convert, "convert"),
        (FormatUse.Render, "render"),
    ];

    private static DiagnosticListResult Diagnostics(ProductCatalog catalog) => new()
    {
        Diagnostics =
        [
            .. catalog.Diagnostics.All
                .OrderBy(static descriptor => descriptor.Code, StringComparer.Ordinal)
                .Select(static descriptor => new DiagnosticInfo
                {
                    Code = descriptor.Code,
                    Owner = descriptor.Owner,
                    Severity = descriptor.Severity.ToString().ToLowerInvariant(),
                    ExitCode = descriptor.ExitCode is null ? null : (int)descriptor.ExitCode.Value,
                    Category = descriptor.Category,
                    DetailsSchemaId = descriptor.DetailsSchemaId,
                }),
        ],
    };

    private static BudgetListResult Budgets(ProductCatalog catalog) => new()
    {
        ResourceBudgets = [.. ResourceBudgetDefaults.Global.OrderBy(static budget => budget.Kind, StringComparer.Ordinal)],
        Products =
        [
            .. catalog.Products.Select(static product => new ProductBudgets
            {
                Product = product.Manifest.Id,
                ResourceBudgets = product.Manifest.ResourceBudgets,
            }),
        ],
    };
}
