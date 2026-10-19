using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Aspose.Cli.TestKit.Scenarios;

/// <summary>
/// The workspace a checked invocation ran in: its arguments, its working and configuration
/// directories and the extra environment variables it ran with.
/// </summary>
/// <param name="Args">The invocation's arguments, without the executable name.</param>
/// <param name="Workspace">The working directory the invocation ran in.</param>
/// <param name="ConfigRoot">The directory that stands for the user's application data, which holds the CLI configuration.</param>
/// <param name="Variables">The extra environment variables, such as a password variable; null for none.</param>
public sealed record HintRun(
    IReadOnlyList<string> Args,
    string Workspace,
    string ConfigRoot,
    IReadOnlyDictionary<string, string?>? Variables);

/// <summary>
/// The hints of a result or an error envelope (<c>error.hint</c> and <c>warnings[].hint</c>) tell
/// the caller what to run next, so what they name must run as written: each command a hint quotes
/// (<c>'aspose-cli cells inspect'</c> or <c>'pdf inspect --detail forms'</c>, or an unquoted
/// <c>aspose-cli …</c> up to its sentence's end), each JSON operation it shows, each option it
/// names alone (<c>lower --dpi</c>) and each operation it names (<c>use clear_autofilter</c>).
/// <list type="bullet">
/// <item>A command with arguments runs on the invocation's own files, in a copy of its workspace,
/// configuration and user profile, with <c>--dry-run</c> where the command has it; a
/// <c>&lt;file&gt;</c>-like placeholder (by its last word, so <c>&lt;that file&gt;</c> too) or a
/// missing input argument takes the invocation's input, a <c>&lt;dir&gt;</c>-like one a directory
/// in that copy, and any other placeholder, such as <c>&lt;sheet&gt;</c>, makes the hint not
/// executable, except <c>&lt;command&gt;</c> (or <c>&lt;subcommand&gt;</c>) alone after a group,
/// as in <c>'aspose-cli cells &lt;command&gt;'</c>. It must not fail with
/// <see cref="RefusedCodes"/>.</item>
/// <item>A command is only checked against the catalog (its command, options, argument count and
/// option values) when it is named by its path and option names alone, such as
/// <c>'cells edit --out'</c>; when the invocation has no file to fill its input with, such as a
/// <c>create</c>'s; when it writes and has no dry run; and when it starts or stops a service or
/// reaches the network (<see cref="StaticOnly"/>).</item>
/// <item>A JSON operation runs as <c>&lt;product&gt; edit &lt;input&gt; --ops … --dry-run</c>.</item>
/// <item>An option named alone must be one of the invocation's command, and the word after it, when
/// it is not running text, one of its allowed values.</item>
/// <item>A named operation must be one of the product's operations; a word counts as one when it is
/// in snake case and starts with a verb some operation of the catalog starts with.</item>
/// </list>
/// An outcome is judged once per test process and keyed by what was judged: the command line that
/// ran, with the invocation's files in it, the content of those files, and its product, or what the catalog check covered, so a
/// catalog check never stands in for a run.
/// </summary>
public static partial class ExecutableHints
{
    /// <summary>The check a problem of a hint reports under.</summary>
    public const string Check = "executable-hint";

    /// <summary>The error codes that mean a hint's command or operation was not written to run.</summary>
    public static IReadOnlyList<string> RefusedCodes { get; } = ["USAGE_ERROR", "OPTION_INVALID", "OPS_INVALID"];

    /// <summary>The top-level commands that start or stop a service or reach the network; they are only checked against the catalog.</summary>
    public static IReadOnlyList<string> StaticOnly { get; } = ["app", "doctor", "mcp", "preview", "update"];

    private const string Executable = "aspose-cli";

    private static readonly string[] InputPlaceholders = ["document", "file", "input", "original", "pdf", "presentation", "workbook"];

    private static readonly string[] DirectoryPlaceholders = ["dir", "directory", "out-dir", "folder"];

    /// <summary>The last word of a placeholder that names an environment variable, such as <c>&lt;owner-password-env&gt;</c>.</summary>
    private const string EnvironmentPlaceholder = "env";

    private static readonly string[] CommandPlaceholders = ["command", "subcommand"];

    /// <summary>Words that follow an option in running text rather than being its value, as in <c>--sheet or</c>.</summary>
    private static readonly HashSet<string> ProseWords = new(
        ["a", "also", "an", "and", "as", "at", "but", "by", "for", "from", "if", "in", "instead", "is", "it", "not", "of", "on", "or", "so", "that", "the", "then", "this", "to", "when", "with"],
        StringComparer.Ordinal);

    private const string Separator = "\u001f";

    private const string WorkspaceMarker = "\u0001workspace";

    private const string OutputMarker = "\u0001output";

    private static readonly ConcurrentDictionary<string, Lazy<string?>> Outcomes = new(StringComparer.Ordinal);

    /// <summary>The problems of the hints in the document a run wrote, its stdout result or its stderr error envelope.</summary>
    public static IReadOnlyList<string> Problems(HintRun run, CliResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return Problems(run, Parse(string.IsNullOrWhiteSpace(result.StdOut) ? result.StdErr : result.StdOut));
    }

    /// <summary>The problems of the hints in a result or an error envelope; empty when every hint runs.</summary>
    public static IReadOnlyList<string> Problems(HintRun run, JsonNode? document)
    {
        ArgumentNullException.ThrowIfNull(run);
        (string Source, string Text)[] hints = [.. Hints(document)];
        if (hints.Length == 0)
        {
            return [];
        }
        CliCatalog catalog = CliCatalog.Current;
        var problems = new List<string>();
        foreach ((string source, string hint) in hints)
        {
            foreach (HintAction action in Extract(catalog, hint))
            {
                Judgement judgement = Plan(catalog, run, document, action);
                if (Outcomes.GetOrAdd(judgement.Key, _ => new Lazy<string?>(judgement.Judge)).Value is { } problem)
                {
                    problems.Add($"the {source} hint {action}: {problem}");
                }
            }
        }
        return [.. problems.Distinct(StringComparer.Ordinal)];
    }

    /// <summary>Throws when a hint of the run's JSON document does not run, naming the invocation and every problem.</summary>
    public static void Require(HintRun run, CliResult result)
    {
        IReadOnlyList<string> problems = Problems(run, result);
        if (problems.Count > 0)
        {
            throw new InvalidOperationException(
                $"A hint of [{string.Join(' ', run.Args)}] does not run as written:\n  " + string.Join("\n  ", problems));
        }
    }

    /// <summary>Every hint of an error envelope or a result, with the code it belongs to.</summary>
    internal static IEnumerable<(string Source, string Text)> Hints(JsonNode? document)
    {
        if (document is not JsonObject root)
        {
            yield break;
        }
        if (root["error"] is JsonObject error && Text(error["hint"]) is { } errorHint)
        {
            yield return (Text(error["code"]) ?? "error", errorHint);
        }
        foreach (JsonNode? warning in root["warnings"] as JsonArray ?? [])
        {
            if (Text(warning?["hint"]) is { } warningHint)
            {
                yield return (Text(warning!["code"]) ?? "warning", warningHint);
            }
        }
    }

    /// <summary>What a hint names: its commands, its JSON operations and its operation names.</summary>
    internal static IReadOnlyList<HintAction> Extract(CliCatalog catalog, string hint)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(hint);
        var actions = new List<HintAction>();
        char[] masked = hint.ToCharArray();
        void Mask(int start, int length) => Array.Fill(masked, ' ', start, length);
        HashSet<string> topLevel = [.. catalog.Commands.Where(static command => !command.Hidden && command.Tokens.Length == 1).Select(static command => command.Words)];
        bool IsCommand(string[] tokens) => tokens.Length > 0 && (tokens[0] == Executable || topLevel.Contains(tokens[0]));

        // A command in backticks is one shell line: its single quotes quote its arguments, and the
        // operations it passes belong to it.
        foreach (Match code in BacktickText().Matches(hint))
        {
            string[] tokens = Tokenize(code.Groups[1].Value, untilSentenceEnd: false);
            if (IsCommand(tokens))
            {
                actions.Add(new HintAction(HintKind.Command, code.Groups[1].Value, tokens[0] == Executable ? tokens[1..] : tokens));
                Mask(code.Index, code.Length);
            }
        }

        for (int index = 0; index < hint.Length; index++)
        {
            if (masked[index] is '{' or '[' && StartsOperation(hint, index) && JsonEnd(hint, index) is int end
                && Parse(hint[index..end]) is { } json && HasOperation(json))
            {
                actions.Add(new HintAction(HintKind.Operations, json.ToJsonString(), []));
                Mask(index, end - index);
                index = end - 1;
            }
        }

        foreach (Match quoted in QuotedText().Matches(new string(masked)))
        {
            string[] tokens = Tokenize(quoted.Groups[1].Value, untilSentenceEnd: false);
            if (IsCommand(tokens))
            {
                actions.Add(new HintAction(HintKind.Command, quoted.Groups[1].Value, tokens[0] == Executable ? tokens[1..] : tokens));
                Mask(quoted.Index, quoted.Length);
            }
        }

        foreach (Match command in UnquotedCommand().Matches(new string(masked)))
        {
            int start = command.Index + command.Length;
            string[] tokens = Tokenize(new string(masked, start, masked.Length - start), untilSentenceEnd: true);
            if (tokens.Length > 0)
            {
                actions.Add(new HintAction(HintKind.Command, Executable + " " + string.Join(' ', tokens.Select(Quote)), tokens));
            }
            Mask(command.Index, masked.Length - command.Index);
        }

        foreach (Match option in BareOption().Matches(new string(masked)))
        {
            string value = option.Groups[2].Value;
            string text = value.Length == 0 || ProseWords.Contains(value) ? option.Groups[1].Value : $"{option.Groups[1].Value} {value}";
            if (!actions.Any(action => action.Kind == HintKind.Option && action.Text == text))
            {
                actions.Add(new HintAction(HintKind.Option, text, text.Split(' ')));
            }
        }

        HashSet<string> verbs = [.. catalog.Products.SelectMany(static product => product.Ops).Select(static op => op.Split('_')[0])];
        foreach (Match name in OperationName().Matches(new string(masked)))
        {
            if (verbs.Contains(name.Value.Split('_')[0]) && !actions.Any(action => action.Kind == HintKind.Operation && action.Text == name.Value))
            {
                actions.Add(new HintAction(HintKind.Operation, name.Value, []));
            }
        }
        return actions;
    }

    /// <summary>
    /// How an action is judged and the key its outcome is cached by: what actually runs, the
    /// command line with the invocation's files in it, and for which product, or, for a check
    /// against the catalog alone, what is checked. A run and a catalog check never share a key.
    /// </summary>
    private sealed record Judgement(string Key, Func<string?> Judge)
    {
        public static Judgement Static(string subject, string? product, string? problem) =>
            new($"static\n{product}\n{subject}", () => problem);
    }

    private static Judgement Plan(CliCatalog catalog, HintRun run, JsonNode? document, HintAction action)
    {
        Invocation invocation = Invocation.Of(catalog, run, document);
        return action.Kind switch
        {
            HintKind.Command => PlanCommand(catalog, run, invocation, action.Tokens),
            HintKind.Operations => PlanOperations(catalog, run, invocation, action.Text),
            HintKind.Option => PlanOption(catalog, run, invocation, action.Tokens),
            _ => Judgement.Static("operation " + action.Text, invocation.Product, OperationNameProblem(catalog, invocation, action.Text)),
        };
    }

    private static Judgement PlanCommand(CliCatalog catalog, HintRun run, Invocation invocation, IReadOnlyList<string> tokens)
    {
        CommandLine line = CommandLine.Parse(catalog, tokens);
        string subject = "command " + string.Join(Separator, tokens);
        if (line.UnknownOptions.Count == 0 && line.Positionals.Count == 0 && !line.HasValues)
        {
            return Judgement.Static(subject, invocation.Product, null); // A command named by its path and options, all of which exist.
        }
        if (line.Command.Subcommands.Count > 0 && line.Rest.Count == 1 && Placeholder().Match(line.Rest[0]) is { Success: true } usage
            && usage.Value == line.Rest[0] && CommandPlaceholders.Contains(usage.Groups[1].Value))
        {
            return Judgement.Static(subject, invocation.Product, null); // A group's usage, such as 'aspose-cli cells <command>'.
        }
        if (line.Rest.SelectMany(static token => Placeholder().Matches(token)).FirstOrDefault(static placeholder =>
                !Fills(InputPlaceholders, placeholder) && !Fills(DirectoryPlaceholders, placeholder) && !NamesVariable(placeholder)) is { } unfilled)
        {
            return Judgement.Static(subject, invocation.Product, $"not executable: this invocation cannot fill {unfilled.Value}");
        }
        int required = line.Command.Arguments.Count(static argument => argument is { Required: true, InputKind: "file" });
        // A file the invocation does not have, such as the one a create would write first, a
        // command that writes without a dry run, and one that starts a service or reaches the
        // network are left to the catalog check.
        bool unfilledFile = (line.Positionals.Count == 0 && invocation.Files.Count < required)
            || (invocation.Files.Count == 0 && line.Rest.SelectMany(static token => Placeholder().Matches(token)).Any(static placeholder => Fills(InputPlaceholders, placeholder)));
        bool writesForReal = !line.Command.Has("--dry-run") && (line.Command.Has("--out") || line.Command.Has("--out-dir") || line.Command.Has("--in-place"));
        string? variable = Variable(run);
        // A variable placeholder the run sets no variable for leaves the command to the catalog
        // and its operations to the ops schema.
        bool unfilledVariable = variable is null && line.Rest.SelectMany(static token => Placeholder().Matches(token)).Any(NamesVariable);
        if (unfilledFile || writesForReal || unfilledVariable || (line.Path.Count > 0 && StaticOnly.Contains(line.Path[0])))
        {
            return Judgement.Static(subject, invocation.Product, StaticProblem(line)
                ?? line.Values.Where(static pair => pair.Option.Name == "--ops")
                    .Select(pair => OperationsSchemaProblem(catalog, line.Command.Product, pair.Value)).FirstOrDefault(static problem => problem is not null));
        }

        var args = new List<string>(line.Path);
        if (line.Positionals.Count == 0 && required > 0)
        {
            args.AddRange(invocation.Files.Take(required).Select(file => Logical(run, file)));
        }
        foreach (string token in line.Rest)
        {
            string value = Logical(run, token);
            foreach (Match placeholder in Placeholder().Matches(token))
            {
                value = value.Replace(placeholder.Value,
                    NamesVariable(placeholder) ? variable!
                    : Fills(InputPlaceholders, placeholder) ? Logical(run, invocation.Files[0])
                    : OutputMarker, StringComparison.Ordinal);
            }
            args.Add(value);
        }
        if (line.Command.Has("--dry-run") && !args.Contains("--dry-run"))
        {
            args.Add("--dry-run");
        }
        if (!args.Any(static arg => arg == "--output-format" || arg.StartsWith("--output-format=", StringComparison.Ordinal)))
        {
            args.AddRange(["--output-format", "json"]);
        }
        return Execution(run, invocation, invocation.Product, args);
    }

    private static Judgement PlanOperations(CliCatalog catalog, HintRun run, Invocation invocation, string json)
    {
        string subject = "operations " + json;
        if (invocation.Product is not { } product || invocation.Files.Count == 0)
        {
            return Judgement.Static(subject, invocation.Product, "not executable: this invocation has no product document to apply the operations to");
        }
        if (!catalog.Commands.Any(command => command.Words == product + " edit"))
        {
            return Judgement.Static(subject, product, $"not executable: {product} has no edit command");
        }
        JsonNode node = JsonNode.Parse(json)!;
        string ops = node is JsonObject wrapper && wrapper.ContainsKey("ops")
            ? json
            : new JsonObject { ["ops"] = node is JsonArray ? node.DeepClone() : new JsonArray(node.DeepClone()) }.ToJsonString();
        if (Placeholder().Matches(ops).Any(NamesVariable))
        {
            if (Variable(run) is not { } variable)
            {
                return Judgement.Static(subject, product, OperationsSchemaProblem(catalog, product, ops));
            }
            ops = Placeholder().Replace(ops, placeholder => NamesVariable(placeholder) ? variable : placeholder.Value);
        }
        return Execution(run, invocation, product, [product, "edit", Logical(run, invocation.Files[0]), "--ops", ops, "--dry-run", "--output-format", "json"]);
    }

    /// <summary>An option a hint names alone, such as <c>--dpi</c> or <c>--scope all</c>: the invocation's command must have it and allow the value.</summary>
    private static Judgement PlanOption(CliCatalog catalog, HintRun run, Invocation invocation, IReadOnlyList<string> tokens)
    {
        CommandLine command = CommandLine.Parse(catalog, run.Args);
        CommandLine line = CommandLine.Parse(catalog, [.. command.Path, tokens[0]]);
        string? problem = line.UnknownOptions.Count > 0 ? $"'{line.Command}' has no option {line.UnknownOptions[0]}" : null;
        // The word after a flag is running text, not its value.
        if (problem is null && tokens.Count > 1 && line.Command.Options.Concat(catalog.Commands.Single(static root => root.Words.Length == 0).Options)
                .FirstOrDefault(option => option.Name == tokens[0]) is { MaximumArity: > 0 } valued && TakesValue(valued, tokens[1]))
        {
            problem = StaticProblem(line with { Values = [(valued, tokens[1])] });
        }
        return Judgement.Static($"option {line.Command.Words}{Separator}{string.Join(Separator, tokens)}", invocation.Product, problem);
    }

    /// <summary>
    /// A command line to run in a copy of the invocation's workspace, keyed by the command line
    /// itself and the content of the invocation's files, so the same file name in two workspaces
    /// is judged on each one's content.
    /// </summary>
    private static Judgement Execution(HintRun run, Invocation invocation, string? product, IReadOnlyList<string> args) =>
        new($"run\n{product}\n{ContentKey(run, invocation)}\n{string.Join(Separator, args)}", () =>
        {
            using var scratch = new Scratch(run);
            var resolved = new List<string>();
            foreach (string arg in args)
            {
                string value = scratch.Resolve(arg);
                // An output outside the copy of the workspace is written into it instead.
                if (resolved.Count > 0 && resolved[^1] is "--out" or "--out-dir" && Path.IsPathFullyQualified(value) && !scratch.Contains(value))
                {
                    value = Path.Combine(scratch.OutputDirectory, Path.GetFileName(value));
                }
                resolved.Add(value);
            }
            return scratch.Run(resolved);
        });

    /// <summary>The SHA-256 of each of the invocation's files that exists, in order; a missing one is empty.</summary>
    private static string ContentKey(HintRun run, Invocation invocation) =>
        string.Join(Separator, invocation.Files
            .Select(file => Path.IsPathFullyQualified(file) ? file : Path.Combine(run.Workspace, file))
            .Select(static path => File.Exists(path) ? FileHashes.Sha256(path) : string.Empty));

    /// <summary>A value with the invocation's workspace written as a marker, so the same command line on the same files has one key.</summary>
    private static string Logical(HintRun run, string value) =>
        value.Replace(Path.TrimEndingDirectorySeparator(run.Workspace), WorkspaceMarker, StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether <paramref name="token"/> is a value of the option: a boolean option takes only <c>true</c> or <c>false</c>, as in <c>--overwrite replaces</c>.</summary>
    private static bool TakesValue(CliOption option, string token) =>
        option.Type != "boolean" || token is "true" or "false";

    /// <summary>Whether a placeholder's last word, such as <c>file</c> of <c>&lt;that file&gt;</c>, is one of <paramref name="names"/>.</summary>
    private static bool Fills(string[] names, Match placeholder) =>
        names.Contains(placeholder.Groups[1].Value.Split(' ')[^1], StringComparer.Ordinal);

    /// <summary>Whether a placeholder names an environment variable, such as <c>&lt;user-password-env&gt;</c>.</summary>
    private static bool NamesVariable(Match placeholder) =>
        placeholder.Groups[1].Value.Split(' ', '-')[^1] == EnvironmentPlaceholder;

    /// <summary>
    /// The environment variable a hint's <c>&lt;...-env&gt;</c> placeholder takes: the one the
    /// invocation names with a <c>*-env</c> option, else one the run sets; null when there is none.
    /// </summary>
    private static string? Variable(HintRun run)
    {
        for (int index = 0; index + 1 < run.Args.Count; index++)
        {
            if (run.Args[index].StartsWith("--", StringComparison.Ordinal) && run.Args[index].EndsWith("-env", StringComparison.Ordinal))
            {
                return run.Args[index + 1];
            }
        }
        return run.Variables?.FirstOrDefault(static pair => !string.IsNullOrEmpty(pair.Value)).Key;
    }

    /// <summary>The problem of the operations a command line passes with <c>--ops</c>, checked against its product's ops schema.</summary>
    private static string? OperationsSchemaProblem(CliCatalog catalog, string? product, string json)
    {
        if (product is null || catalog.Products.FirstOrDefault(candidate => candidate.Id == product)?.OpsSchemaId is not { } schema
            || Parse(json) is not { } node)
        {
            return null;
        }
        JsonNode document = node is JsonObject wrapper && wrapper.ContainsKey("ops")
            ? node
            : new JsonObject { ["ops"] = node is JsonArray ? node.DeepClone() : new JsonArray(node.DeepClone()) };
        return ScenarioContract.Validate(schema, document) is { } invalid ? $"the operations {invalid}" : null;
    }

    private static string? StaticProblem(CommandLine line)
    {
        if (line.UnknownOptions.Count > 0)
        {
            return $"'{line.Command}' has no option {string.Join(", ", line.UnknownOptions)}";
        }
        if (line.Positionals.Count > line.Command.Arguments.Sum(static argument => argument.MaximumArity))
        {
            return $"'{line.Command}' takes no argument '{line.Positionals[^1]}'";
        }
        foreach ((CliOption option, string value) in line.Values)
        {
            if (option.AllowedValues.Count > 0 && !option.AllowedValues.Contains(value, StringComparer.Ordinal) && !Placeholder().IsMatch(value))
            {
                return $"{option.Name} of '{line.Command}' does not accept '{value}'";
            }
        }
        return null;
    }

    private static string? OperationNameProblem(CliCatalog catalog, Invocation invocation, string name)
    {
        CliProduct[] products = invocation.Product is { } id ? [catalog.Product(id)] : [.. catalog.Products];
        return products.Any(product => product.Ops.Contains(name, StringComparer.Ordinal))
            ? null
            : $"{(invocation.Product is { } owner ? owner : "no product")} has no operation '{name}'";
    }

    private static bool StartsOperation(string text, int index)
    {
        int next = index + 1;
        while (next < text.Length && char.IsWhiteSpace(text[next]))
        {
            next++;
        }
        if (text[index] == '[')
        {
            return next < text.Length && text[next] == '{' && StartsOperation(text, next);
        }
        return text.AsSpan(next).StartsWith("\"op", StringComparison.Ordinal);
    }

    /// <summary>The index after the JSON value that starts at <paramref name="start"/>, or null when it does not close.</summary>
    private static int? JsonEnd(string text, int start)
    {
        int depth = 0;
        bool inString = false;
        for (int index = start; index < text.Length; index++)
        {
            char character = text[index];
            if (inString)
            {
                if (character == '\\')
                {
                    index++;
                }
                else if (character == '"')
                {
                    inString = false;
                }
            }
            else if (character == '"')
            {
                inString = true;
            }
            else if (character is '{' or '[')
            {
                depth++;
            }
            else if (character is '}' or ']' && --depth == 0)
            {
                return index + 1;
            }
        }
        return null;
    }

    private static bool HasOperation(JsonNode json) => json switch
    {
        JsonObject item => item.ContainsKey("op") || (item["ops"] is JsonArray ops && ops.Any(static op => op is JsonObject { } entry && entry.ContainsKey("op"))),
        JsonArray items => items.Any(static item => item is JsonObject entry && entry.ContainsKey("op")),
        _ => false,
    };

    /// <summary>
    /// Splits a command line the way a shell splits double quotes. With
    /// <paramref name="untilSentenceEnd"/>, the line ends before a <c>(</c> that opens a token and
    /// after a token that ends with sentence punctuation, which is dropped.
    /// </summary>
    private static string[] Tokenize(string text, bool untilSentenceEnd)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        char quote = '\0';
        bool quoted = false;
        bool placeholder = false;
        bool started = false;
        for (int index = 0; index < text.Length; index++)
        {
            char character = text[index];
            if (placeholder || (character == '<' && Placeholder().Match(text, index) is { Success: true } match && match.Index == index))
            {
                // A placeholder such as <that file> is one token, spaces included.
                current.Append(character);
                started = true;
                placeholder = character != '>';
            }
            else if (character is '"' or '\'' && (!quoted || character == quote))
            {
                quoted = !quoted;
                quote = quoted ? character : '\0';
                started = true;
            }
            else if (!quoted && char.IsWhiteSpace(character))
            {
                if (started && Finish())
                {
                    return [.. tokens];
                }
            }
            else if (untilSentenceEnd && !quoted && !started && character == '(')
            {
                return [.. tokens];
            }
            else
            {
                current.Append(character);
                started = true;
            }
        }
        if (started)
        {
            Finish();
        }
        return [.. tokens];

        bool Finish()
        {
            string token = current.ToString();
            current.Clear();
            started = false;
            bool end = untilSentenceEnd && token.Length > 0 && token[^1] is '.' or ',' or ';' or ':';
            if (end)
            {
                token = token[..^1];
            }
            if (token.Length > 0)
            {
                tokens.Add(token);
            }
            return end;
        }
    }

    private static string Quote(string token) => token.Any(char.IsWhiteSpace) ? $"\"{token}\"" : token;

    private static JsonNode? Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }
        try
        {
            return JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue(out string? text) && !string.IsNullOrWhiteSpace(text) ? text : null;

    /// <summary>Single-quoted text whose quotes are not apostrophes inside a word, such as <c>sheet's</c>.</summary>
    [GeneratedRegex(@"(?<![\p{L}\p{N}])'([^'\r\n]+)'(?![\p{L}\p{N}])")]
    private static partial Regex QuotedText();

    /// <summary>Text in backticks, the way a hint quotes a whole command line that has quotes of its own.</summary>
    [GeneratedRegex(@"`([^`\r\n]+)`")]
    private static partial Regex BacktickText();

    [GeneratedRegex(@"(?<![\w'-])aspose-cli(?=\s)")]
    private static partial Regex UnquotedCommand();

    [GeneratedRegex(@"(?<![\w-])(--[a-z][a-z0-9-]*)(?:[ =]([a-z0-9][a-z0-9_.-]*[a-z0-9]|[a-z0-9]))?")]
    private static partial Regex BareOption();

    [GeneratedRegex(@"(?<![\w-])[a-z][a-z0-9]*(?:_[a-z0-9]+)+(?![\w-])")]
    private static partial Regex OperationName();

    [GeneratedRegex(@"<([a-z][a-z0-9-]*(?: [a-z][a-z0-9-]*)*)>")]
    private static partial Regex Placeholder();

    /// <summary>The invocation a hint came from: its product and its input files, as it named them.</summary>
    private sealed record Invocation(string? Product, IReadOnlyList<string> Files)
    {
        public static Invocation Of(CliCatalog catalog, HintRun run, JsonNode? document)
        {
            CommandLine line = CommandLine.Parse(catalog, run.Args);
            var files = new List<string>();
            for (int index = 0; index < line.Positionals.Count && line.Command.Arguments.Count > 0; index++)
            {
                CliArgument argument = line.Command.Arguments[Math.Min(index, line.Command.Arguments.Count - 1)];
                if (argument.InputKind == "file")
                {
                    files.Add(line.Positionals[index]);
                }
            }
            if (files.Count == 0 && Text(document?["output"]?["path"]) is { } output && File.Exists(output))
            {
                files.Add(output);
            }
            string? product = line.Command.Product
                ?? (files.Count > 0 ? catalog.ReaderOf(Path.IsPathFullyQualified(files[0]) ? files[0] : Path.Combine(run.Workspace, files[0])) : null);
            return new Invocation(product, files);
        }
    }

    /// <summary>A command line split by the catalog into its command path, options with their values and positional arguments.</summary>
    private sealed record CommandLine(
        CliCommand Command,
        IReadOnlyList<string> Path,
        IReadOnlyList<string> Rest,
        IReadOnlyList<string> Positionals,
        IReadOnlyList<string> UnknownOptions,
        IReadOnlyList<(CliOption Option, string Value)> Values)
    {
        public bool HasValues => Values.Count > 0;

        public static CommandLine Parse(CliCatalog catalog, IReadOnlyList<string> tokens)
        {
            CliCommand command = catalog.Commands.Single(static command => command.Words.Length == 0);
            CliCommand root = command;
            // Global options may come before the command path, as in --workdir X pdf create;
            // they are read with the options after it.
            int start = 0;
            while (start < tokens.Count && tokens[start].StartsWith("--", StringComparison.Ordinal)
                && root.Options.FirstOrDefault(option => option.Recursive && option.Name == tokens[start].Split('=')[0]) is { } global)
            {
                start += !tokens[start].Contains('=', StringComparison.Ordinal) && global.MaximumArity > 0 && global.Type != "boolean" && start + 1 < tokens.Count ? 2 : 1;
            }
            if (start > 0)
            {
                tokens = [.. tokens.Skip(start), .. tokens.Take(start)];
            }
            int length = 0;
            for (int count = 1; count <= tokens.Count; count++)
            {
                string words = string.Join(' ', tokens.Take(count));
                if (catalog.Commands.FirstOrDefault(candidate => candidate.Words == words) is not { } found)
                {
                    break;
                }
                command = found;
                length = count;
            }
            CliOption[] options = [.. command.Options, .. root.Options.Where(option => option.Recursive && !command.Has(option.Name))];
            var positionals = new List<string>();
            var unknown = new List<string>();
            var values = new List<(CliOption, string)>();
            bool literal = false;
            for (int index = length; index < tokens.Count; index++)
            {
                string token = tokens[index];
                if (literal || token.Length < 2 || token[0] != '-')
                {
                    positionals.Add(token);
                    continue;
                }
                if (token == "--")
                {
                    literal = true;
                    continue;
                }
                int equals = token.IndexOf('=', StringComparison.Ordinal);
                string name = equals < 0 ? token : token[..equals];
                CliOption? option = options.FirstOrDefault(candidate => candidate.Name == name || candidate.Aliases.Contains(name, StringComparer.Ordinal));
                if (option is null)
                {
                    unknown.Add(name);
                    continue;
                }
                if (equals >= 0)
                {
                    values.Add((option, token[(equals + 1)..]));
                    continue;
                }
                for (int taken = 0; taken < option.MaximumArity && index + 1 < tokens.Count && !tokens[index + 1].StartsWith('-') && TakesValue(option, tokens[index + 1]); taken++)
                {
                    values.Add((option, tokens[++index]));
                }
            }
            return new CommandLine(command, [.. tokens.Take(length)], [.. tokens.Skip(length)], positionals, unknown, values);
        }
    }

    /// <summary>
    /// A copy of the invocation's workspace and configuration in which a hint runs, so that what it
    /// writes never reaches the invocation's files; removed afterwards.
    /// </summary>
    private sealed class Scratch : IDisposable
    {
        private readonly TempDirectory _directory = new();
        private readonly HintRun _run;
        private readonly string _workspace;
        private readonly string _config;
        private readonly string _home;

        public Scratch(HintRun run)
        {
            _run = run;
            _workspace = _directory.File("workspace");
            _config = _directory.File("config");
            _home = _directory.File("home");
            Directory.CreateDirectory(_home);
            Copy(run.Workspace, _workspace);
            Copy(run.ConfigRoot, _config);
            OutputDirectory = System.IO.Path.Combine(_workspace, "hint-output");
            Directory.CreateDirectory(OutputDirectory);
        }

        public string OutputDirectory { get; }

        /// <summary>A logical value with the workspace and output markers replaced by their places in the copy.</summary>
        public string Resolve(string value) =>
            value.Replace(WorkspaceMarker, _workspace, StringComparison.Ordinal).Replace(OutputMarker, OutputDirectory, StringComparison.Ordinal);

        public bool Contains(string path) =>
            System.IO.Path.GetFullPath(path).StartsWith(_directory.Path, StringComparison.OrdinalIgnoreCase);

        /// <summary>Runs the command line in the copy; the refusal it ends with, or null.</summary>
        public string? Run(IReadOnlyList<string> args)
        {
            CliResult result;
            try
            {
                // The user's profile is the copy's too, so nothing a hint runs reaches the real one.
                var variables = new Dictionary<string, string?>(_run.Variables ?? new Dictionary<string, string?>(), StringComparer.OrdinalIgnoreCase)
                {
                    ["HOME"] = _home,
                    ["USERPROFILE"] = _home,
                };
                result = new CliProcess(CliRunner.ExecutablePath, CliEnvironment.Evaluation(_config, variables), TimeSpan.FromSeconds(60))
                    .Run(_workspace, string.Empty, [.. args]);
            }
            catch (TimeoutException)
            {
                return null; // Only a refusal of the command line itself is judged.
            }
            if (result.ExitCode == 0 || Parse(result.StdErr)?["error"] is not JsonObject error
                || Text(error["code"]) is not { } code || !RefusedCodes.Contains(code))
            {
                return null;
            }
            string message = Text(error["message"]) ?? string.Empty;
            return $"fails with {code}: {ScenarioContract.Excerpt(message.Replace(_workspace, "<workspace>", StringComparison.OrdinalIgnoreCase))}";
        }

        public void Dispose() => _directory.Dispose();

        private static void Copy(string source, string target)
        {
            Directory.CreateDirectory(target);
            if (!Directory.Exists(source))
            {
                return;
            }
            foreach (string directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(System.IO.Path.Combine(target, System.IO.Path.GetRelativePath(source, directory)));
            }
            foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                File.Copy(file, System.IO.Path.Combine(target, System.IO.Path.GetRelativePath(source, file)), overwrite: true);
            }
        }
    }
}

/// <summary>What a hint names.</summary>
internal enum HintKind
{
    /// <summary>A command line; <see cref="HintAction.Tokens"/> holds it without the executable name.</summary>
    Command,

    /// <summary>A JSON operation, an array of them or an ops document.</summary>
    Operations,

    /// <summary>An operation by its name.</summary>
    Operation,

    /// <summary>An option named without a command, with the value that follows it when there is one.</summary>
    Option,
}

/// <summary>One thing a hint names, as it wrote it.</summary>
internal sealed record HintAction(HintKind Kind, string Text, IReadOnlyList<string> Tokens)
{
    public override string ToString() => Kind switch
    {
        HintKind.Command => $"'{Text}'",
        HintKind.Operations => $"operations {Text}",
        HintKind.Option => $"option '{Text}'",
        _ => $"operation '{Text}'",
    };
}
