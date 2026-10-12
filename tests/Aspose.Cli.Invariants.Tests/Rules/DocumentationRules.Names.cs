using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;

namespace Aspose.Cli.Invariants.Tests;

/// <summary>
/// C-R38 in the C# sources: the names that error messages, hints, help and the descriptions the
/// schemas publish (documentation comments) write must exist in this build. The Skills and the
/// README are checked by the Skill name tests of the platform tests. The same reading finds the
/// public text that names a hidden option or command (C-R04).
/// <para>
/// How a source writes a name so that this check reads it: an option as <c>--name</c>, an
/// operation as its <c>snake_case</c> name and a code as its <c>UPPER_SNAKE</c> name, anywhere in
/// the text; a command after <c>aspose-cli</c> (<c>aspose-cli pdf inspect --detail outline</c>),
/// in quotes (<c>'pdf query forms'</c>, also with backquotes or double quotes, and in a
/// documentation comment as <c>&lt;c&gt;pdf query forms&lt;/c&gt;</c>), or as a whole text, such
/// as a help example (<c>cells convert book.csv --to xlsx</c>). Its path must be a command, and
/// the options written after it that command's or global ones. A product command written bare in
/// a sentence (<c>use slides render for</c>) is reported, since quoting is the convention, and its
/// path is still checked: after a product id the next words are command words, so
/// <c>pdf frobnicate</c> has no command; after a product id that prose uses as a noun
/// (<see cref="NounProducts"/>) only a word that names a command somewhere starts a path. A text that is exactly one name declares it rather than
/// naming it (<c>[Operation("set_values")]</c>) and is not read.
/// </para>
/// </summary>
internal static partial class ContractRules
{
    /// <summary>Words of the sources written like a code, operation or option that name something else, with why.</summary>
    private static readonly Dictionary<string, string> NotNames = new(StringComparer.Ordinal)
    {
        ["build_property"] = "the MSBuild property prefix an analyzer reads",
        ["HTTPS_PROXY"] = "the environment variable that sets the proxy of release downloads",
        ["index_files"] = "the folder Excel's HTML export writes beside the page",
        ["PDF_A_2B"] = "the Aspose.Pdf conformance name that pdfa-2b maps to",
        ["PDF_SIGNING_PASSWORD"] = "an example environment variable that --certificate-password-env names",
        ["SCREAMING_SNAKE_CASE"] = "the name of a naming style",
        ["shift_jis"] = "a text encoding name that cells convert --encoding accepts",
        ["--certificate-password"] = "the prefix of the --certificate-password-env and -stdin option family, as a parameter describes it",
        ["--file-filter"] = "an argument of the zenity file picker",
    };

    /// <summary>
    /// Product ids that prose also uses as plain nouns ("table cells", "the slides"): after one of
    /// them only a command word (<see cref="CommandWords"/>) starts a command path. After any
    /// other product id every lowercase word does, so a renamed or removed command is reported.
    /// </summary>
    private static readonly Dictionary<string, string> NounProducts = new(StringComparer.Ordinal)
    {
        ["cells"] = "worksheet cells",
        ["slides"] = "the slides of a deck",
        ["words"] = "words of text",
    };

    /// <summary>The product verbs of C-R02, which start a command path after any product id.</summary>
    private static readonly string[] ContractVerbs =
        ["inspect", "read", "search", "create", "edit", "convert", "render", "compare", "extract", "split", "merge", "sign", "validate"];

    /// <summary>Text that reads like a command after a noun product id but is prose, with where.</summary>
    private static readonly Dictionary<string, string> ProsePhrases = new(StringComparer.Ordinal)
    {
        ["cells read"] = "\"table cells read it before the text\" in a Words result description",
        ["cells stop"] = "\"the listed cells stop at the diff budget\" in a Cells compare result description",
    };

    private static IEnumerable<InvariantCase> R38(CliCatalog catalog)
    {
        var names = new SourceNames(catalog);
        string sources = Path.Combine(RepositoryPaths.Root, "src");
        string[] projects = [.. Directory.GetDirectories(sources).Order(StringComparer.Ordinal)];
        foreach (string project in projects)
        {
            string subject = "src/" + Path.GetFileName(project);
            yield return Check("C-R38", subject, () => names.Of(project).Names);
            yield return Check("C-R38", subject + " bare commands", () => names.Of(project).Bare);
            yield return Check("C-R04", subject + " hidden names", () => names.Of(project).Hidden);
        }
        yield return Check("C-R38", "src reading", () =>
        {
            HashSet<string> used = [.. projects.SelectMany(project => names.Of(project).Exempted)];
            HashSet<string> kinds = [.. projects.SelectMany(project => names.Of(project).Kinds)];
            return NotNames.Keys.Concat(ProsePhrases.Keys).Where(name => !used.Contains(name)).Order(StringComparer.Ordinal)
                .Select(static name => $"'{name}' is exempted as no name, but no source writes it any more; delete the exemption")
                .Concat(new[] { "command", "option", "code", "operation" }.Where(kind => !kinds.Contains(kind))
                    .Select(static kind => $"no source names a {kind} that the reading recognizes, so it checks nothing; its rules stopped matching"));
        });
        yield return Check("C-R04", "Skills and README hidden names", names.HiddenInDocuments);
        yield return HiddenInHintCase(catalog);
    }

    /// <summary>
    /// A <c>cells read</c> over its cell budget, whose follow-up a source composes from constants
    /// that the reading of the sources cannot see: it succeeds with its first window and a
    /// window.next, and no message or hint it prints names a hidden option; only the generated
    /// window.next carries one.
    /// </summary>
    private static InvariantCase HiddenInHintCase(CliCatalog catalog) =>
        Check("C-R04", "cells read budget hint", () =>
        {
            string[] hidden = [.. HiddenOptions(catalog)];
            using var workspace = new TempWorkspace();
            File.WriteAllBytes(workspace.File("book.xlsx"), ScenarioFixtures.Read("cells.xlsx"));
            CliResult result = workspace.Run("cells", "read", "book.xlsx", "--range", "A1:Z100", "--max-cells", "1", "--output-format", "json");
            if (result.ExitCode != 0 || JsonNode.Parse(result.StdOut.Length > 0 ? result.StdOut : result.StdErr) is not { } document
                || document["error"] is not null)
            {
                return [$"a read over its budget failed with exit {result.ExitCode}: {result.StdErr.Trim()}"];
            }

            if (document["window"]?["next"] is null)
            {
                return ["a read over its budget gave no window.next"];
            }

            string text = string.Join(' ', (document["warnings"]?.AsArray() ?? [])
                .Select(static warning => $"{warning?["message"]} {warning?["hint"]}"));
            return hidden.Where(name => OptionMention(name).IsMatch(text))
                .Select(name => $"a warning of the read names the hidden option {name}");
        }, shard: "cells");

    /// <summary>The options that every command hides: a name some command shows is public.</summary>
    private static IEnumerable<string> HiddenOptions(CliCatalog catalog)
    {
        HashSet<string> shown = [.. catalog.Commands.SelectMany(static command => command.Options).Where(static option => !option.Hidden).Select(static option => option.Name)];
        return catalog.Commands.SelectMany(static command => command.Options)
            .Where(option => option.Hidden && !shown.Contains(option.Name))
            .Select(static option => option.Name)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);
    }

    private static Regex OptionMention(string name) => new($@"(?<![\w-]){Regex.Escape(name)}(?![\w-])", RegexOptions.CultureInvariant);

    /// <summary>What the reading of one project found.</summary>
    private sealed record SourceFindings(string[] Names, string[] Bare, string[] Hidden, string[] Exempted, string[] Kinds);

    /// <summary>The names of this build and the reading of each source project against them.</summary>
    private sealed partial class SourceNames
    {
        private readonly CliCatalog _catalog;
        private readonly HashSet<string> _options;
        private readonly HashSet<string> _codes;
        private readonly HashSet<string> _operations;
        private readonly Dictionary<string, HashSet<string>> _commandOptions;
        private readonly HashSet<string> _globalOptions;
        private readonly HashSet<string> _globalValueOptions;
        private readonly Regex _quoted;
        private readonly Regex _bare;
        private readonly Regex _hidden;
        private readonly string[] _products;
        private readonly HashSet<string> _commandWords;
        private readonly ConcurrentDictionary<string, Lazy<SourceFindings>> _findings = new(StringComparer.Ordinal);

        public SourceNames(CliCatalog catalog)
        {
            _catalog = catalog;
            JsonArray commands = catalog.Document["commands"]!.AsArray();
            _commandOptions = commands.ToDictionary(
                static command => Words(command!["path"]!.GetValue<string>()),
                static command => new HashSet<string>(command!["options"]!.AsArray().SelectMany(NamesOf), StringComparer.Ordinal),
                StringComparer.Ordinal);
            _options = [.. _commandOptions.Values.SelectMany(static options => options)];
            _globalOptions = _commandOptions[string.Empty];
            _globalValueOptions =
            [
                .. catalog.Command(string.Empty).Options
                    .Where(static option => option.Type is not ("boolean" or "Void"))
                    .Select(static option => option.Name),
            ];
            _codes =
            [
                .. catalog.Diagnostics.Keys,
                .. catalog.Document["products"]!.AsArray()
                    .SelectMany(static product => product!["review"]?["checks"]?.AsArray() ?? [])
                    .Select(static check => check!["code"]!.GetValue<string>()),
            ];
            _operations =
            [
                .. catalog.Document["products"]!.AsArray()
                    .SelectMany(static product => product!["operations"]?.AsArray() ?? [])
                    .SelectMany(static operation => operation!["ops"]!.AsArray().Select(static op => op!.GetValue<string>())),
            ];
            string roots = string.Join('|', catalog.Command(string.Empty).Subcommands.Select(Regex.Escape));
            _products = [.. catalog.Products.Select(static product => product.Id)];
            _commandWords = [.. ContractVerbs, .. catalog.Commands.SelectMany(static command => command.Tokens)];
            string products = string.Join('|', _products.Select(Regex.Escape));
            _bare = new Regex($@"(?<![\w./\\-])(?<product>{products}) (?<verb>[a-z]+)(?: (?<next>[a-z]+))?\b", RegexOptions.CultureInvariant);
            // Inside one kind of quote, the other kinds may quote a value: 'cells render --sheet "Sales"'.
            _quoted = new Regex(
                string.Join('|', "'`\"".Select(quote => $@"{quote}(?<command>(?:{roots})(?: [^{quote}\n]*?)?){quote}")),
                RegexOptions.CultureInvariant);
            string[] hidden =
            [
                .. HiddenOptions(catalog),
                .. catalog.Commands.Where(static command => command.Hidden).Select(static command => command.Verb),
            ];
            _hidden = new Regex($@"(?<![\w-])(?:{string.Join('|', hidden.Select(Regex.Escape))})(?![\w-])", RegexOptions.CultureInvariant);
        }

        private static IEnumerable<string> NamesOf(JsonNode? option) =>
            option!["aliases"]!.AsArray().Select(static alias => alias!.GetValue<string>()).Append(option["name"]!.GetValue<string>());

        private static string Words(string path) =>
            path.Contains(' ', StringComparison.Ordinal) ? path[(path.IndexOf(' ', StringComparison.Ordinal) + 1)..] : string.Empty;

        public SourceFindings Of(string project) =>
            _findings.GetOrAdd(project, key => new Lazy<SourceFindings>(() => Read(key))).Value;

        /// <summary>
        /// Reads one project's sources. Each finding is reported once per file, without its line,
        /// so an edit elsewhere in the file leaves the text alone.
        /// </summary>
        private SourceFindings Read(string project)
        {
            var problems = new SortedSet<string>(StringComparer.Ordinal);
            var bare = new SortedSet<string>(StringComparer.Ordinal);
            var hidden = new SortedSet<string>(StringComparer.Ordinal);
            var exempted = new HashSet<string>(StringComparer.Ordinal);
            var kinds = new HashSet<string>(StringComparer.Ordinal);
            IEnumerable<string> files = Directory.EnumerateFiles(project, "*.cs", SearchOption.AllDirectories)
                .Where(file => !Path.GetRelativePath(project, file).Split(Path.DirectorySeparatorChar).Any(static part => part is "bin" or "obj"))
                .Order(StringComparer.Ordinal);
            foreach (string file in files)
            {
                string where = Path.GetRelativePath(project, file).Replace('\\', '/');
                foreach (SourceText text in ReadSource(File.ReadAllText(file)))
                {
                    string trimmed = text.Text.Trim();
                    if (UpperSnake().IsMatch(trimmed) || LowerSnake().IsMatch(trimmed) || OptionName().IsMatch(trimmed)
                        || _hidden.Match(trimmed) is { Success: true } whole && whole.Length == trimmed.Length)
                    {
                        // A text that is exactly one name declares it.
                        continue;
                    }
                    problems.UnionWith(CheckText(text.Text, exempted, kinds, out List<string> written).Select(problem => $"{where}: {problem}"));
                    bare.UnionWith(written.Select(command => $"{where}: '{command}' is written bare; quote it or write aspose-cli before it"));
                    hidden.UnionWith(_hidden.Matches(text.Text).Select(match => $"{where}: names the hidden {match.Value}"));
                }
            }
            return new SourceFindings([.. problems], [.. bare], [.. hidden], [.. exempted], [.. kinds]);
        }

        /// <summary>The hidden options and commands that the Skills and the README name.</summary>
        public IEnumerable<string> HiddenInDocuments()
        {
            IEnumerable<string> skills = Directory.GetDirectories(Path.Combine(RepositoryPaths.Root, "src"))
                .Select(static project => Path.Combine(project, "Skills"))
                .Where(Directory.Exists)
                .SelectMany(static skills => Directory.EnumerateFiles(skills, "*.md", SearchOption.AllDirectories));
            foreach (string file in skills.Append(Path.Combine(RepositoryPaths.Root, "README.md")).Order(StringComparer.Ordinal))
            {
                string where = Path.GetRelativePath(RepositoryPaths.Root, file).Replace('\\', '/');
                foreach (string line in File.ReadLines(file))
                {
                    foreach (Match match in _hidden.Matches(line))
                    {
                        yield return $"{where}: names the hidden {match.Value}";
                    }
                }
            }
        }

        private List<string> CheckText(string text, HashSet<string> exempted, HashSet<string> kinds, out List<string> bare)
        {
            var problems = new List<string>();
            bare = [];

            // Commands: their paths are checked against the tree, and their options against the command.
            var spans = new List<(int Start, int End)>();
            foreach (Match match in Prefixed().Matches(text))
            {
                Group command = match.Groups["command"];
                spans.Add((match.Index, command.Index + command.Length));
                problems.AddRange(CheckCommand(command.Value, exempted));
            }
            foreach (Match match in _quoted.Matches(text))
            {
                Group command = match.Groups["command"];
                if (!Inside(spans, command.Index))
                {
                    spans.Add((match.Index, match.Index + match.Length));
                    problems.AddRange(CheckCommand(command.Value, exempted));
                }
            }
            string trimmed = text.Trim();
            string[] words = trimmed.Split(' ', 3);
            if (spans.Count == 0 && words.Length >= 2 && LowerWord().IsMatch(words[1])
                && (StartsCommand(words[0], words[1], exempted)
                    || (_catalog.Command(string.Empty).Subcommands.Contains(words[0]) && _catalog.Commands.Any(command => command.Words == $"{words[0]} {words[1]}"))))
            {
                // A whole text that starts with a command, such as a help example.
                spans.Add((0, text.Length));
                problems.AddRange(CheckCommand(trimmed, exempted));
            }
            if (spans.Count > 0)
            {
                kinds.Add("command");
            }
            foreach (Match match in _bare.Matches(text))
            {
                // After a product name, a lowercase word is a command word: the path must exist.
                string product = match.Groups["product"].Value;
                string verb = match.Groups["verb"].Value;
                if (!Inside(spans, match.Index) && StartsCommand(product, verb, exempted))
                {
                    bare.Add($"{product} {verb}");
                    if (_catalog.Commands.FirstOrDefault(command => command.Words == $"{product} {verb}") is not { } command)
                    {
                        problems.Add($"'{product}' has no command '{verb}'");
                    }
                    else if (command.Subcommands.Count > 0 && match.Groups["next"] is { Success: true } next
                        && !command.Subcommands.Contains(next.Value))
                    {
                        problems.Add($"'{command.Words}' has no command '{next.Value}'");
                    }
                }
            }

            foreach (Match match in OptionToken().Matches(text))
            {
                string name = match.Groups["name"].Value;
                kinds.Add("option");
                if (!Inside(spans, match.Index) && !Known(name, _options, exempted))
                {
                    problems.Add($"'{name}' is no option of any command");
                }
            }
            foreach (Match match in UpperToken().Matches(text))
            {
                kinds.Add("code");
                if (!match.Value.StartsWith("ASPOSE_", StringComparison.Ordinal) && !Known(match.Value, _codes, exempted))
                {
                    problems.Add($"'{match.Value}' is no diagnostic or review check");
                }
            }
            foreach (Match match in LowerToken().Matches(text))
            {
                kinds.Add("operation");
                if (!Known(match.Value, _operations, exempted))
                {
                    problems.Add($"'{match.Value}' is no operation of any product");
                }
            }
            return problems;
        }

        /// <summary>Whether a product id and the word after it start a command path.</summary>
        private bool StartsCommand(string product, string word, HashSet<string> exempted)
        {
            if (!_products.Contains(product))
            {
                return false;
            }
            if (!NounProducts.ContainsKey(product))
            {
                return true;
            }
            if (!_commandWords.Contains(word))
            {
                return false;
            }
            if (ProsePhrases.ContainsKey($"{product} {word}"))
            {
                exempted.Add($"{product} {word}");
                return false;
            }
            return true;
        }

        private static bool Known(string name, HashSet<string> names, HashSet<string> exempted)
        {
            if (names.Contains(name))
            {
                return true;
            }
            if (NotNames.ContainsKey(name))
            {
                exempted.Add(name);
                return true;
            }
            return false;
        }

        private static bool Inside(List<(int Start, int End)> spans, int index) =>
            spans.Any(span => index >= span.Start && index < span.End);

        /// <summary>
        /// Walks a written command down the tree: global options and their values may come first,
        /// then each word must name a subcommand until a leaf, whose options are its own or global.
        /// A clause or sentence ends the command. A placeholder or interpolated word where a
        /// command word belongs stops the walk, and the options after it need only exist
        /// somewhere; so does a word after the bare executable name, which is prose (<c>the
        /// aspose-cli executable</c>).
        /// </summary>
        private IEnumerable<string> CheckCommand(string written, HashSet<string> exempted)
        {
            var tokens = new List<string>();
            foreach (string word in written.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                string token = word.TrimEnd('.', ',', ';', ':', ')');
                if (token.Length > 0)
                {
                    tokens.Add(token);
                }
                if (token.Length != word.Length)
                {
                    break;
                }
            }
            CliCommand command = _catalog.Command(string.Empty);
            bool resolved = true;
            int index = 0;
            for (; index < tokens.Count; index++)
            {
                string token = tokens[index];
                if (token.StartsWith('-'))
                {
                    string name = token.Split('=', 2)[0];
                    if (command.Words.Length == 0 && _globalOptions.Contains(name))
                    {
                        index += _globalValueOptions.Contains(name) && !token.Contains('=', StringComparison.Ordinal) ? 1 : 0;
                        continue;
                    }
                    break;
                }
                if (IsPlaceholder(token))
                {
                    resolved = command.Subcommands.Count == 0;
                    break;
                }
                string words = command.Words.Length == 0 ? token : $"{command.Words} {token}";
                CliCommand? next = _catalog.Commands.FirstOrDefault(candidate => candidate.Words == words);
                if (next is null)
                {
                    if (command.Subcommands.Count > 0)
                    {
                        resolved = false;
                        if (command.Words.Length > 0)
                        {
                            yield return $"'{command.Words}' has no command '{token}'";
                        }
                    }
                    break;
                }
                command = next;
            }
            HashSet<string> options = resolved ? [.. _commandOptions[command.Words], .. _globalOptions] : _options;
            for (; index < tokens.Count; index++)
            {
                Match option = OptionName().Match(tokens[index].Split('=', 2)[0]);
                if (option.Success && !Known(option.Value, options, exempted))
                {
                    yield return resolved
                        ? $"'{(command.Words.Length == 0 ? "aspose-cli" : command.Words)}' has no option '{option.Value}'"
                        : $"'{option.Value}' is no option of any command";
                }
            }
        }

        private static bool IsPlaceholder(string token) =>
            token.StartsWith('<') || token.Contains(Hole, StringComparison.Ordinal) || token.StartsWith("...", StringComparison.Ordinal);

        /// <summary>A command after the executable name, to the end of its quote.</summary>
        [GeneratedRegex(@"(?<![\w-])aspose-cli(?<command>(?: [^\s'`]+)*)")]
        private static partial Regex Prefixed();

        /// <summary>An option name, unless an interpolation or a placeholder completes it (<c>--all-{part}s</c>).</summary>
        [GeneratedRegex(@"(?<![\w-])(?<name>--[a-z][a-z0-9-]*)(?![a-z0-9-]|[…{])")]
        private static partial Regex OptionToken();

        [GeneratedRegex(@"^[a-z]+$")]
        private static partial Regex LowerWord();

        [GeneratedRegex(@"^--[a-z][a-z0-9-]*$")]
        private static partial Regex OptionName();

        [GeneratedRegex(@"(?<![\w-])[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)+(?![\w-])")]
        private static partial Regex UpperToken();

        [GeneratedRegex(@"(?<![\w./\\-])[a-z][a-z0-9]*(?:_[a-z0-9]+)+(?![\w-])")]
        private static partial Regex LowerToken();

        [GeneratedRegex(@"^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)+$")]
        private static partial Regex UpperSnake();

        [GeneratedRegex(@"^[a-z][a-z0-9]*(?:_[a-z0-9]+)+$")]
        private static partial Regex LowerSnake();
    }
}
