using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Aspose.Cli.TestKit.Scenarios;

/// <summary>
/// The variants the scenarios and the generated cases judge hints on as well: a command that
/// replaces an existing output, the invocation run again with the options a hint says to run it
/// again with, and the invocation run again on an encrypted input, opened with a password from a
/// variable and from stdin, and on an input at a path with a space.
/// </summary>
public static partial class ExecutableHints
{
    /// <summary>The error code of a command that would write over an existing file it was not told to replace.</summary>
    private const string OutputExists = "OUTPUT_EXISTS";

    /// <summary>The variable that holds the password of the encrypted variant's input.</summary>
    private const string VariantVariable = "HINT_VARIANT_PASSWORD";

    /// <summary>The password of the encrypted variant's input; the encrypted fixtures' own.</summary>
    private const string VariantPassword = ScenarioFixtures.Password;

    /// <summary>The option that reads the input's password from stdin.</summary>
    private const string PasswordStdinOption = "--password-stdin";

    /// <summary>The folder of the copy of the workspace each variant's input is written in; its name marks the commands that name it.</summary>
    private static readonly (string Name, string Folder, string Description)[] InputVariants =
    [
        ("encrypted", "hint-variant-encrypted", "on an encrypted copy of its input opened with --password-env"),
        ("stdin", "hint-variant-stdin", "on an encrypted copy of its input opened with --password-stdin"),
        ("spaced", "hint variant spaced", "on its input at a path with a space"),
    ];

    /// <summary>Where the variant run writes the outputs the invocation names, which already hold the invocation's own.</summary>
    private const string VariantOutputFolder = "hint variant output";

    /// <summary>Whether the command's <c>--out</c> names a file or directory that exists in the invocation's workspace.</summary>
    private static bool ExistingOutput(HintRun run, CommandLine line) =>
        line.Values.Where(static pair => pair.Option.Name == "--out").Select(static pair => pair.Value).FirstOrDefault() is { } named
        && !Placeholder().IsMatch(named)
        && Resolved(run, named) is { } path
        && InWorkspace(run, path)
        && (File.Exists(path) || Directory.Exists(path));

    /// <summary>Whether a full path lies in the invocation's workspace, whose copy a command runs in.</summary>
    private static bool InWorkspace(HintRun run, string path) =>
        path.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(run.Workspace)) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A command that replaces an existing output, run for real in a copy of the workspace as
    /// <see cref="Execution"/> runs one, which must not fail with <c>OUTPUT_EXISTS</c> either.
    /// </summary>
    private static Judgement Replacing(HintRun run, Invocation invocation, string? product, IReadOnlyList<string> args)
    {
        string[] refused = [.. RefusedCodes, .. invocation.Password is null ? [] : PasswordCodes, OutputExists];
        return new($"replace\n{Execution(run, invocation, product, args).Key}", () =>
        {
            using var scratch = new Scratch(run);
            return scratch.Run([.. args.Select(scratch.Resolve)], refused);
        });
    }

    /// <summary>
    /// Whether the invocation can run again as it ran: what it read from stdin is a JSON document
    /// <see cref="PipedInput"/> gives it again, or it read nothing there. A secret read from stdin
    /// (<c>--password-stdin</c>) is not kept, so a run that read one is not repeated.
    /// </summary>
    private static bool Repeatable(HintRun run) =>
        !run.Args.Any(static arg => arg.EndsWith("-stdin", StringComparison.Ordinal))
        && (!run.Args.Contains("-") || PipedInput(run, run.Args) is not null);

    /// <summary>The part of a key that tells apart the stdin the invocation's command line is given again, as <see cref="Execution"/>'s key does.</summary>
    private static string PipedKey(HintRun run, IReadOnlyList<string> args) =>
        PipedInput(run, args) is { } input ? Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input))) : string.Empty;

    /// <summary>A path as the invocation's workspace resolves it, or null when it is no path.</summary>
    private static string? Resolved(HintRun run, string value)
    {
        try
        {
            return Path.GetFullPath(Path.IsPathFullyQualified(value) ? value : Path.Combine(run.Workspace, value));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    /// <summary>
    /// The problems of a hint that says to run the command again with options, as in "run the
    /// command again with --encrypt-env naming an environment variable that holds a password": the
    /// invocation runs again with them in a copy of its workspace, which holds what it wrote, and
    /// must not fail with a refused code or <c>OUTPUT_EXISTS</c>. An option that names a variable
    /// takes the one the hint names, else one the check sets; a hint that names an option with any
    /// other value, or one the command does not have, is left to the option check. Only a
    /// successful invocation is run again: a failed one needs more than the options, as a mistyped
    /// command run again with <c>--help</c> does.
    /// </summary>
    private static IEnumerable<string> AgainProblems(CliCatalog catalog, HintRun run, JsonNode? document, IReadOnlyList<(string Source, string Text)> hints)
    {
        if (document is not JsonObject result || result.ContainsKey("error") || !Repeatable(run))
        {
            yield break; // A failure, or a run that cannot be repeated.
        }
        CommandLine line = CommandLine.Parse(catalog, run.Args);
        CliCommand root = catalog.Commands.Single(static command => command.Words.Length == 0);
        Invocation invocation = Invocation.Of(catalog, run, document);
        foreach ((string source, string hint) in hints)
        {
            foreach (Match again in RunAgain().Matches(hint))
            {
                var added = new List<string>();
                bool fillable = true;
                foreach (Match named in AgainOption().Matches(again.Groups["clause"].Value))
                {
                    string name = named.Groups[1].Value;
                    CliOption? option = line.Command.Option(name) ?? root.Options.FirstOrDefault(candidate => candidate.Recursive && candidate.Name == name);
                    if (option is null)
                    {
                        fillable = false;
                    }
                    else if (run.Args.Contains(name))
                    {
                        continue;
                    }
                    else if (option.ValueSource == "environment-variable-name")
                    {
                        added.AddRange([name, named.Groups[2].Success ? named.Groups[2].Value : VariantVariable]);
                    }
                    else if (option.MaximumArity == 0 || option.Type == "boolean")
                    {
                        added.Add(name);
                    }
                    else
                    {
                        fillable = false;
                    }
                }
                if (!fillable || added.Count == 0)
                {
                    continue;
                }
                string key = $"again\n{ContentKey(run, invocation)}\n{PipedKey(run, run.Args)}\n{string.Join(Separator, run.Args.Select(arg => Logical(run, arg)))}\n{string.Join(Separator, added)}";
                if (Outcomes.GetOrAdd(key, _ => new Lazy<string?>(() =>
                    {
                        var variables = new Dictionary<string, string?>(run.Variables ?? new Dictionary<string, string?>(), StringComparer.OrdinalIgnoreCase)
                        {
                            [VariantVariable] = VariantPassword,
                        };
                        using var scratch = new Scratch(run with { Variables = variables });
                        string[] refused = [.. RefusedCodes, .. invocation.Password is null ? [] : PasswordCodes, OutputExists];
                        return scratch.Run([.. run.Args.Select(arg => scratch.Resolve(Logical(run, arg))), .. added], refused);
                    })).Value is { } problem)
                {
                    yield return $"the {source} hint, run as the command again with {string.Join(' ', added)}: {problem}";
                }
            }
        }
    }

    /// <summary>
    /// The problems of the hints of the invocation run on each variant of its input, when a command
    /// one of its hints names holds that input: judged once per invocation, input content and variant.
    /// </summary>
    private static IEnumerable<string> VariantProblems(CliCatalog catalog, HintRun run, JsonNode? document, IReadOnlyList<HintAction> commands)
    {
        Invocation invocation = Invocation.Of(catalog, run, document);
        if (invocation.Files.Count == 0 || !Repeatable(run))
        {
            yield break;
        }
        string input = invocation.Files[0];
        string? full = Resolved(run, input);
        if (full is null || !File.Exists(full) || !InWorkspace(run, full)
            || !commands.Any(action => action.Tokens.Any(token => string.Equals(Resolved(run, token), full, StringComparison.OrdinalIgnoreCase))))
        {
            yield break;
        }
        foreach ((string name, string folder, string description) in InputVariants)
        {
            string key = $"variant\n{name}\n{ContentKey(run, invocation)}\n{PipedKey(run, run.Args)}\n{string.Join(Separator, run.Args.Select(arg => Logical(run, arg)))}";
            if (Outcomes.GetOrAdd(key, _ => new Lazy<string?>(() => JudgeVariant(catalog, run, invocation, input, full, name, folder, description))).Value is { } problems)
            {
                foreach (string problem in problems.Split('\n'))
                {
                    yield return problem;
                }
            }
        }
    }

    /// <summary>The problems, one per line, of the hints the invocation gives on one variant of its input; null when there are none or the variant does not apply.</summary>
    private static string? JudgeVariant(CliCatalog catalog, HintRun run, Invocation invocation, string input, string full, string name, string folder, string description)
    {
        CommandLine line = CommandLine.Parse(catalog, run.Args);
        bool encrypted = name is "encrypted" or "stdin";
        if (encrypted && (invocation.Password is not null || !line.Command.Has(name == "stdin" ? PasswordStdinOption : PasswordOption)
            || run.Args.Any(static arg => arg.StartsWith("--password", StringComparison.Ordinal))))
        {
            return null; // The input already opens with a password, or the command takes none.
        }
        if (name == "stdin" && PipedInput(run, run.Args) is not null)
        {
            return null; // Stdin holds the invocation's document.
        }
        if (name == "spaced" && input.Contains(' ', StringComparison.Ordinal))
        {
            return null; // The input's path already holds a space.
        }
        using var scratch = new Scratch(run);
        string source = Path.Combine(scratch.Workspace, Path.GetRelativePath(run.Workspace, full));
        string directory = Path.Combine(scratch.Workspace, folder);
        Directory.CreateDirectory(directory);
        string variant = Path.Combine(directory, Path.GetFileName(full));
        var variables = new Dictionary<string, string?>(run.Variables ?? new Dictionary<string, string?>(), StringComparer.OrdinalIgnoreCase);
        if (encrypted)
        {
            if (!Encrypt(catalog, scratch, source, variant))
            {
                return null; // The input's format carries no password.
            }
            variables[VariantVariable] = VariantPassword;
        }
        else
        {
            File.Copy(source, variant);
        }

        // The invocation again, in the copy: its input replaced by the variant, named as the
        // invocation named it, and its outputs, which the copy already holds, written beside it.
        string named = Path.IsPathFullyQualified(input) ? variant : Path.GetRelativePath(scratch.Workspace, variant);
        var args = new List<string>();
        bool replaced = false;
        for (int index = 0; index < run.Args.Count; index++)
        {
            string arg = run.Args[index];
            if (!replaced && arg == input)
            {
                args.Add(named);
                replaced = true;
            }
            else if (index > 0 && run.Args[index - 1] is "--out" or "-o" or "--out-dir")
            {
                args.Add(Path.Combine(scratch.Workspace, VariantOutputFolder, Path.GetFileName(Path.TrimEndingDirectorySeparator(arg))));
            }
            else
            {
                args.Add(scratch.Resolve(Logical(run, arg)));
            }
        }
        if (!replaced)
        {
            return null;
        }
        Directory.CreateDirectory(Path.Combine(scratch.Workspace, VariantOutputFolder));
        if (name == "encrypted")
        {
            args.AddRange([PasswordOption, VariantVariable]);
        }
        else if (name == "stdin")
        {
            args.Add(PasswordStdinOption);
        }
        if (scratch.Execute(args, variables, name == "stdin" ? VariantPassword + "\n" : PipedInput(run, args)) is not { } result)
        {
            return null;
        }

        // The commands that name the variant's input, judged in the copy as the invocation's own
        // are; a password read from stdin is passed on in the variable that holds it, never on stdin.
        var variantRun = name == "stdin"
            ? new HintRun(args, scratch.Workspace, scratch.Config, variables, PasswordVariable: VariantVariable)
            : new HintRun(args, scratch.Workspace, scratch.Config, variables, run.StandardInput);
        JsonNode? document = Parse(string.IsNullOrWhiteSpace(result.StdOut) ? result.StdErr : result.StdOut);
        var problems = new List<string>();
        foreach ((string code, string hint) in Hints(document))
        {
            foreach (HintAction action in Extract(catalog, hint).Where(action => action.Kind == HintKind.Command && action.Text.Contains(folder, StringComparison.OrdinalIgnoreCase)))
            {
                Judgement judgement = Plan(catalog, variantRun, document, action);
                if (Outcomes.GetOrAdd(judgement.Key, _ => new Lazy<string?>(judgement.Judge)).Value is { } problem)
                {
                    problems.Add($"the {code} hint {action} of the invocation {description}: {problem}"
                        .Replace(scratch.Workspace, run.Workspace, StringComparison.OrdinalIgnoreCase));
                }
            }
        }
        return problems.Count == 0 ? null : string.Join('\n', problems.Distinct(StringComparer.Ordinal));
    }

    /// <summary>A sentence that says to run the invocation's command again with options, up to the end of its clause.</summary>
    [GeneratedRegex(@"(?i)\brun(?:ning)? (?:the|this) command again with (?<clause>[^,.;]+)")]
    private static partial Regex RunAgain();

    /// <summary>An option in such a clause, with the variable it names when an upper-case name follows it.</summary>
    [GeneratedRegex(@"(?<![\w-])(--[a-z][a-z0-9-]*)(?:\s+([A-Z][A-Z0-9_]*)(?![\w-]))?")]
    private static partial Regex AgainOption();

    /// <summary>
    /// Writes <paramref name="target"/>, a copy of <paramref name="source"/> encrypted with the
    /// variant's password, with the product's own commands: a PDF by its <c>encrypt_document</c>
    /// operation, any other format by <c>convert --encrypt-env</c> to its own format. False when
    /// no product reads it or its format carries no password.
    /// </summary>
    private static bool Encrypt(CliCatalog catalog, Scratch scratch, string source, string target)
    {
        if (catalog.ReaderOf(source) is not { } product)
        {
            return false;
        }
        string format = Path.GetExtension(source).TrimStart('.').ToLowerInvariant();
        string[] args = product == "pdf"
            ? ["pdf", "edit", source, "--ops", $$"""{"ops":[{"op":"encrypt_document","userPasswordEnv":"{{VariantVariable}}","ownerPasswordEnv":"{{VariantVariable}}"}]}""", "--out", target, "--output-format", "json"]
            : [product, "convert", source, "--to", format, "--out", target, "--encrypt-env", VariantVariable, "--output-format", "json"];
        return scratch.Execute(args, new Dictionary<string, string?> { [VariantVariable] = VariantPassword }) is { ExitCode: 0 } && File.Exists(target);
    }
}
