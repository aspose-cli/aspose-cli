using System.Text.Json.Nodes;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Invariants;

/// <summary>
/// One generated invariant case: a scenario whose expectations state one invariant for one
/// command, option, operation or format pair, or a <see cref="Check"/> of the command tree that
/// the live catalog alone answers, such as one option name across every command. <see cref="Slow"/>
/// cases complete a matrix whose unmarked sample already covers every command and product; a slow
/// case that a known violation names still runs unmarked.
/// </summary>
internal sealed record InvariantCase(
    string Id,
    string Invariant,
    string Shard,
    bool Slow,
    Scenario? Scenario,
    Func<IReadOnlyList<ScenarioProblem>>? Check = null);

/// <summary>
/// Generates the invariant cases from the live CLI catalog (<see cref="CliCatalog"/>): nothing
/// here lists a command, option, operation or format by hand. A case id reads
/// <c>&lt;invariant&gt; | &lt;subject&gt;</c>; the shard groups the cases of one product, of the
/// platform commands, or of one product's format matrix, so the shards run side by side.
/// </summary>
internal static class InvariantCases
{
    public const string MissingInput = "missing-input";
    public const string CorruptInput = "corrupt-input";
    public const string UnknownOption = "unknown-option";
    public const string UnknownCommand = "unknown-command";
    public const string UnknownOp = "unknown-op";
    public const string UnknownField = "unknown-field";
    public const string InvalidEnum = "invalid-enum";
    public const string UnwritableOutput = "unwritable-output";
    public const string ReadOnly = "read-only";
    public const string DryRun = "dry-run";
    public const string OutputReopens = "output-reopens";
    public const string SecretHidden = "secret-hidden";
    public const string EvaluationDisclosed = "evaluation-disclosed";
    public const string SameNameOption = "same-name-option";
    public const string DefaultNotRestated = "default-not-restated";
    public const string Platform = "platform";

    /// <summary>The warning that discloses evaluation output (<c>aspose-cli docs licensing</c>).</summary>
    private const string EvaluationWarning = "EVAL_MODE";

    /// <summary>The shard of the slow part of every product's format matrix.</summary>
    public const string FormatMatrix = "formats";

    private const string SecretVariable = "INVARIANT_SECRET";

    /// <summary>A password that opens no fixture; as a secret, it must never be printed either.</summary>
    private const string WrongPassword = "Invariant-Wrong-Secret-3b8d";
    private const string CorruptContent = "This is not a document.\n";

    private static readonly Lazy<IReadOnlyDictionary<string, InvariantCase>> Generated = new(Generate);

    /// <summary>Every invariant a known violation may name.</summary>
    public static IReadOnlySet<string> Invariants { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        ScenarioContract.NoInternalError, ScenarioContract.ErrorEnvelope, ScenarioContract.ResultEnvelope,
        MissingInput, CorruptInput, UnknownOption, UnknownCommand, UnknownOp, UnknownField, InvalidEnum,
        UnwritableOutput, ReadOnly, DryRun, OutputReopens, SecretHidden, EvaluationDisclosed, SameNameOption,
        DefaultNotRestated,
    };

    public static IReadOnlyDictionary<string, InvariantCase> All => Generated.Value;

    /// <summary>The unmarked cases of a shard.</summary>
    public static TheoryData<string> Sample(string shard) => Data(shard, static item => !item.Slow);

    /// <summary>The slow cases of a shard that no known violation names.</summary>
    public static TheoryData<string> Exhaustive(string shard) => Data(shard, static item => item.Slow && !IsListed(item));

    /// <summary>
    /// The slow cases of a shard that a known violation names, in any license mode: they run
    /// unmarked, so every run checks every entry that applies to its mode.
    /// </summary>
    public static TheoryData<string> Listed(string shard) => Data(shard, static item => item.Slow && IsListed(item));

    private static bool IsListed(InvariantCase item) => KnownViolations.Entries.Any(entry => entry.Case == item.Id);

    private static TheoryData<string> Data(string shard, Func<InvariantCase, bool> include) =>
        [.. All.Values.Where(item => item.Shard == shard && include(item)).Select(static item => item.Id)];

    private static IReadOnlyDictionary<string, InvariantCase> Generate()
    {
        CliCatalog catalog = CliCatalog.Current;
        var cases = new List<InvariantCase>();
        CliCommand[] visible = [.. catalog.Commands.Where(static command => !command.Hidden)];
        CliCommand[] runnable = [.. visible.Where(IsRunnable)];
        foreach (CliCommand command in runnable)
        {
            cases.Add(MissingInputCase(command));
            cases.Add(CorruptInputCase(catalog, command));
            cases.AddRange(ReadOnlyCases(command));
            cases.AddRange(DryRunCases(command));
        }
        cases.AddRange(SecretCases(runnable));
        foreach (CliCommand command in visible)
        {
            cases.AddRange(UnknownOptionCases(command));
            cases.AddRange(UnknownCommandCases(catalog, command));
            cases.AddRange(InvalidEnumCases(command));
            cases.AddRange(EvaluationDisclosedCases(command));
        }
        foreach (CliProduct product in catalog.Products.Where(static product => ScenarioFixtures.Products.Contains(product.Id)))
        {
            cases.AddRange(OperationCases(catalog, product));
            cases.AddRange(OperationSecretCases(catalog, product));
            cases.AddRange(UnwritableOutputCases(catalog, product));
            cases.AddRange(OutputReopensCases(catalog, product));
        }
        cases.AddRange(SameNameOptions.Cases(catalog));
        cases.AddRange(RestatedDefaults.Cases(catalog));
        return cases.ToDictionary(static item => item.Id, StringComparer.Ordinal);
    }

    /// <summary>A command that reads a document it is given: it has a required file argument.</summary>
    private static bool IsRunnable(CliCommand command) =>
        command.Arguments.Any(static argument => argument.Required && argument.InputKind == "file");

    private static string ShardOf(CliCommand command) => command.Product ?? Platform;

    // ----- (b) failures: a missing and a corrupt input --------------------------------------

    private static InvariantCase MissingInputCase(CliCommand command)
    {
        Invocation invocation = Invocation.For(command);
        string missing = "missing" + Path.GetExtension(invocation.Input!);
        invocation.Files.Remove(invocation.Input!);
        invocation.Replace(invocation.Input!, missing);
        return Case(MissingInput, $"{command}", ShardOf(command), slow: false, invocation, new ScenarioExpectation
        {
            Error = ["FILE_NOT_FOUND"],
            Files = new ScenarioFiles { NothingWritten = true },
        });
    }

    private static InvariantCase CorruptInputCase(CliCatalog catalog, CliCommand command)
    {
        Invocation invocation = Invocation.For(command);
        invocation.Files[invocation.Input!] = new ScenarioFile { Text = CorruptContent };
        // A refusal of the input itself: the exit codes of the catalog's input and format errors.
        int[] refusals = [.. ExitCodesOf(catalog, "input", "format")];
        return Case(CorruptInput, $"{command}", ShardOf(command), slow: false, invocation, new ScenarioExpectation
        {
            ExitCode = refusals,
            Files = new ScenarioFiles { NothingWritten = true },
        });
    }

    // ----- (c) mistakes carry a suggestion and write nothing --------------------------------

    private static IEnumerable<InvariantCase> UnknownOptionCases(CliCommand command)
    {
        string[] names = [.. command.Options.Select(static option => option.Name.TrimStart('-'))];
        // A hidden option is never suggested, so a typo of one has no suggestion to check.
        CliOption[] visible = [.. command.Options.Where(static option => !option.Hidden)];
        for (int index = 0; index < visible.Length; index++)
        {
            CliOption option = visible[index];
            string typo = "--" + Typo(option.Name.TrimStart('-'), names);
            yield return Case(UnknownOption, $"{command} {option.Name} as {typo}", ShardOf(command),
                slow: index > 0 && command.Words.Length > 0,
                new Invocation([.. command.Tokens, typo]),
                new ScenarioExpectation
                {
                    ExitCode = [2],
                    Error = ["USAGE_ERROR"],
                    Json = [new JsonAssertion { Path = "error.details.suggestions", Contains = option.Name }],
                    Files = new ScenarioFiles { NothingWritten = true },
                });
        }
    }

    private static IEnumerable<InvariantCase> UnknownCommandCases(CliCatalog catalog, CliCommand command)
    {
        string[] names = [.. command.Subcommands.Where(name => !catalog.Command(Join(command.Words, name)).Hidden)];
        foreach (string name in names)
        {
            string typo = Typo(name, names);
            yield return Case(UnknownCommand, $"{Join(command.Words, name)} as {typo}", ShardOf(command), slow: false,
                new Invocation([.. command.Tokens, typo]),
                new ScenarioExpectation
                {
                    ExitCode = [2],
                    Error = ["USAGE_ERROR"],
                    Json = [new JsonAssertion { Path = "error.details.suggestions", Contains = Join(command.Words, name) }],
                    Files = new ScenarioFiles { NothingWritten = true },
                });
        }
    }

    private static IEnumerable<InvariantCase> InvalidEnumCases(CliCommand command)
    {
        // An invalid --output would change the output the checks read, so it is left out.
        foreach (CliOption option in command.Options.Where(static option =>
            option.AllowedValues.Count > 0 && option.Type != "boolean" && option.Name != "--output"))
        {
            string value = option.AllowedValues[0];
            string typo = Typo(value, option.AllowedValues);
            yield return Case(InvalidEnum, $"{command} {option.Name} {typo}", ShardOf(command), slow: false,
                new Invocation([.. command.Tokens, option.Name, typo]),
                new ScenarioExpectation
                {
                    ExitCode = [2],
                    Error = ["USAGE_ERROR", "OPTION_INVALID"],
                    Json = [new JsonAssertion { Path = "error.details.suggestions", Contains = value }],
                    Files = new ScenarioFiles { NothingWritten = true },
                });
        }
    }

    private static IEnumerable<InvariantCase> OperationCases(CliCatalog catalog, CliProduct product)
    {
        if (product.OpsSchemaId is null || product.Ops.Count == 0)
        {
            yield break;
        }
        JsonNode definitions = JsonNode.Parse(catalog.SchemaText(product.OpsSchemaId))!["$defs"]!;
        string extension = ScenarioFixtures.PrimaryFormat(product.Id);
        string input = "input." + extension;
        int[] sample = [0, product.Ops.Count / 2, product.Ops.Count - 1];
        for (int index = 0; index < product.Ops.Count; index++)
        {
            string op = product.Ops[index];
            bool slow = !sample.Contains(index);
            string typo = Typo(op, product.Ops);
            yield return Case(UnknownOp, $"{product.Id} {op} as {typo}", product.Id, slow,
                Edit(product.Id, input, extension, new JsonObject { ["op"] = typo }),
                new ScenarioExpectation
                {
                    ExitCode = [4],
                    Error = ["OPS_INVALID"],
                    Json = [new JsonAssertion { Path = "error.details.suggestions", Contains = op }],
                    Files = new ScenarioFiles { NothingWritten = true },
                });

            string[] fields = definitions[op]?["properties"] is JsonObject properties
                ? [.. properties.Select(static property => property.Key).Where(static key => key is not ("id" or "op"))]
                : [];
            if (fields.Length == 0)
            {
                continue;
            }
            string field = fields[0];
            string fieldTypo = Typo(field, fields);
            yield return Case(UnknownField, $"{product.Id} {op}.{field} as {fieldTypo}", product.Id, slow,
                Edit(product.Id, input, extension, new JsonObject { ["op"] = op, [fieldTypo] = "x" }),
                new ScenarioExpectation
                {
                    ExitCode = [4],
                    Error = ["OPS_INVALID"],
                    Json = [new JsonAssertion { Path = "error.details.suggestions", Contains = field }],
                    Files = new ScenarioFiles { NothingWritten = true },
                });
        }
    }

    private static Invocation Edit(string product, string input, string extension, JsonObject operation)
    {
        var invocation = new Invocation([product, "edit", input, "--ops", "ops.json", "--dry-run"]);
        invocation.Files[input] = new ScenarioFile { Fixture = $"{product}.{extension}" };
        invocation.Files["ops.json"] = new ScenarioFile { Text = new JsonObject { ["ops"] = new JsonArray(operation) }.ToJsonString() };
        return invocation;
    }

    // ----- (d) an output the command cannot write is refused --------------------------------

    private static IEnumerable<InvariantCase> UnwritableOutputCases(CliCatalog catalog, CliProduct product)
    {
        int[] refusals = [.. ExitCodesOf(catalog, "usage", "format")];
        ScenarioExpectation Refused() => new() { ExitCode = refusals, Files = new ScenarioFiles { NothingWritten = true } };
        string own = ScenarioFixtures.PrimaryFormat(product.Id);
        string[] writable = [.. product.LoadFormats, .. product.ConvertFormats, .. product.RenderFormats, "jpg"];
        // One foreign extension per other product: the first format it loads that this product
        // neither reads nor writes.
        string[] foreign =
        [
            .. catalog.Products.Where(other => other.Id != product.Id)
                .Select(other => other.LoadFormats.FirstOrDefault(format => !writable.Contains(format)))
                .OfType<string>(),
        ];
        if (foreign.Length == 0)
        {
            yield break;
        }
        CliCommand convert = catalog.Command($"{product.Id} convert");
        IReadOnlyList<string> targets = convert.Option("--to")!.AllowedValues;
        string target = targets.Contains(own) ? own : targets[0];
        foreach (string extension in foreign)
        {
            yield return Case(UnwritableOutput, $"{product.Id} convert --to {target} --out out.{extension}", product.Id, slow: false,
                Invocation.For(convert, "--to").Add("--to", target, "--out", "out." + extension), Refused());
        }
        foreach (string to in targets.Where(to => to != target))
        {
            yield return Case(UnwritableOutput, $"{product.Id} convert --to {to} --out out.{foreign[0]}", product.Id, slow: true,
                Invocation.For(convert, "--to").Add("--to", to, "--out", "out." + foreign[0]), Refused());
        }
        if (catalog.Commands.FirstOrDefault(command => command.Words == $"{product.Id} render") is { } render)
        {
            string image = render.Option("--to")!.AllowedValues[0];
            yield return Case(UnwritableOutput, $"{product.Id} render --to {image} --out out.{foreign[0]}", product.Id, slow: false,
                Invocation.For(render).Add("--to", image, "--out", "out." + foreign[0]), Refused());
        }
        if (catalog.Commands.FirstOrDefault(command => command.Words == $"{product.Id} edit") is { } edit && edit.Has("--out"))
        {
            yield return Case(UnwritableOutput, $"{product.Id} edit --out out.{foreign[0]}", product.Id, slow: false,
                Invocation.For(edit).Add("--out", "out." + foreign[0]), Refused());
        }
        if (catalog.Commands.FirstOrDefault(command => command.Words == $"{product.Id} create") is { } create)
        {
            Invocation invocation = Invocation.For(create);
            invocation.Replace("created." + own, "out." + foreign[0]);
            yield return Case(UnwritableOutput, $"{product.Id} create out.{foreign[0]}", product.Id, slow: false, invocation, Refused());
        }
    }

    // ----- (e) read-only commands succeed and write nothing; dry runs write nothing and ------
    // ----- report no output, while the same edit run for real reports the file it wrote ----

    private static IEnumerable<InvariantCase> ReadOnlyCases(CliCommand command)
    {
        bool review = command.Verb == "review";
        if (!review && (command.Has("--out") || command.Has("--out-dir") || command.Has("--in-place")))
        {
            yield break;
        }
        ScenarioExpectation Unchanged(Invocation invocation) => new()
        {
            // The smallest valid invocation of a read-only command succeeds, so a check that
            // nothing was written is never met by a command that failed before it could write.
            ExitCode = [0],
            // Review writes its evidence next to the input; every other read-only command writes nothing.
            Files = review
                ? new ScenarioFiles { Unchanged = [.. invocation.Files.Keys] }
                : new ScenarioFiles { NothingWritten = true },
        };
        Invocation plain = Invocation.For(command);
        yield return Case(ReadOnly, $"{command}", ShardOf(command), slow: false, plain, Unchanged(plain));
        foreach (string detail in command.Option("--detail")?.AllowedValues ?? [])
        {
            Invocation detailed = Invocation.For(command).Add("--detail", detail);
            yield return Case(ReadOnly, $"{command} --detail {detail}", ShardOf(command), slow: false, detailed, Unchanged(detailed));
        }
    }

    private static IEnumerable<InvariantCase> DryRunCases(CliCommand command)
    {
        if (!command.Has("--dry-run") || command.Product is not { } product)
        {
            yield break;
        }
        string extension = ScenarioFixtures.PrimaryFormat(product);
        (string Subject, string[] Extra)[] variants =
        [
            ("", []),
            (" --in-place", ["--in-place"]),
            ($" --out out.{extension}", ["--out", "out." + extension]),
        ];
        foreach ((string subject, string[] extra) in variants)
        {
            Invocation invocation = Invocation.For(command).Add("--dry-run").Add(extra);
            yield return Case(DryRun, $"{command} --dry-run{subject}", product, slow: false, invocation, new ScenarioExpectation
            {
                ExitCode = [0],
                Json =
                [
                    new JsonAssertion { Path = "dryRun", Value = true },
                    // A dry run publishes nothing, so its result names no output, even for --out or --in-place.
                    new JsonAssertion { Path = "output", Exists = false },
                ],
                Files = new ScenarioFiles { NothingWritten = true },
            });
        }
        // The same edit run for real says it was not a dry run and names the file it published.
        if (command.Has("--out"))
        {
            string output = "out." + extension;
            yield return Case(DryRun, $"{command} --out {output}", product, slow: false, Invocation.For(command).Add("--out", output), Published(output));
        }
        if (command.Has("--in-place"))
        {
            Invocation inPlace = Invocation.For(command).Add("--in-place");
            yield return Case(DryRun, $"{command} --in-place", product, slow: false, inPlace, Published(inPlace.Input!));
        }

        static ScenarioExpectation Published(string path) => new()
        {
            ExitCode = [0],
            Json =
            [
                new JsonAssertion { Path = "dryRun", Value = false },
                new JsonAssertion { Path = "output.path", Exists = true },
            ],
            Files = new ScenarioFiles { Present = [path] },
        };
    }

    // ----- (f) every declared conversion opens again ----------------------------------------

    private static IEnumerable<InvariantCase> OutputReopensCases(CliCatalog catalog, CliProduct product)
    {
        string shard = product.Id + "-formats";
        string own = ScenarioFixtures.PrimaryFormat(product.Id);
        bool converts = catalog.Commands.Any(command => command.Words == $"{product.Id} convert");
        bool renders = catalog.Commands.Any(command => command.Words == $"{product.Id} render");
        // The sample converts the product's own document to every declared format and renders it
        // to every image format; the slow part converts a seed of every other format the product
        // both reads and writes back to its own format and to PDF, so every input format and
        // every output format is exercised without the full matrix.
        if (converts)
        {
            foreach (string target in product.ConvertFormats)
            {
                yield return Conversion(product.Id, "convert", own, target, shard, slow: false);
            }
        }
        if (renders)
        {
            foreach (string target in product.RenderFormats)
            {
                yield return Conversion(product.Id, "render", own, target, shard, slow: false);
            }
        }
        if (!converts)
        {
            yield break;
        }
        string[] backTo = [.. new[] { own, "pdf" }.Where(product.ConvertFormats.Contains).Distinct()];
        foreach (string source in product.LoadFormats.Where(format => format != own && product.ConvertFormats.Contains(format)))
        {
            foreach (string target in backTo)
            {
                yield return Conversion(product.Id, "convert", source, target, FormatMatrix, slow: true);
            }
        }
    }

    private static InvariantCase Conversion(string product, string verb, string source, string target, string shard, bool slow)
    {
        string input = "input." + source;
        var invocation = new Invocation([product, verb, input, "--to", target]);
        invocation.Files[input] = new ScenarioFile { Fixture = $"{product}.{source}" };
        return Case(OutputReopens, $"{product} {verb} {source} -> {target}", shard, slow, invocation, new ScenarioExpectation
        {
            ExitCode = [0],
            Reopens = ["@result"],
        });
    }

    // ----- (g) secret values are never printed ----------------------------------------------

    /// <summary>
    /// Every secret option, with the password that opens what it protects and, for an option that
    /// opens something, with a wrong one: the command succeeds or refuses the password, and
    /// prints neither. An <c>--encrypt</c> option protects the output; any other secret opens the
    /// signing certificate, the right document of a comparison, or the first input.
    /// </summary>
    private static IEnumerable<InvariantCase> SecretCases(IEnumerable<CliCommand> commands)
    {
        var sampled = new HashSet<(string Shard, string Option, bool Right)>();
        foreach (CliCommand command in commands)
        {
            foreach (CliOption option in command.Options.Where(static option => option.Secret))
            {
                bool encrypts = option.Name.StartsWith("--encrypt", StringComparison.Ordinal);
                foreach (bool right in encrypts ? [true] : new[] { true, false })
                {
                    Invocation invocation = Invocation.For(command, option.Name);
                    string? refusal = encrypts ? null : Protect(invocation, option);
                    string secret = right ? ScenarioFixtures.Password : WrongPassword;
                    string? stdin = Pass(invocation, option, secret);
                    bool slow = !sampled.Add((ShardOf(command), option.Name, right));
                    yield return Case(SecretHidden, $"{command} {option.Name}{(right ? string.Empty : " with a wrong password")}",
                        ShardOf(command), slow, invocation,
                        right
                            ? new ScenarioExpectation { ExitCode = [0], Hidden = [secret] }
                            : new ScenarioExpectation { Error = [refusal!], Files = new ScenarioFiles { NothingWritten = true }, Hidden = [secret] },
                        stdin);
                }
            }
        }
    }

    /// <summary>Replaces the document a password option opens with its encrypted fixture; returns the code that refuses a wrong password.</summary>
    private static string Protect(Invocation invocation, CliOption option)
    {
        if (option.Name.Contains("certificate", StringComparison.Ordinal))
        {
            return "SIGN_CERT_INVALID"; // certificate.pfx is protected by the fixture password already.
        }
        string target = invocation.Inputs[option.Name.StartsWith("--right-", StringComparison.Ordinal) ? 1 : 0];
        string fixture = invocation.Files[target].Fixture!;
        invocation.Files[target] = new ScenarioFile { Fixture = fixture[..fixture.IndexOf('.', StringComparison.Ordinal)] + ".encrypted" };
        return "PASSWORD_INVALID";
    }

    /// <summary>Passes a secret the way the option reads it; returns the standard input, if any.</summary>
    private static string? Pass(Invocation invocation, CliOption option, string secret)
    {
        switch (option.ValueSource)
        {
            case "environment-variable-name":
                invocation.Add(option.Name, SecretVariable);
                invocation.Env[SecretVariable] = secret;
                return null;
            case "stdin":
                invocation.Add(option.Name);
                return secret;
            default:
                invocation.Add(option.Name, secret);
                return null;
        }
    }

    /// <summary>
    /// Every operation with a secret field, a property whose name ends in <c>Env</c> and names
    /// the variable that holds the secret: an edit that sets each such field to a variable
    /// holding the fixture password, and any other required field to the first value of its
    /// enum, never prints the password. It succeeds, or refuses the document's state with an
    /// input error, such as an unprotect of a document that has no protection; the operation
    /// itself is valid. Operations that need other input are left out.
    /// </summary>
    private static IEnumerable<InvariantCase> OperationSecretCases(CliCatalog catalog, CliProduct product)
    {
        if (product.OpsSchemaId is null)
        {
            yield break;
        }
        JsonNode definitions = JsonNode.Parse(catalog.SchemaText(product.OpsSchemaId))!["$defs"]!;
        string extension = ScenarioFixtures.PrimaryFormat(product.Id);
        string input = "input." + extension;
        int[] outcomes = [0, .. ExitCodesOf(catalog, "input")];
        foreach (string op in product.Ops)
        {
            if (definitions[op]?["properties"] is not JsonObject properties)
            {
                continue;
            }
            string[] secrets = [.. properties.Select(static property => property.Key).Where(static key => key.EndsWith("Env", StringComparison.Ordinal))];
            var operation = new JsonObject { ["op"] = op };
            foreach (string secret in secrets)
            {
                operation[secret] = SecretVariable;
            }
            string[] others =
            [
                .. (definitions[op]!["required"] as JsonArray ?? []).Select(static name => name!.GetValue<string>())
                    .Where(name => name != "op" && !secrets.Contains(name)),
            ];
            foreach (string name in others)
            {
                if (properties[name]?["enum"] is JsonArray { Count: > 0 } values)
                {
                    operation[name] = values[0]!.DeepClone();
                }
            }
            if (secrets.Length == 0 || others.Any(name => !operation.ContainsKey(name)))
            {
                continue;
            }
            var invocation = new Invocation([product.Id, "edit", input, "--ops", "ops.json", "--out", "output." + extension]);
            invocation.Files[input] = new ScenarioFile { Fixture = $"{product.Id}.{extension}" };
            invocation.Files["ops.json"] = new ScenarioFile { Text = new JsonObject { ["ops"] = new JsonArray(operation) }.ToJsonString() };
            invocation.Env[SecretVariable] = ScenarioFixtures.Password;
            yield return Case(SecretHidden, $"{product.Id} edit {op}", product.Id, slow: false, invocation,
                new ScenarioExpectation { ExitCode = outcomes, Hidden = [ScenarioFixtures.Password] });
        }
    }

    // ----- (h) every successful write discloses evaluation mode -----------------------------

    /// <summary>
    /// Every product command that writes a document, run as its smallest valid invocation: in
    /// evaluation mode it succeeds and its result warns <see cref="EvaluationWarning"/>, so the
    /// marks the engine saved into the output are disclosed; with a license it does not claim them.
    /// Review writes evidence, not a deliverable, and is left out.
    /// </summary>
    private static IEnumerable<InvariantCase> EvaluationDisclosedCases(CliCommand command)
    {
        bool writes = command.Verb == "create" || command.Has("--out") || command.Has("--out-dir") || command.Has("--in-place");
        if (command.Product is not { } product || command.Verb == "review" || !writes)
        {
            yield break;
        }
        Invocation invocation = Invocation.For(command);
        if (command.Has("--out") && !command.Has("--to") && !invocation.Args.Contains("--out", StringComparer.Ordinal))
        {
            // Without --to, an optional --out is what makes the command write: an edit writes a new
            // file rather than its input, and a compare writes its redline.
            invocation.Add("--out", "output." + ScenarioFixtures.PrimaryFormat(product));
        }
        yield return Case(EvaluationDisclosed, $"{command}", product, slow: false, invocation, new ScenarioExpectation
        {
            ExitCode = [0],
            Warnings = ScenarioLicense.Licensed
                ? new ScenarioCodes { Absent = [EvaluationWarning] }
                : new ScenarioCodes { Present = [EvaluationWarning] },
        });
    }

    // ----- helpers ---------------------------------------------------------------------------

    private static InvariantCase Case(
        string invariant,
        string subject,
        string shard,
        bool slow,
        Invocation invocation,
        ScenarioExpectation expect,
        string? stdin = null)
    {
        string id = $"{invariant} | {subject}";
        var scenario = new Scenario
        {
            Name = id,
            Files = new Dictionary<string, ScenarioFile>(invocation.Files),
            Steps =
            [
                new ScenarioStep
                {
                    Args = [.. invocation.Args],
                    Env = invocation.Env.Count == 0 ? null : new Dictionary<string, string>(invocation.Env),
                    Stdin = stdin,
                    Expect = expect,
                },
            ],
        };
        return new InvariantCase(id, invariant, shard, slow, scenario);
    }

    private static IEnumerable<int> ExitCodesOf(CliCatalog catalog, params string[] categories) =>
        catalog.Document["diagnostics"]!.AsArray()
            .Where(diagnostic => categories.Contains(diagnostic!["category"]!.GetValue<string>()))
            .Select(static diagnostic => diagnostic!["exitCode"]?.GetValue<int>())
            .OfType<int>()
            .Distinct()
            .Order();

    private static string Join(string words, string name) => words.Length == 0 ? name : $"{words} {name}";

    /// <summary>
    /// A typing slip of a name: its second and third letters swapped, or, for a short name or one
    /// where the swap changes nothing or names a sibling, its last letter doubled.
    /// </summary>
    internal static string Typo(string name, IEnumerable<string> siblings)
    {
        if (name.Length >= 6 && name[1] != name[2])
        {
            string swapped = string.Concat(name[0].ToString(), name[2].ToString(), name[1].ToString(), name[3..]);
            if (!siblings.Contains(swapped, StringComparer.OrdinalIgnoreCase))
            {
                return swapped;
            }
        }
        return name + name[^1];
    }
}
