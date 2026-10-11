using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.TestKit.Scenarios;

namespace Aspose.Cli.Invariants.Tests;

// The command tree and its options, as capabilities declares them. Every part here reads the
// catalog only; the parts that run commands are in the other rule files.
internal static partial class ContractRules
{
    // ----- C-R01: product commands are `<product> <verb>`, service commands `<group> <verb>` ----

    private static IEnumerable<InvariantCase> R01(CliCatalog catalog)
    {
        IReadOnlyList<TreeCommand> tree = CommandTree.Of(catalog);
        foreach (string product in catalog.Products.Select(static product => product.Id))
        {
            yield return Check("C-R01", product, () => tree
                .Where(command => command.Product == product)
                .SelectMany(static command => command.Command.Tokens.Length > 2
                    ? [$"{command} is a third command level; a product command is `aspose-cli <product> <verb> <file...>`."]
                    : command.Command.Tokens.Length == 2 && command.IsGroup
                        ? [$"{command} is a command group below the product; its subcommands are a third level."]
                        : Array.Empty<string>()));
        }
        yield return Check("C-R01", "host", () => tree
            .Where(static command => command.Product is null && command.Command.Tokens.Length > 2)
            .Select(static command => $"{command} is a third command level; a service command is `aspose-cli <group> <verb>`."));
    }

    // ----- C-R02: the product verbs are one fixed list ------------------------------------------

    private static IEnumerable<InvariantCase> R02(CliCatalog catalog)
    {
        foreach (string product in catalog.Products.Select(static product => product.Id))
        {
            yield return Check("C-R02", product, () => CommandTree.OfProduct(catalog, product)
                .Select(static command => command.ProductVerb)
                .Where(static verb => verb.Length > 0 && !CommandTree.ProductVerbs.Contains(verb, StringComparer.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .Select(verb => $"{product} {verb}: '{verb}' is not a product verb ({CommandTree.List(CommandTree.ProductVerbs)})."));
        }
    }

    // ----- C-R03 (declared): a command group has no arguments and no options of its own --------

    /// <summary>
    /// The options a command group may declare besides the global ones every command inherits,
    /// each with its reason.
    /// </summary>
    private static readonly (string Command, string Option, string Reason)[] GroupOptionExemptions =
    [
        ("aspose-cli", "--version", "C-R35 requires the root's --version: one line, or the version schema with JSON output."),
    ];

    private static IEnumerable<InvariantCase> R03Declared(CliCatalog catalog)
    {
        foreach (TreeCommand group in CommandTree.Of(catalog).Where(static command => command.IsGroup))
        {
            yield return Check("C-R03", $"{group} declared", () =>
            [
                .. group.Arguments.Select(argument => $"{group} has subcommands but takes the argument <{argument.Name}>."),
                .. group.Options
                    .Where(option => !option.Recursive && !GroupOptionExemptions.Any(exemption =>
                        exemption.Command == group.ToString() && exemption.Option == option.Name))
                    .Select(option => $"{group} has subcommands but declares its own option {option.Name}."),
                .. GroupOptionExemptions
                    .Where(exemption => exemption.Command == group.ToString()
                        && !group.Options.Any(option => !option.Recursive && option.Name == exemption.Option))
                    .Select(exemption => $"{group}: the exemption for {exemption.Option} no longer matches an option; delete it from {nameof(GroupOptionExemptions)}."),
            ]);
        }
    }

    // ----- C-R04: hidden commands start with __; public text never names a hidden option -------

    private static IEnumerable<InvariantCase> R04(CliCatalog catalog)
    {
        IReadOnlyList<TreeCommand> tree = CommandTree.Of(catalog);
        foreach (TreeCommand command in tree.Where(static command => command.Hidden || command.Verb.StartsWith('_')))
        {
            yield return Check("C-R04", command.ToString(), () => command.Hidden == command.Verb.StartsWith("__", StringComparison.Ordinal)
                ? []
                : [command.Hidden
                    ? $"{command} is hidden, so its name starts with '__'."
                    : $"{command} starts with '__', so it is hidden."]);
        }
        foreach (string hidden in tree.SelectMany(static command => command.Options).Where(static option => option.Hidden)
            .Select(static option => option.Name).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            var mention = new Regex($@"(?<![\w-]){Regex.Escape(hidden)}(?![\w-])", RegexOptions.CultureInvariant);
            yield return Check("C-R04", hidden, () => PublicTexts(catalog, tree)
                .Where(text => mention.IsMatch(text.Text))
                .Select(text => $"{text.Where} names the hidden option {hidden}.")
                .Distinct(StringComparer.Ordinal));
        }
    }

    /// <summary>
    /// The text capabilities publishes for people and agents: the descriptions of the visible
    /// commands and of their visible options and arguments, which help prints too. Hints, schema
    /// descriptions and Skills are outside capabilities, so this part does not read them.
    /// </summary>
    private static IEnumerable<(string Where, string Text)> PublicTexts(CliCatalog catalog, IReadOnlyList<TreeCommand> tree)
    {
        foreach ((TreeCommand command, JsonNode node) in tree.Zip(catalog.Document["commands"]!.AsArray().Select(static node => node!)))
        {
            if (command.Hidden)
            {
                continue;
            }
            yield return ($"The description of {command}", node["description"]?.GetValue<string>() ?? string.Empty);
            foreach (TreeOption option in command.Options.Where(static option => !option.Hidden))
            {
                yield return ($"The description of {command} {option.Name}", option.Description);
            }
            foreach (TreeArgument argument in command.Arguments)
            {
                yield return ($"The description of {command} <{argument.Name}>", argument.Description);
            }
        }
    }

    // ----- C-R05: product arguments are file, left, right or files; one input file at most -----

    private static readonly string[] ArgumentNames = ["file", "left", "right", "files"];

    private static IEnumerable<InvariantCase> R05(CliCatalog catalog)
    {
        foreach (string product in catalog.Products.Select(static product => product.Id))
        {
            yield return Check("C-R05", product, () => CommandTree.OfProduct(catalog, product).SelectMany(static command =>
            {
                IEnumerable<string> names = command.Arguments
                    .Where(static argument => !ArgumentNames.Contains(argument.Name, StringComparer.Ordinal))
                    .Select(argument => $"{command} <{argument.Name}>: a product argument is named {CommandTree.List(ArgumentNames)}.");
                int inputs = command.Arguments.Where(static argument => argument.InputKind == "file").Sum(static argument => argument.MaximumArity);
                return inputs > 1 && command.ProductVerb is not ("compare" or "merge")
                    ? names.Append($"{command} takes {inputs} input files; only compare and merge take more than one.")
                    : names;
            }));
        }
    }

    // ----- C-R06: long names are lowercase kebab case; the short names are -o, -q, -v, -h -------

    private static readonly Dictionary<string, string> ShortNames = new(StringComparer.Ordinal)
    {
        ["-o"] = "--out",
        ["-q"] = "--quiet",
        ["-v"] = "--verbose",
        ["-h"] = "--help",
    };

    private static IEnumerable<InvariantCase> R06(CliCatalog catalog)
    {
        foreach (TreeCommand command in CommandTree.Of(catalog).Where(static command => command.Options.Count > 0))
        {
            yield return Check("C-R06", command.ToString(), () => command.Options.SelectMany(static option =>
            {
                var problems = new List<string>();
                if (!option.Name.StartsWith("--", StringComparison.Ordinal) || !CommandTree.Kebab().IsMatch(option.Name[2..]))
                {
                    problems.Add($"{option.Name} is not a lowercase kebab-case long name.");
                }
                problems.AddRange(option.Aliases
                    .Where(alias => !ShortNames.TryGetValue(alias, out string? owner) || owner != option.Name)
                    .Select(alias => $"{option.Name} has the short name {alias}; the only short names are -o (--out), -q (--quiet), -v (--verbose) and -h (--help)."));
                return problems;
            }));
        }
    }

    // ----- C-R07: same-name options agree ------------------------------------------------------

    /// <summary>
    /// C-R07 is checked by <see cref="SameNameOptions"/>, whose cases (invariant
    /// <see cref="InvariantCases.SameNameOption"/>, which <see cref="InvariantCases.RuleOf"/> maps to
    /// C-R07) compare every aspect of every same-name option; this part adds none, so no
    /// comparison is made in two places.
    /// </summary>
    private static IEnumerable<InvariantCase> R07(CliCatalog catalog)
    {
        yield break;
    }

    // ----- C-R08: one concept, one option name --------------------------------------------------

    /// <summary>Names no command may declare, each with the vocabulary name of its concept.</summary>
    private static readonly (string Name, bool Prefix, string Instead)[] BannedNames =
    [
        ("--output", false, "the output format is --output-format, one output file --out"),
        ("--mode", false, "name what the value chooses: --content, --scope or --detail"),
        ("--compare", false, "what a compare compares is --content"),
        ("--preview", true, "a sample is --sample or --sample-rows"),
        ("--all-", true, "a selection takes the value all, such as --pages all"),
        ("--markdown", false, "content read from another file is --from-<format>"),
        ("--text", false, "content read from another file is --from-<format>"),
        ("--blank", false, "a create without --from-<format> or --template is blank"),
        ("--size", false, "a page size is --page-size, a slide size --slide-size"),
        ("--by-bookmarks", false, "a split strategy is --by"),
        ("--open", false, "a boolean option has a positive name for its default (C-R09)"),
        ("--name-template", false, "output names follow C-R12"),
    ];

    /// <summary>
    /// The concepts of the vocabulary that capabilities shows a command has, each with the one
    /// name it must use. The rest (the selection options, --content against --scope, --sample,
    /// --skip, --page-size, the split options) need the meaning of an option and stay guidance.
    /// </summary>
    private static readonly (string Concept, Func<TreeCommand, IEnumerable<string>> Violations)[] Concepts =
    [
        ("output format", static command => command.Words.Length > 0 ? [] :
            command.Option("--output-format") is not { } format
                ? ["aspose-cli: the output format option is --output-format."]
                : format.Aliases.Count > 0
                    ? [$"aspose-cli --output-format: the output format has no short name, not {CommandTree.List(format.Aliases)}."]
                    : []),
        ("output file", static command => command.Options
            .Where(static option => option.Name.StartsWith("--out", StringComparison.Ordinal) && option.Name is not ("--out" or "--out-dir" or "--output-format")
                && !IsBanned(option.Name))
            .Select(option => $"{command} {option.Name}: one output file is --out (-o), an output directory --out-dir.")),
        ("target format", static command => command.ProductVerb is "convert" or "render" && !command.Has("--to")
            ? [$"{command}: the target format is --to."]
            : []),
        ("content from a file", static command => command.ProductVerb != "create" ? [] : command.Options
            .Where(static option => option.InputKind == "file" && option.Name != "--template" && !option.Name.StartsWith("--from-", StringComparison.Ordinal)
                && !IsBanned(option.Name))
            .Select(option => $"{command} {option.Name}: content read from another file is --from-<format>, a template --template.")),
        ("inspect sections", static command => command.ProductVerb == "inspect" && !command.Has("--detail")
            ? [$"{command}: the sections an inspect adds are --detail."]
            : []),
        ("extracted parts", static command => command.ProductVerb == "extract" && !command.Has("--what")
            ? [$"{command}: the kind of part an extract writes is --what."]
            : []),
    ];

    /// <summary>Whether a name is banned; a concept does not report a banned name twice.</summary>
    private static bool IsBanned(string name) =>
        BannedNames.Any(banned => banned.Prefix ? name.StartsWith(banned.Name, StringComparison.Ordinal) : name == banned.Name);

    private static IEnumerable<InvariantCase> R08(CliCatalog catalog)
    {
        foreach (TreeCommand command in CommandTree.Of(catalog).Where(static command => command.Options.Count > 0))
        {
            yield return Check("C-R08", command.ToString(), () =>
            [
                .. command.Options.SelectMany(option => BannedNames
                    .Where(banned => banned.Prefix ? option.Name.StartsWith(banned.Name, StringComparison.Ordinal) : option.Name == banned.Name)
                    .Select(banned => $"{command} {option.Name}: a banned name; {banned.Instead}.")),
                .. Concepts.SelectMany(concept => concept.Violations(command)),
            ]);
        }
    }

    // ----- C-R09: boolean options have positive names ---------------------------------------------

    private static IEnumerable<InvariantCase> R09(CliCatalog catalog)
    {
        // Whether x itself is on by default, which alone justifies --no-x, is in the meaning of
        // the option, not in capabilities (the --no-x switch defaults to false either way); only
        // the --x and --no-x pair is checked here.
        IReadOnlyList<TreeCommand> tree = CommandTree.Of(catalog);
        foreach (string negative in tree.SelectMany(static command => command.Options).Where(IsNegative)
            .Select(static option => option.Name).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            string positive = "--" + negative["--no-".Length..];
            yield return Check("C-R09", negative, () => tree
                .Where(command => command.Has(positive))
                .Select(command => $"{command} declares {positive} while {CommandTree.List(tree.Where(other => other.Has(negative)).Select(static other => other.ToString()))} declare {negative}."));
        }
    }

    private static bool IsNegative(TreeOption option) => option.IsBoolean && option.Name.StartsWith("--no-", StringComparison.Ordinal);

    // ----- C-R10 (declared): every secret has -env and -stdin forms --------------------------------

    private static IEnumerable<InvariantCase> R10Declared(CliCatalog catalog)
    {
        foreach (TreeCommand command in CommandTree.Of(catalog).Where(static command => command.Options.Any(static option => option.Secret)))
        {
            yield return Check("C-R10", $"{command} declared", () => command.Options
                .Where(static option => option.Secret)
                .GroupBy(static option => SecretName(option.Name), StringComparer.Ordinal)
                .OrderBy(static group => group.Key, StringComparer.Ordinal)
                .SelectMany(group =>
                {
                    string[] forms = [.. group.Select(static option => option.Name)];
                    return new[] { group.Key + "-env", group.Key + "-stdin" }
                        .Where(form => !forms.Contains(form, StringComparer.Ordinal))
                        .Select(form => $"{command} {group.Key}: the secret has no {form}.")
                        .Concat(group
                            .Where(option => option.Name == group.Key && !option.Description.Contains("Discouraged", StringComparison.Ordinal))
                            .Select(option => $"{command} {option.Name}: the plain secret's description does not mark it Discouraged."));
                }));
        }
    }

    /// <summary>The secret one option carries: its name without the <c>-env</c> or <c>-stdin</c> source.</summary>
    private static string SecretName(string name) =>
        name.EndsWith("-env", StringComparison.Ordinal) ? name[..^"-env".Length]
        : name.EndsWith("-stdin", StringComparison.Ordinal) ? name[..^"-stdin".Length]
        : name;

    // ----- C-R11 (declared): every command that writes files has --overwrite and --dry-run --------

    /// <summary>The writing commands that have no <c>--overwrite</c>, each with its reason.</summary>
    private static readonly (string Command, string Reason)[] OverwriteExemptions =
    [
        ("license install", "An installation puts the license in place and replaces the installed one; that is what install means."),
        ("skill install", "An installation puts the Skill in place and replaces the version this CLI installed; that is what install means."),
        ("update install", "An installation puts the release in place and replaces the installed one; that is what install means."),
    ];

    private static IEnumerable<InvariantCase> R11Declared(CliCatalog catalog)
    {
        foreach (TreeCommand command in CommandTree.Of(catalog).Where(static command => !command.Hidden && Writes(command)))
        {
            // A removal replaces nothing, so it needs --dry-run but no --overwrite.
            bool exempt = OverwriteExemptions.Any(exemption => exemption.Command == command.ToString());
            string[] required = RemovesInstalled(command) || exempt ? ["--dry-run"] : ["--overwrite", "--dry-run"];
            yield return Check("C-R11", $"{command} declared", () =>
            [
                .. required
                    .Where(name => !command.Has(name))
                    .Select(name => $"{command} writes files but has no {name}."),
                .. exempt && command.Has("--overwrite")
                    ? [$"{command}: the --overwrite exemption no longer matches; delete it from {nameof(OverwriteExemptions)}."]
                    : Array.Empty<string>(),
            ]);
        }

        string[] commands = [.. CommandTree.Of(catalog).Select(static command => command.ToString())];
        foreach ((string command, _) in OverwriteExemptions.Where(exemption => !commands.Contains(exemption.Command)))
        {
            yield return Check("C-R11", $"{command} exemption", () =>
                [$"{command} no longer exists; delete its exemption from {nameof(OverwriteExemptions)}."]);
        }
    }

    /// <summary>
    /// A command that writes files: it names an output (<c>--out</c>, <c>--out-dir</c>), creates
    /// the file its argument names (a product's <c>create</c>), or installs into or removes from
    /// an installation directory (a service's <c>install</c> or <c>remove</c>). C-R11 holds each to
    /// <c>--dry-run</c>, declared here and run in <c>OperationBehaviorRules.cs</c>.
    /// </summary>
    internal static bool Writes(TreeCommand command) =>
        command.Has("--out") || command.Has("--out-dir") || command.ProductVerb == "create" || command.Product is null && command.Verb is "install" or "remove";

    private static bool RemovesInstalled(TreeCommand command) => command.Product is null && command.Verb == "remove";

    // ----- C-R12 (declared): --out or --out-dir, never both ---------------------------------------

    private static IEnumerable<InvariantCase> R12Declared(CliCatalog catalog)
    {
        foreach (TreeCommand command in CommandTree.Of(catalog).Where(static command => command.Has("--out") || command.Has("--out-dir")))
        {
            yield return Check("C-R12", $"{command} declared", () => command.Has("--out") && command.Has("--out-dir")
                ? [$"{command} has both --out and --out-dir."]
                : []);
        }
    }

    // ----- C-R13 (declared): --to is optional on convert and render ----------------------------------

    private static IEnumerable<InvariantCase> R13Declared(CliCatalog catalog)
    {
        foreach (TreeCommand command in CommandTree.Of(catalog).Where(static command => command.ProductVerb is "convert" or "render"))
        {
            yield return Check("C-R13", $"{command} declared", () => command.Option("--to") is { Required: true }
                ? [$"{command} --to is required; the --out extension implies it."]
                : []);
        }
    }

    // ----- C-R15 (declared): every read and search has a --max-<unit> budget --------------------

    private static IEnumerable<InvariantCase> R15Declared(CliCatalog catalog)
    {
        foreach (TreeCommand command in CommandTree.Of(catalog).Where(static command => !command.IsGroup && IsReadOrSearch(command)))
        {
            yield return Check("C-R15", $"{command} declared", () => command.Options.Any(static option => option.Name.StartsWith("--max-", StringComparison.Ordinal))
                ? []
                : [$"{command} reads content but has no --max-<unit> budget."]);
        }
    }

    /// <summary>
    /// A product read or search: the verbs <c>read</c> and <c>search</c> and, while they remain,
    /// the <c>query &lt;noun&gt;</c> commands that C-R01 and C-R02 fold into them.
    /// </summary>
    private static bool IsReadOrSearch(TreeCommand command) =>
        command.Product is not null && (command.ProductVerb is "read" or "search" or "query");

    // ----- C-R17 (options): allowed values are lowercase kebab case ------------------------------

    /// <summary>The option whose values are check codes, which keep their own spelling.</summary>
    private const string CheckCodeOption = "--code";

    private static readonly string[] BooleanValues = ["false", "true"];

    private static IEnumerable<InvariantCase> R17Options(CliCatalog catalog)
    {
        IReadOnlySet<string> formats = CommandTree.FormatIds(catalog);
        IReadOnlyList<TreeCommand> tree = CommandTree.Of(catalog);
        foreach (TreeCommand command in tree.Where(static command => command.Options.Any(static option => option.IsBoolean)))
        {
            yield return Check("C-R17", $"{command} booleans", () =>
            {
                string[] booleans =
                [
                    .. command.Options.Where(static option => option.IsBoolean && !option.AllowedValues.SequenceEqual(BooleanValues, StringComparer.Ordinal))
                        .Select(static option => option.Name),
                ];
                return booleans.Length == 0 ? [] : [$"{command}: boolean options that do not publish false, true: {CommandTree.List(booleans)}."];
            });
        }
        foreach (TreeCommand command in tree.Where(static command => command.Options.Any(static option => !option.IsBoolean && option.AllowedValues.Count > 0)))
        {
            yield return Check("C-R17", $"{command} values", () => command.Options
                .Where(static option => !option.IsBoolean && option.Name != CheckCodeOption)
                .Select(option => (option.Name, Values: option.AllowedValues.Where(value => !formats.Contains(value) && !CommandTree.Kebab().IsMatch(value)).ToArray()))
                .Where(static option => option.Values.Length > 0)
                .Select(option => $"{command} {option.Name}: values not in lowercase kebab case: {CommandTree.List(option.Values)}."));
        }
    }

    // ----- C-R22 (declared): one operation-count limit, 10000 ------------------------------------

    private const int MaximumOperationCount = 10000;

    private static IEnumerable<InvariantCase> R22Declared(CliCatalog catalog)
    {
        foreach (JsonNode product in catalog.Document["products"]!.AsArray().Select(static product => product!))
        {
            string id = product["id"]!.GetValue<string>();
            yield return Check("C-R22", $"{id} declared", () => (product["operations"]?.AsArray() ?? [])
                .Select(static operations => operations!)
                .Where(static operations => operations["maximumOperationCount"]?.GetValue<int>() != MaximumOperationCount)
                .Select(operations => $"{id} {operations["command"]!.GetValue<string>()}: maximumOperationCount is "
                    + $"{operations["maximumOperationCount"]?.ToJsonString() ?? "missing"}, not {MaximumOperationCount}."));
        }
    }

    // ----- C-R36: format ids are unique and have no aliases; --to lists the product's formats -----

    private static IEnumerable<InvariantCase> R36(CliCatalog catalog)
    {
        foreach (JsonNode product in catalog.Document["products"]!.AsArray().Select(static product => product!))
        {
            string id = product["id"]!.GetValue<string>();
            JsonNode[] formats = [.. product["formats"]!.AsArray().Select(static format => format!)];
            yield return Check("C-R36", $"{id} formats", () =>
            [
                .. formats
                    .Where(static format => format["aliases"]!.AsArray().Count > 0)
                    .Select(format => $"{id} format {format["id"]!.GetValue<string>()} has the aliases {CommandTree.List(CommandTree.Strings(format["aliases"]))}."),
                .. formats
                    .GroupBy(static format => format["id"]!.GetValue<string>(), StringComparer.Ordinal)
                    .Where(static group => group.Count() > 1)
                    .Select(group => $"{id} declares the format {group.Key} {group.Count()} times."),
            ]);
            foreach (TreeCommand command in CommandTree.OfProduct(catalog, id).Where(static command => command.Has("--to")))
            {
                yield return Check("C-R36", $"{command} --to", () =>
                {
                    string list = command.ProductVerb == "render" ? "renderFormats" : "convertFormats";
                    string[] expected = [.. CommandTree.Strings(product[list]).Order(StringComparer.Ordinal)];
                    string[] actual = [.. command.Option("--to")!.AllowedValues.Order(StringComparer.Ordinal)];
                    return actual.SequenceEqual(expected, StringComparer.Ordinal)
                        ? []
                        : [$"{command} --to lists {CommandTree.List(actual)}; {list} is {CommandTree.List(expected)}."];
                });
            }
        }
    }
}
