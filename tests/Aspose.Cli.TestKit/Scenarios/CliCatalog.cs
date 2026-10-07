using System.Collections.Concurrent;
using System.Text.Json.Nodes;

namespace Aspose.Cli.TestKit.Scenarios;

/// <summary>
/// The machine-readable contract of the CLI under test: its commands, options, products and
/// diagnostics, read once per test process from the live <c>capabilities --output json</c>,
/// never from a hand-written list.
/// </summary>
public sealed class CliCatalog
{
    private static readonly Lazy<CliCatalog> Live = new(static () => new CliCatalog(ScenarioHost.Capabilities()));
    private readonly ConcurrentDictionary<string, Lazy<string>> _schemas = new(StringComparer.Ordinal);

    private CliCatalog(JsonNode document)
    {
        Document = document;
        string[] productIds = [.. document["products"]!.AsArray().Select(static product => product!["id"]!.GetValue<string>())];
        var paths = document["commands"]!.AsArray()
            .Select(static command => command!["path"]!.GetValue<string>())
            .ToArray();
        Commands = [.. document["commands"]!.AsArray().Select(command => ReadCommand(command!, paths, productIds))];
        Products = [.. document["products"]!.AsArray().Select(static product => ReadProduct(product!))];
        Diagnostics = document["diagnostics"]!.AsArray().ToDictionary(
            static diagnostic => diagnostic!["code"]!.GetValue<string>(),
            static diagnostic => new CliDiagnostic(
                diagnostic!["exitCode"]?.GetValue<int>(),
                diagnostic["detailsSchemaId"]?.GetValue<string>()),
            StringComparer.Ordinal);
        Routes = document["routing"]!["routes"]!.AsArray().ToDictionary(
            static route => route!["extension"]!.GetValue<string>(),
            static route => route!["product"]!.GetValue<string>(),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The catalog of the CLI this test run executes.</summary>
    public static CliCatalog Current => Live.Value;

    /// <summary>The raw capabilities document.</summary>
    public JsonNode Document { get; }

    /// <summary>Every command, hidden ones included, in the order capabilities lists them.</summary>
    public IReadOnlyList<CliCommand> Commands { get; }

    /// <summary>The products in their display order.</summary>
    public IReadOnlyList<CliProduct> Products { get; }

    /// <summary>The diagnostics catalog by code.</summary>
    public IReadOnlyDictionary<string, CliDiagnostic> Diagnostics { get; }

    /// <summary>The product that content routing assigns to each extension, such as <c>.pdf</c>.</summary>
    public IReadOnlyDictionary<string, string> Routes { get; }

    /// <summary>The command with the given path words, such as <c>cells convert</c>.</summary>
    public CliCommand Command(string words) =>
        Commands.Single(command => command.Words == words);

    /// <summary>The product with the given id.</summary>
    public CliProduct Product(string id) => Products.Single(product => product.Id == id);

    /// <summary>The raw text of a schema the CLI prints with <c>aspose-cli schema &lt;id&gt;</c>.</summary>
    public string SchemaText(string id) =>
        _schemas.GetOrAdd(id, key => new Lazy<string>(() => ScenarioHost.Schema(key))).Value;

    /// <summary>
    /// The product whose <c>inspect</c> reopens a file: the product routing assigns to its
    /// extension, else <paramref name="preferred"/>, the product that wrote it, when it loads the
    /// extension; null otherwise. Another product that merely declares an unrouted extension is
    /// never asked, so its refusal is not reported against the product that wrote the file.
    /// </summary>
    public string? ReaderOf(string path, string? preferred = null)
    {
        string extension = Path.GetExtension(path);
        string format = extension.TrimStart('.').ToLowerInvariant();
        bool Loads(string id) => Product(id).LoadFormats.Contains(format, StringComparer.OrdinalIgnoreCase);
        if (Routes.TryGetValue(extension, out string? routed) && Loads(routed))
        {
            return routed;
        }
        return preferred is not null && Loads(preferred) ? preferred : null;
    }

    private static CliCommand ReadCommand(JsonNode command, string[] paths, string[] productIds)
    {
        string path = command["path"]!.GetValue<string>();
        string words = path.Contains(' ', StringComparison.Ordinal) ? path[(path.IndexOf(' ', StringComparison.Ordinal) + 1)..] : string.Empty;
        string prefix = path + " ";
        string[] subcommands =
        [
            .. paths.Where(other => other.StartsWith(prefix, StringComparison.Ordinal) && !other[prefix.Length..].Contains(' ', StringComparison.Ordinal))
                .Select(other => other[prefix.Length..]),
        ];
        string? first = words.Split(' ')[0];
        return new CliCommand(
            words,
            command["hidden"]!.GetValue<bool>(),
            productIds.Contains(first) ? first : null,
            [.. command["options"]!.AsArray().Select(static option => ReadOption(option!))],
            [.. (command["arguments"]?.AsArray() ?? []).Select(static argument => ReadArgument(argument!))],
            subcommands);
    }

    private static CliOption ReadOption(JsonNode option) => new(
        option["name"]!.GetValue<string>(),
        option["type"]!.GetValue<string>(),
        option["required"]!.GetValue<bool>(),
        option["hidden"]!.GetValue<bool>(),
        option["secret"]!.GetValue<bool>(),
        option["valueSource"]!.GetValue<string>(),
        Strings(option["allowedValues"]));

    private static CliArgument ReadArgument(JsonNode argument) => new(
        argument["inputKind"]!.GetValue<string>(),
        argument["maximumArity"]!.GetValue<int>(),
        argument["required"]!.GetValue<bool>());

    private static CliProduct ReadProduct(JsonNode product)
    {
        JsonNode? operations = product["operations"]?.AsArray().FirstOrDefault();
        return new CliProduct(
            product["id"]!.GetValue<string>(),
            Strings(product["loadFormats"]),
            Strings(product["convertFormats"]),
            Strings(product["renderFormats"]),
            operations?["inputSchema"]?.GetValue<string>(),
            Strings(operations?["ops"]));
    }

    private static string[] Strings(JsonNode? array) =>
        array is null ? [] : [.. array.AsArray().Select(static item => item!.GetValue<string>())];
}

/// <summary>One command of the catalog.</summary>
/// <param name="Words">The path after the executable name, such as <c>cells query range</c>; empty for the root.</param>
/// <param name="Hidden">Whether help hides the command.</param>
/// <param name="Product">The product that owns the command, or null for a platform command.</param>
/// <param name="Options">The command's own options; the root lists the recursive global ones.</param>
/// <param name="Arguments">The command's arguments in order.</param>
/// <param name="Subcommands">The names of the commands directly below it.</param>
public sealed record CliCommand(
    string Words,
    bool Hidden,
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

/// <summary>One option of a command; a hidden one is left out of help and suggestions.</summary>
public sealed record CliOption(
    string Name,
    string Type,
    bool Required,
    bool Hidden,
    bool Secret,
    string ValueSource,
    IReadOnlyList<string> AllowedValues);

/// <summary>One positional argument of a command.</summary>
public sealed record CliArgument(string InputKind, int MaximumArity, bool Required);

/// <summary>One product of the catalog.</summary>
public sealed record CliProduct(
    string Id,
    IReadOnlyList<string> LoadFormats,
    IReadOnlyList<string> ConvertFormats,
    IReadOnlyList<string> RenderFormats,
    string? OpsSchemaId,
    IReadOnlyList<string> Ops);

/// <summary>One diagnostic of the catalog; an error has an exit code.</summary>
public sealed record CliDiagnostic(int? ExitCode, string? DetailsSchemaId);
