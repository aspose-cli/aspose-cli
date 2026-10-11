using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.TestKit.Scenarios;

namespace Aspose.Cli.Invariants.Tests;

/// <summary>One command of the capabilities document with the fields the command tree rules read.</summary>
/// <param name="Command">The catalog's reading of the command: words, product, subcommands.</param>
/// <param name="Options">The command's own options, as capabilities declares them.</param>
/// <param name="Arguments">The command's positional arguments in order.</param>
internal sealed record TreeCommand(CliCommand Command, IReadOnlyList<TreeOption> Options, IReadOnlyList<TreeArgument> Arguments)
{
    public string Words => Command.Words;

    public string? Product => Command.Product;

    public bool Hidden => Command.Hidden;

    /// <summary>The product verb, the second path word of a product command; empty otherwise.</summary>
    public string ProductVerb => Product is not null && Command.Tokens.Length > 1 ? Command.Tokens[1] : string.Empty;

    /// <summary>The last path word, such as <c>convert</c>; empty for the root.</summary>
    public string Verb => Command.Verb;

    public bool IsGroup => Command.Subcommands.Count > 0;

    public TreeOption? Option(string name) => Options.FirstOrDefault(option => option.Name == name);

    public bool Has(string name) => Option(name) is not null;

    public override string ToString() => Command.ToString();
}

/// <summary>One option of a command as capabilities declares it.</summary>
internal sealed record TreeOption(JsonNode Node)
{
    public string Name => Node["name"]!.GetValue<string>();

    public IReadOnlyList<string> Aliases => CommandTree.Strings(Node["aliases"]);

    public string Type => Node["type"]!.GetValue<string>();

    public int MaximumArity => Node["maximumArity"]!.GetValue<int>();

    public bool Required => Node["required"]!.GetValue<bool>();

    public bool Recursive => Node["recursive"]!.GetValue<bool>();

    public bool Hidden => Node["hidden"]!.GetValue<bool>();

    public bool HasDefault => Node["hasDefault"]!.GetValue<bool>();

    /// <summary>The published default, or null when there is none or it is null.</summary>
    public string? Default => HasDefault ? Node["default"]?.GetValue<string>() : null;

    public IReadOnlyList<string> AllowedValues => CommandTree.Strings(Node["allowedValues"]);

    public bool Secret => Node["secret"]!.GetValue<bool>();

    public string ValueSource => Node["valueSource"]!.GetValue<string>();

    /// <summary>What the value names, <c>file</c> or <c>json-source</c>; null when it names nothing to read.</summary>
    public string? InputKind => Node["inputKind"]?.GetValue<string>();

    public string Description => Node["description"]?.GetValue<string>() ?? string.Empty;

    public bool IsBoolean => Type == "boolean";
}

/// <summary>One positional argument of a command as capabilities declares it.</summary>
internal sealed record TreeArgument(JsonNode Node)
{
    public string Name => Node["name"]!.GetValue<string>();

    /// <summary>What the value names, <c>file</c> or <c>json-source</c>; null when it names nothing to read.</summary>
    public string? InputKind => Node["inputKind"]?.GetValue<string>();

    public int MaximumArity => Node["maximumArity"]!.GetValue<int>();

    public string Description => Node["description"]?.GetValue<string>() ?? string.Empty;
}

/// <summary>The command tree of the capabilities document, read once per catalog.</summary>
internal static partial class CommandTree
{
    /// <summary>The product verbs of C-R02, each with one meaning.</summary>
    public static readonly string[] ProductVerbs =
        ["inspect", "read", "search", "create", "edit", "convert", "render", "compare", "extract", "split", "merge", "sign", "validate"];

    /// <summary>Every command in the order capabilities lists them, hidden ones included.</summary>
    public static IReadOnlyList<TreeCommand> Of(CliCatalog catalog)
    {
        JsonArray nodes = catalog.Document["commands"]!.AsArray();
        return
        [
            .. catalog.Commands.Select((command, index) => new TreeCommand(
                command,
                [.. nodes[index]!["options"]!.AsArray().Select(static option => new TreeOption(option!))],
                [.. (nodes[index]!["arguments"]?.AsArray() ?? []).Select(static argument => new TreeArgument(argument!))])),
        ];
    }

    /// <summary>The product commands of one product, its group command included.</summary>
    public static IEnumerable<TreeCommand> OfProduct(CliCatalog catalog, string product) =>
        Of(catalog).Where(command => command.Product == product);

    /// <summary>Every format id the products declare.</summary>
    public static IReadOnlySet<string> FormatIds(CliCatalog catalog) =>
        catalog.Document["products"]!.AsArray()
            .SelectMany(static product => product!["formats"]!.AsArray())
            .Select(static format => format!["id"]!.GetValue<string>())
            .ToHashSet(StringComparer.Ordinal);

    public static string[] Strings(JsonNode? array) =>
        array is null ? [] : [.. array.AsArray().Select(static item => item!.GetValue<string>())];

    public static string List(IEnumerable<string> items) => string.Join(", ", items);

    /// <summary>Lowercase kebab case: lowercase letters and digits in words joined by single hyphens.</summary>
    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    public static partial Regex Kebab();
}
