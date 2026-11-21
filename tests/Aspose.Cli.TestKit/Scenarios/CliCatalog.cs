using System.Collections.Concurrent;
using System.Text.Json.Nodes;

namespace Aspose.Cli.TestKit.Scenarios;

/// <summary>
/// The machine-readable contract of the CLI under test: its commands, options, products and
/// diagnostics, read once per test process from the live <c>schema</c> catalog and the documents it
/// lists, never from a hand-written list.
/// </summary>
public sealed class CliCatalog
{
    private const string CommandSuffix = "-command";
    private static readonly Lazy<CliCatalog> Live = new(static () => new CliCatalog());
    private readonly ConcurrentDictionary<string, Lazy<string>> _schemas = new(StringComparer.Ordinal);
    private readonly IReadOnlyDictionary<string, JsonArray> _formats;
    private readonly HashSet<string> _documents = new(StringComparer.Ordinal);
    private readonly Lazy<IReadOnlyList<string>> _parts;

    private CliCatalog()
    {
        _parts = new Lazy<IReadOnlyList<string>>(ReadParts);
        Catalog = JsonNode.Parse(SchemaText(string.Empty))!;
        string version = Ids.First()[..Ids.First().IndexOf('/', StringComparison.Ordinal)];
        string Id(string owner, string name) => $"{version}/{owner}/{name}";

        // Every document read below is a run of the CLI: read them four at a time first.
        string[] read =
        [
            .. Ids.Where(static id => id.EndsWith(CommandSuffix, StringComparison.Ordinal)
                || id.EndsWith("/formats", StringComparison.Ordinal) || id.EndsWith("/common/diagnostics", StringComparison.Ordinal)),
        ];
        Parallel.ForEach(read, new ParallelOptions { MaxDegreeOfParallelism = 4 }, id => SchemaText(id));

        // Each command with a document, and a node of its own for a command that only groups others.
        var nodes = new List<JsonNode>();
        foreach (JsonNode product in Catalog["products"]!.AsArray().Select(static product => product!))
        {
            string id = product["id"]!.GetValue<string>();
            nodes.Add(new JsonObject
            {
                ["words"] = id,
                ["description"] = product["description"]!.GetValue<string>(),
                ["options"] = new JsonArray(),
                ["arguments"] = new JsonArray(),
            });
            foreach (JsonNode command in product["commands"]!.AsArray().Select(static command => command!))
            {
                string name = command["name"]!.GetValue<string>();
                nodes.Add(Document(Id(id, name.Replace(' ', '-') + CommandSuffix), $"{id} {name}"));
            }
        }

        foreach (JsonNode command in Catalog["commands"]!.AsArray().Select(static command => command!))
        {
            string name = command["name"]!.GetValue<string>();
            string id = Id("common", name.Replace(' ', '-') + CommandSuffix);
            nodes.Add(Ids.Contains(id)
                ? Document(id, name)
                : new JsonObject
                {
                    ["words"] = name,
                    ["description"] = command["description"]!.GetValue<string>(),
                    ["options"] = new JsonArray(),
                    ["arguments"] = new JsonArray(),
                });
        }

        nodes.Add(Document(Id("common", "aspose-cli" + CommandSuffix), string.Empty));
        CommandNodes = [.. nodes.OrderBy(static node => node["words"]!.GetValue<string>(), StringComparer.Ordinal)];
        string[] words = [.. CommandNodes.Select(static node => node["words"]!.GetValue<string>())];
        string[] productIds = [.. Catalog["products"]!.AsArray().Select(static product => product!["id"]!.GetValue<string>())];
        Commands = [.. CommandNodes.Select(node => ReadCommand(node, words, productIds))];
        _formats = productIds.ToDictionary(
            static id => id,
            id => JsonNode.Parse(SchemaText(Id(id, "formats")))!["formats"]!.AsArray(),
            StringComparer.Ordinal);
        Products = [.. Catalog["products"]!.AsArray().Select(product => ReadProduct(product!, _formats[product!["id"]!.GetValue<string>()]))];
        _documents.UnionWith(productIds.Select(id => Id(id, "formats")));
        _documents.UnionWith([Id("common", "diagnostics"), Id("common", "budgets")]);
        DiagnosticNodes = [.. JsonNode.Parse(SchemaText(Id("common", "diagnostics")))!["diagnostics"]!.AsArray().Select(static node => node!)];
        Diagnostics = DiagnosticNodes.ToDictionary(
            static diagnostic => diagnostic["code"]!.GetValue<string>(),
            static diagnostic => new CliDiagnostic(
                diagnostic["exitCode"]?.GetValue<int>(),
                diagnostic["detailsSchemaId"]?.GetValue<string>(),
                diagnostic["severity"]?.GetValue<string>()),
            StringComparer.Ordinal);
    }

    /// <summary>The catalog of the CLI this test run executes.</summary>
    public static CliCatalog Current => Live.Value;

    /// <summary>The raw <c>schema</c> catalog.</summary>
    public JsonNode Catalog { get; }

    /// <summary>Every document id the catalog lists.</summary>
    public IReadOnlyList<string> Ids => [.. Catalog["ids"]!.AsArray().Select(static id => id!.GetValue<string>())];

    /// <summary>
    /// The ids of the parts of every object table: <c>&lt;ops&gt;/&lt;name&gt;</c> for each name its directory
    /// lists, the directory itself among them.
    /// </summary>
    public IReadOnlyList<string> PartIds => _parts.Value;

    /// <summary>
    /// The ids of the JSON Schemas, in the catalog's order: every id but those of the documents of
    /// commands, formats, diagnostics and budgets, and of the parts of an object table's schema.
    /// </summary>
    public IReadOnlyList<string> SchemaIds
    {
        get
        {
            HashSet<string> ids = [.. Ids];
            return [.. Ids.Where(id => !_documents.Contains(id) && !IsPart(id, ids))];
        }
    }

    /// <summary>
    /// The document of each command of <see cref="Commands"/>, at the same position, with the
    /// command's path words after the executable name in <c>words</c>; a command that only groups
    /// others has a node of its own with no options or arguments.
    /// </summary>
    public IReadOnlyList<JsonNode> CommandNodes { get; }

    /// <summary>Every command, in ordinal order of their words, the executable's first.</summary>
    public IReadOnlyList<CliCommand> Commands { get; }

    /// <summary>The products in their display order.</summary>
    public IReadOnlyList<CliProduct> Products { get; }

    /// <summary>The diagnostics document's entries, in its order.</summary>
    public IReadOnlyList<JsonNode> DiagnosticNodes { get; }

    /// <summary>The diagnostics catalog by code.</summary>
    public IReadOnlyDictionary<string, CliDiagnostic> Diagnostics { get; }

    /// <summary>The command with the given path words, such as <c>cells convert</c>.</summary>
    public CliCommand Command(string words) =>
        Commands.Single(command => command.Words == words);

    /// <summary>The product with the given id.</summary>
    public CliProduct Product(string id) => Products.Single(product => product.Id == id);

    /// <summary>The entries of a product's formats document.</summary>
    public IReadOnlyList<JsonNode> FormatNodes(string product) => [.. _formats[product].Select(static node => node!)];

    /// <summary>The raw text of a document the CLI prints with <c>aspose-cli schema &lt;id&gt;</c>; the catalog for an empty id.</summary>
    public string SchemaText(string id) =>
        _schemas.GetOrAdd(id, key => new Lazy<string>(() => ScenarioHost.Schema(key))).Value;

    /// <summary>
    /// The product whose <c>read</c> reopens a file: <paramref name="preferred"/>, the product that
    /// wrote it, when it loads the extension; otherwise the one product that loads it; null when
    /// none or several do.
    /// </summary>
    public string? ReaderOf(string path, string? preferred = null)
    {
        string format = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        bool Loads(string id) => Product(id).ReadFormats.Contains(format, StringComparer.OrdinalIgnoreCase);
        if (preferred is not null && Loads(preferred))
        {
            return preferred;
        }

        string[] readers = [.. Products.Select(static product => product.Id).Where(Loads)];
        return readers.Length == 1 ? readers[0] : null;
    }

    private IReadOnlyList<string> ReadParts() =>
    [
        .. Ids.Where(static id => id.EndsWith("/ops/directory", StringComparison.Ordinal)).SelectMany(directory =>
        {
            string ops = directory[..directory.LastIndexOf('/')];
            JsonNode listed = JsonNode.Parse(SchemaText(directory))!;
            return new[] { "directory" }
                .Concat(listed["kinds"]!.AsObject().Select(static kind => kind.Key))
                .Concat(listed["adds"]!.AsObject().Select(static add => "add." + add.Key))
                .Concat(listed["actions"]!.AsObject().Select(static action => "do." + action.Key))
                .Select(name => $"{ops}/{name}");
        }),
    ];

    /// <summary>Whether the id names one part of an object table's schema, such as <c>v3/pdf/ops/page</c>.</summary>
    private static bool IsPart(string id, HashSet<string> ids)
    {
        int slash = id.LastIndexOf('/');
        return slash > 0 && id[..slash].EndsWith("/ops", StringComparison.Ordinal) && ids.Contains(id[..slash]);
    }

    private JsonNode Document(string id, string words)
    {
        _documents.Add(id);
        JsonNode document = JsonNode.Parse(SchemaText(id))!;
        document["words"] = words;
        return document;
    }

    private static CliCommand ReadCommand(JsonNode command, string[] words, string[] productIds)
    {
        string own = command["words"]!.GetValue<string>();
        string prefix = own.Length == 0 ? string.Empty : own + " ";
        string[] subcommands =
        [
            .. words.Where(other => other.Length > prefix.Length && other.StartsWith(prefix, StringComparison.Ordinal) && !other[prefix.Length..].Contains(' ', StringComparison.Ordinal))
                .Select(other => other[prefix.Length..]),
        ];
        string? first = own.Split(' ')[0];
        return new CliCommand(
            own,
            productIds.Contains(first) ? first : null,
            [.. command["options"]!.AsArray().Select(static option => ReadOption(option!))],
            [.. (command["arguments"]?.AsArray() ?? []).Select(static argument => ReadArgument(argument!))],
            subcommands);
    }

    private static CliOption ReadOption(JsonNode option) => new(
        option["name"]!.GetValue<string>(),
        option["type"]!.GetValue<string>(),
        option["required"]!.GetValue<bool>(),
        false,
        option["secret"]!.GetValue<bool>(),
        option["valueSource"]!.GetValue<string>(),
        Strings(option["allowedValues"]),
        option["recursive"]!.GetValue<bool>(),
        option["maximumArity"]?.GetValue<int>() ?? 1,
        Strings(option["aliases"]),
        option["inputKind"]?.GetValue<string>());

    private static CliArgument ReadArgument(JsonNode argument) => new(
        argument["inputKind"]?.GetValue<string>(),
        argument["maximumArity"]!.GetValue<int>(),
        argument["required"]!.GetValue<bool>());

    /// <summary>
    /// A product of the catalog; its read formats, which some command opens, are its load formats
    /// in the catalog's order, then those its formats document marks <c>convert-input</c>.
    /// </summary>
    private static CliProduct ReadProduct(JsonNode product, JsonArray formats) => new(
        product["id"]!.GetValue<string>(),
        [
            .. Strings(product["loadFormats"]),
            .. formats
                .Where(static format => format!["uses"]!.AsArray().Any(static use => use!.GetValue<string>() == "convert-input"))
                .Select(static format => format!["id"]!.GetValue<string>()),
        ],
        Strings(product["convertFormats"]),
        Strings(product["renderFormats"]));

    private static string[] Strings(JsonNode? array) =>
        array is null ? [] : [.. array.AsArray().Select(static item => item!.GetValue<string>())];
}

/// <summary>One command of the catalog.</summary>
/// <param name="Words">The path after the executable name, such as <c>cells read</c>; empty for the root.</param>
/// <param name="Product">The product that owns the command, or null for a platform command.</param>
/// <param name="Options">The command's own options; the root lists the recursive global ones.</param>
/// <param name="Arguments">The command's arguments in order.</param>
/// <param name="Subcommands">The names of the commands directly below it.</param>
public sealed record CliCommand(
    string Words,
    string? Product,
    IReadOnlyList<CliOption> Options,
    IReadOnlyList<CliArgument> Arguments,
    IReadOnlyList<string> Subcommands)
{
    /// <summary>The path words as separate tokens.</summary>
    public string[] Tokens => Words.Length == 0 ? [] : Words.Split(' ');

    /// <summary>The last path word, such as <c>convert</c>; empty for the root.</summary>
    public string Verb => Words.Length == 0 ? string.Empty : Tokens[^1];

    /// <summary>The option with the given name, or null.</summary>
    public CliOption? Option(string name) => Options.FirstOrDefault(option => option.Name == name);

    /// <summary>Whether the command has an option with the given name.</summary>
    public bool Has(string name) => Option(name) is not null;

    public override string ToString() => Words.Length == 0 ? "aspose-cli" : Words;
}

/// <summary>
/// One option of a command; a hidden one is left out of help, and a recursive one
/// is taken by every command below its own. <see cref="MaximumArity"/> is the number of values it
/// takes, 0 for a flag; <see cref="Aliases"/> are its other spellings, such as <c>-q</c>.
/// <see cref="InputKind"/> says what its value names when the command reads it, such as
/// <c>file</c> or <c>json-source</c>.
/// </summary>
public sealed record CliOption(
    string Name,
    string Type,
    bool Required,
    bool Hidden,
    bool Secret,
    string ValueSource,
    IReadOnlyList<string> AllowedValues,
    bool Recursive,
    int MaximumArity,
    IReadOnlyList<string> Aliases,
    string? InputKind = null);

/// <summary>One positional argument of a command.</summary>
public sealed record CliArgument(string? InputKind, int MaximumArity, bool Required);

/// <summary>One product of the catalog.</summary>
public sealed record CliProduct(
    string Id,
    IReadOnlyList<string> ReadFormats,
    IReadOnlyList<string> ConvertFormats,
    IReadOnlyList<string> RenderFormats);

/// <summary>One diagnostic of the catalog; an error has an exit code, and <paramref name="Severity"/> is <c>error</c> or <c>warning</c>.</summary>
public sealed record CliDiagnostic(int? ExitCode, string? DetailsSchemaId, string? Severity);
