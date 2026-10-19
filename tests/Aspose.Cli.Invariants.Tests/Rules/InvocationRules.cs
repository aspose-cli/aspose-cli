using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;
using static Aspose.Cli.Invariants.Tests.InvocationChecks;

namespace Aspose.Cli.Invariants.Tests;

// How a command run behaves: streams, exit codes, stdin, selections and mistakes. Helpers carry
// the number of the rule they serve, since the other rule files share this partial class.
internal static partial class ContractRules
{
    /// <summary>A secret written to standard input, which no stream may repeat.</summary>
    private const string R10Secret = "Invariant-Stdin-Secret-7c2a";

    /// <summary>A selection beyond every fixture document, which has a few units at most.</summary>
    private const string R16Beyond = "99";

    /// <summary>The selection options and the malformed value of each (<c>--range</c> takes A1 notation).</summary>
    private static readonly Dictionary<string, string> R16Options = new(StringComparer.Ordinal)
    {
        ["--pages"] = "1-x",
        ["--slides"] = "1-x",
        ["--sheets"] = "1-x",
        ["--blocks"] = "1-x",
        ["--range"] = "A1:!!",
    };

    /// <summary>
    /// Every group command run without a subcommand: in table mode it exits 2 and prints its help
    /// to stderr, and in JSON mode its <c>USAGE_ERROR</c> lists the visible subcommands in
    /// <c>details.available</c> and no <c>details.errors</c>. A group other than the root with
    /// options or arguments of its own would run an action of its own, which C-R03's declared
    /// part reports instead.
    /// </summary>
    private static IEnumerable<InvariantCase> R03Run(CliCatalog catalog)
    {
        string output = OutputOption(catalog);
        foreach (CliCommand group in catalog.Commands.Where(static command => !command.Hidden && command.Subcommands.Count > 0
            && (command.Words.Length == 0 || (command.Options.Count == 0 && command.Arguments.Count == 0))))
        {
            string[] subcommands = [.. group.Subcommands.Select(name => Join(group.Words, name)).Where(path => !catalog.Command(path).Hidden)];
            yield return Runs("C-R03", $"{group} without a subcommand", InvariantCases.ShardOf(group), slow: false,
                new Invocation(group.Tokens), session => R03Problems(session, output, subcommands));
        }
    }

    private static IEnumerable<string> R03Problems(RuleWorkspace session, string output, string[] subcommands)
    {
        string help = Normalize(session.Run("--help").StdOut);
        CliResult table = session.Run(output, "table");
        if (table.ExitCode != 2)
        {
            yield return $"table mode exits {table.ExitCode}, expected 2";
        }
        if (table.StdOut.Length > 0)
        {
            yield return $"table mode writes to stdout: {Excerpt(table.StdOut)}";
        }
        if (help.Length == 0 || !Normalize(table.StdErr).Contains(help, StringComparison.Ordinal))
        {
            yield return $"table mode does not print the help to stderr: {Excerpt(table.StdErr)}";
        }
        CliResult json = session.Run(output, "json");
        if (json.ExitCode != 2)
        {
            yield return $"JSON mode exits {json.ExitCode}, expected 2";
        }
        JsonNode? error = ParseJson(json.StdErr)?["error"];
        if (error?["code"]?.GetValue<string>() is not "USAGE_ERROR")
        {
            yield return $"JSON mode fails with {error?["code"]?.ToJsonString() ?? "no error"}, expected USAGE_ERROR";
        }
        JsonNode? details = error?["details"];
        // The missing subcommand is one problem, which the message states; details.errors lists several.
        if (details?["errors"] is { } errors)
        {
            yield return $"the error lists details.errors for one problem: {Excerpt(errors.ToJsonString())}";
        }
        if (Strings(details?["available"]) is not { } available)
        {
            yield return $"the error has no details.available; its details: {Excerpt(details?.ToJsonString() ?? "none")}";
            yield break;
        }
        foreach (string missing in subcommands.Except(available, StringComparer.Ordinal))
        {
            yield return $"details.available leaves out '{missing}'";
        }
        foreach (string extra in available.Except(subcommands, StringComparer.Ordinal))
        {
            yield return $"details.available lists '{extra}', which is not a visible subcommand";
        }
    }

    /// <summary>
    /// Every pair of standard-input sources of a command, a <c>--*-stdin</c> flag or a JSON
    /// source given as <c>-</c>, used together: the command refuses with <c>OPTION_INVALID</c>
    /// (exit 2), so it neither reads one source as the other nor repeats what stdin held.
    /// </summary>
    private static IEnumerable<InvariantCase> R10Run(CliCatalog catalog)
    {
        foreach (CliCommand command in catalog.Commands.Where(static command => !command.Hidden))
        {
            CliOption[] sources =
            [
                .. command.Options.Where(option => option.ValueSource == "stdin" || InputKind(catalog, command, option.Name) == "json-source"),
            ];
            if (sources.Length < 2)
            {
                continue;
            }
            string document = command.Product is { } product && ScenarioFixtures.Products.Contains(product)
                ? Encoding.UTF8.GetString(ScenarioFixtures.Read(product + ".ops"))
                : "{}";
            for (int first = 0; first < sources.Length; first++)
            {
                for (int second = first + 1; second < sources.Length; second++)
                {
                    CliOption[] pair = [sources[first], sources[second]];
                    Invocation invocation = Invocation.For(command, pair.FirstOrDefault(static option => option.ValueSource != "stdin")?.Name);
                    foreach (CliOption option in pair)
                    {
                        if (option.ValueSource == "stdin")
                        {
                            invocation.Add(option.Name);
                        }
                        else
                        {
                            invocation.Add(option.Name, "-");
                        }
                    }
                    yield return InvariantCases.Case("C-R10", $"{command} {pair[0].Name} with {pair[1].Name}", InvariantCases.ShardOf(command), slow: false,
                        invocation,
                        new ScenarioExpectation
                        {
                            ExitCode = [2],
                            Error = ["OPTION_INVALID"],
                            Hidden = [R10Secret],
                            Files = new ScenarioFiles { NothingWritten = true },
                        },
                        stdin: $"{R10Secret}\n{document}\n");
                }
            }
        }
    }

    /// <summary>
    /// <c>--to</c> follows <c>--out</c>: a convert or render without <c>--to</c> writes the format
    /// its <c>--out</c> extension names; both given must agree; with neither, a convert is a
    /// <c>USAGE_ERROR</c> and a render writes PNG.
    /// </summary>
    private static IEnumerable<InvariantCase> R13Run(CliCatalog catalog)
    {
        int[] usage = [.. InvariantCases.ExitCodesOf(catalog, "usage")];
        foreach (string product in ScenarioFixtures.Products)
        {
            string own = ScenarioFixtures.PrimaryFormat(product);
            if (catalog.Commands.FirstOrDefault(command => command.Words == $"{product} convert") is { } convert
                && convert.Option("--to") is { AllowedValues.Count: > 1 } to)
            {
                string target = to.AllowedValues.Contains("pdf") && own != "pdf" ? "pdf" : to.AllowedValues.First(value => value != own);
                yield return InvariantCases.Case("C-R13", $"{convert} --out out.{target} without --to", product, slow: false,
                    Invocation.For(convert, "--to").Add("--out", "out." + target),
                    new ScenarioExpectation { ExitCode = [0], Files = new ScenarioFiles { Present = ["out." + target] } });
                yield return InvariantCases.Case("C-R13", $"{convert} --to {target} --out out.{own}", product, slow: false,
                    Invocation.For(convert, "--to").Add("--to", target, "--out", "out." + own),
                    new ScenarioExpectation { ExitCode = usage, Files = new ScenarioFiles { NothingWritten = true } });
                yield return InvariantCases.Case("C-R13", $"{convert} without --to or --out", product, slow: false,
                    Invocation.For(convert, "--to"),
                    new ScenarioExpectation { ExitCode = usage, Error = ["USAGE_ERROR"], Files = new ScenarioFiles { NothingWritten = true } });
            }
            if (catalog.Commands.FirstOrDefault(command => command.Words == $"{product} render") is { } render
                && render.Option("--to") is { AllowedValues.Count: > 1 } image)
            {
                string other = image.AllowedValues.First(static value => value != "png");
                // A render writes every unit by default and the fixture may hold several, so it
                // selects the first, whose image is the output itself.
                string[] first = render.Options.FirstOrDefault(static option => option.Name is "--pages" or "--slides") is { } selection
                    ? [selection.Name, "1"]
                    : [];
                yield return InvariantCases.Case("C-R13", $"{render} --out out.{other} without --to", product, slow: false,
                    Invocation.For(render).Add(first).Add("--out", "out." + other),
                    new ScenarioExpectation { ExitCode = [0], Files = new ScenarioFiles { Present = ["out." + other] } });
                yield return InvariantCases.Case("C-R13", $"{render} without --to or --out", product, slow: false,
                    Invocation.For(render).Add(first),
                    new ScenarioExpectation { ExitCode = [0], Files = new ScenarioFiles { Present = ["input.png"] } });
            }
        }
    }

    /// <summary>
    /// <c>-</c> names only standard input: <c>--out -</c> is <c>OPTION_INVALID</c> with a hint
    /// to name a file, on every command that has <c>--out</c> (that a refusal writes nothing is
    /// the <c>unwritable-output</c> invariant's); and <c>--</c> ends the options, so a file whose
    /// name starts with a dash can follow it.
    /// </summary>
    private static IEnumerable<InvariantCase> R14(CliCatalog catalog)
    {
        foreach (CliCommand command in catalog.Commands.Where(static command => !command.Hidden && command.Has("--out")))
        {
            yield return InvariantCases.Case("C-R14", $"{command} --out -", InvariantCases.ShardOf(command), slow: false,
                Invocation.For(command, "--out").Add("--out", "-"),
                new ScenarioExpectation
                {
                    ExitCode = [2],
                    Error = ["OPTION_INVALID"],
                    Json = [new JsonAssertion { Path = "error.hint", Contains = "name a file" }],
                });
        }
        string output = OutputOption(catalog);
        foreach (string product in ScenarioFixtures.Products)
        {
            if (catalog.Commands.FirstOrDefault(command => command.Words == $"{product} inspect") is not { } inspect)
            {
                continue;
            }
            string format = ScenarioFixtures.PrimaryFormat(product);
            string file = "-input." + format;
            var invocation = new Invocation(inspect.Tokens);
            invocation.Files[file] = new ScenarioFile { Fixture = $"{product}.{format}" };
            yield return Runs("C-R14", $"{inspect} -- {file}", product, slow: false, invocation, session =>
            {
                CliResult result = session.Run(output, "json", "--", file);
                return result.ExitCode == 0 ? [] : [$"exit {result.ExitCode}: {Excerpt(result.StdErr)}"];
            });
        }
    }

    /// <summary>
    /// Every selection option of a command that reads a document: a unit beyond the document is
    /// a <c>*_NOT_FOUND</c> error (exit 4) with <c>requested</c> and <c>availableCount</c>, never
    /// a truncation; <c>all</c> selects everything; a malformed value is <c>OPTION_INVALID</c>
    /// (exit 2). A <c>--range</c> takes A1 notation, which has no unit count to exceed, so it is
    /// checked for <c>all</c> and a malformed value only. A malformed selection in an operation
    /// field is <c>OPS_INVALID</c> (exit 4), once per product.
    /// </summary>
    private static IEnumerable<InvariantCase> R16(CliCatalog catalog)
    {
        string[] notFound = [.. catalog.Diagnostics.Keys.Where(static code => code.EndsWith("_NOT_FOUND", StringComparison.Ordinal)).Order(StringComparer.Ordinal)];
        foreach (CliCommand command in catalog.Commands.Where(static command => !command.Hidden && command.Product is { } product
            && ScenarioFixtures.Products.Contains(product) && command.Verb != "create"
            && command.Arguments.Any(static argument => argument.Required && argument.InputKind == "file")))
        {
            foreach (CliOption option in command.Options.Where(static option => R16Options.ContainsKey(option.Name)))
            {
                string shard = command.Product!;
                if (option.Name != "--range")
                {
                    yield return InvariantCases.Case("C-R16", $"{command} {option.Name} {R16Beyond}", shard, slow: false,
                        R16Selection(command, option.Name, R16Beyond),
                        new ScenarioExpectation
                        {
                            ExitCode = [4],
                            Error = notFound,
                            Json =
                            [
                                new JsonAssertion { Path = "error.details.requested", Exists = true },
                                new JsonAssertion { Path = "error.details.availableCount", Exists = true },
                            ],
                            Files = new ScenarioFiles { NothingWritten = true },
                        });
                }
                yield return InvariantCases.Case("C-R16", $"{command} {option.Name} all", shard, slow: false,
                    R16Selection(command, option.Name, "all"),
                    new ScenarioExpectation { ExitCode = [0] });
                string malformed = R16Options[option.Name];
                yield return InvariantCases.Case("C-R16", $"{command} {option.Name} {malformed}", shard, slow: false,
                    R16Selection(command, option.Name, malformed),
                    new ScenarioExpectation { ExitCode = [2], Error = ["OPTION_INVALID"], Files = new ScenarioFiles { NothingWritten = true } });
            }
        }
        foreach (CliProduct product in catalog.Products.Where(static product => ScenarioFixtures.Products.Contains(product.Id)))
        {
            if (catalog.Commands.FirstOrDefault(command => command.Words == $"{product.Id} edit") is { } edit
                && R16Operation(catalog, product) is { } operation)
            {
                yield return InvariantCases.Case("C-R16", $"{product.Id} op {operation["op"]} with a malformed selection", product.Id, slow: false,
                    R26Edit(edit, operation),
                    new ScenarioExpectation { ExitCode = [4], Error = ["OPS_INVALID"], Files = new ScenarioFiles { NothingWritten = true } });
            }
        }
    }

    /// <summary>The selection definitions of the ops schemas and the malformed value of each.</summary>
    private static readonly Dictionary<string, string> R16Definitions = new(StringComparer.Ordinal)
    {
        ["#/$defs/pages"] = "1-x",
        ["#/$defs/range"] = "A1:!!",
    };

    /// <summary>
    /// The first operation whose only required field is a selection, directly or in an object
    /// such as a target, with that selection malformed; null when the product has none.
    /// </summary>
    private static JsonObject? R16Operation(CliCatalog catalog, CliProduct product)
    {
        if (product.OpsSchemaId is null)
        {
            return null;
        }
        JsonNode definitions = JsonNode.Parse(catalog.SchemaText(product.OpsSchemaId))!["$defs"]!;
        foreach (string op in product.Ops)
        {
            if (definitions[op]?["properties"] is not JsonObject properties)
            {
                continue;
            }
            string[] required = [.. (definitions[op]!["required"] as JsonArray ?? []).Select(static name => name!.GetValue<string>()).Where(static name => name != "op")];
            foreach ((string field, JsonNode? schema) in properties.Where(property => required.Length == 0 || required is [var only] && only == property.Key))
            {
                string reference = schema?["$ref"]?.GetValue<string>() ?? string.Empty;
                if (R16Definitions.TryGetValue(reference, out string? malformed))
                {
                    return new JsonObject { ["op"] = op, [field] = malformed };
                }
                if (reference.StartsWith("#/$defs/", StringComparison.Ordinal)
                    && definitions[reference["#/$defs/".Length..]]?["properties"] is JsonObject inner
                    && inner.FirstOrDefault(static property => R16Definitions.ContainsKey(property.Value?["$ref"]?.GetValue<string>() ?? string.Empty)) is { Key: { } name } selection)
                {
                    return new JsonObject { ["op"] = op, [field] = new JsonObject { [name] = R16Definitions[selection.Value!["$ref"]!.GetValue<string>()] } };
                }
            }
        }
        return null;
    }

    /// <summary>
    /// The smallest invocation that applies a selection: a convert that can write PDF writes it,
    /// since a flow format has no pages to select; a split selects pages <c>--by pages</c>; and an extract selects the
    /// units of the text it extracts.
    /// </summary>
    private static Invocation R16Selection(CliCommand command, string option, string value)
    {
        string? omit = command.Option("--to")?.AllowedValues.Contains("pdf") == true
            ? "--to"
            : new[] { "--by", "--every", "--what" }.FirstOrDefault(command.Has);
        Invocation invocation = Invocation.For(command, omit);
        if (omit == "--to")
        {
            invocation.Add("--to", "pdf");
        }
        else if (omit == "--by")
        {
            invocation.Add("--by", "pages");
        }
        else if (omit == "--what")
        {
            invocation.Add("--what", "text");
        }
        return invocation.Add(option, value);
    }

    /// <summary>Every edit accepts its operations as a bare JSON array as well as an <c>{ops:[...]}</c> document.</summary>
    private static IEnumerable<InvariantCase> R22Run(CliCatalog catalog)
    {
        foreach (CliCommand edit in catalog.Commands.Where(static command => !command.Hidden && command.Verb == "edit" && command.Has("--ops")
            && command.Product is { } product && ScenarioFixtures.Products.Contains(product)))
        {
            JsonNode operations = JsonNode.Parse(ScenarioFixtures.Read(edit.Product + ".ops"))!["ops"]!;
            Invocation invocation = Invocation.For(edit, "--ops").Add("--ops", "ops.json");
            invocation.Files["ops.json"] = new ScenarioFile { Text = operations.ToJsonString() };
            if (edit.Has("--dry-run"))
            {
                invocation.Add("--dry-run");
            }
            yield return InvariantCases.Case("C-R22", $"{edit} --ops with a bare array", edit.Product!, slow: false, invocation,
                new ScenarioExpectation { ExitCode = [0] });
        }
    }

    /// <summary>
    /// The streams of a successful and a failed run of each product's <c>inspect</c> and of
    /// <c>doctor</c>: a success writes its result to stdout, in table and in JSON mode, and with
    /// a license and no warning leaves stderr empty, and a licensed JSON result carries no license
    /// block (C-R33); a failure in table mode leaves stdout empty.
    /// A failure in JSON mode is the <c>error-envelope</c> check of every scenario. JSON-mode
    /// warnings belong to the result on stdout, so evaluation runs, which may warn, are checked
    /// for stdout only.
    /// </summary>
    private static IEnumerable<InvariantCase> R23(CliCatalog catalog)
    {
        string output = OutputOption(catalog);
        foreach ((string shard, CliCommand command) in R23Samples(catalog))
        {
            Invocation success = Invocation.For(command);
            yield return Runs("C-R23", $"{command} succeeds in table mode", shard, slow: false, success, session =>
            {
                CliResult result = session.Run(output, "table");
                var problems = new List<string>();
                if (result.ExitCode != 0)
                {
                    problems.Add($"exit {result.ExitCode}: {Excerpt(result.StdErr)}");
                }
                else if (result.StdOut.Trim().Length == 0)
                {
                    problems.Add("stdout is empty");
                }
                if (result.ExitCode == 0 && ScenarioLicense.Licensed && result.StdErr.Trim().Length > 0)
                {
                    problems.Add($"stderr is not empty: {Excerpt(result.StdErr)}");
                }
                return problems;
            });
            yield return Runs("C-R23", $"{command} succeeds in JSON mode", shard, slow: false, success, session =>
            {
                CliResult result = session.Run(output, "json");
                JsonNode? document = ParseJson(result.StdOut);
                var problems = new List<string>();
                if (result.ExitCode != 0)
                {
                    problems.Add($"exit {result.ExitCode}: {Excerpt(result.StdErr)}");
                }
                else if (document is null)
                {
                    problems.Add($"stdout is not one JSON document: {Excerpt(result.StdOut)}");
                }
                bool warns = document?["warnings"] is JsonArray { Count: > 0 };
                if (result.ExitCode == 0 && ScenarioLicense.Licensed && !warns && result.StdErr.Trim().Length > 0)
                {
                    problems.Add($"stderr is not empty: {Excerpt(result.StdErr)}");
                }
                // C-R33: a licensed result omits the license block.
                if (ScenarioLicense.Licensed && document?["license"] is { } license)
                {
                    problems.Add($"a licensed result carries license {Excerpt(license.ToJsonString())}");
                }
                return problems;
            });
            yield return Runs("C-R23", $"{command} fails in table mode", shard, slow: false, R23Failure(command), session =>
            {
                CliResult result = session.Run(output, "table");
                var problems = new List<string>();
                if (result.ExitCode == 0)
                {
                    problems.Add("the failure exits 0");
                }
                if (result.StdOut.Trim().Length > 0)
                {
                    problems.Add($"stdout is not empty: {Excerpt(result.StdOut)}");
                }
                if (result.StdErr.Trim().Length == 0)
                {
                    problems.Add("stderr is empty");
                }
                return problems;
            });
        }
    }

    /// <summary>The commands whose streams C-R23 and C-R37 judge: each product's <c>inspect</c> and the platform's <c>doctor</c>.</summary>
    private static IEnumerable<(string Shard, CliCommand Command)> R23Samples(CliCatalog catalog)
    {
        foreach (string product in ScenarioFixtures.Products)
        {
            if (catalog.Commands.FirstOrDefault(command => command.Words == $"{product} inspect") is { } inspect)
            {
                yield return (product, inspect);
            }
        }
        if (catalog.Commands.FirstOrDefault(static command => command.Words == "doctor") is { } doctor)
        {
            yield return (InvariantCases.Platform, doctor);
        }
    }

    /// <summary>A failing run of a sample: a missing input, or an unknown option for a command that reads none.</summary>
    private static Invocation R23Failure(CliCommand command)
    {
        Invocation invocation = Invocation.For(command);
        if (invocation.Input is not { } input)
        {
            return invocation.Add("--invariant-unknown-option");
        }
        invocation.Files.Remove(input);
        invocation.Replace(input, "missing" + Path.GetExtension(input));
        return invocation;
    }

    /// <summary>
    /// A command that completes with a result that fails its check exits 8: a PDF that is not
    /// PDF/A (<c>pdf validate</c>), a document whose font is missing (<c>fonts check</c>), and a
    /// <c>doctor</c> whose license check fails. And Ctrl+C without <c>--timeout</c> cancels a
    /// command with exit 130 and leaves no staged output behind; that case runs a helper process
    /// with a console of its own, so it is slow.
    /// </summary>
    private static IEnumerable<InvariantCase> R24Run(CliCatalog catalog)
    {
        if (catalog.Commands.FirstOrDefault(static command => command.Words == "pdf validate") is { } validate)
        {
            yield return InvariantCases.Case("C-R24", $"{validate} of a PDF that is not PDF/A", "pdf", slow: false, Invocation.For(validate),
                new ScenarioExpectation { ExitCode = [8], Json = [new JsonAssertion { Path = "valid", Value = false }] });
        }
        if (catalog.Commands.FirstOrDefault(static command => command.Words == "fonts check") is { } check
            && ScenarioFixtures.Products.Contains("words"))
        {
            // A Words document whose default font no machine has.
            const string Document = "missing-font.docx";
            const string Operations = """{"ops":[{"op":"set_default_font","font":"Invariant Missing Font"}]}""";
            string id = $"C-R24 | {check} of a document whose font is missing";
            var scenario = new Scenario
            {
                Name = id,
                Files = new Dictionary<string, ScenarioFile>
                {
                    ["input.docx"] = new() { Fixture = "words.docx" },
                    ["ops.json"] = new() { Text = Operations },
                },
                Steps =
                [
                    new ScenarioStep
                    {
                        Args = ["words", "edit", "input.docx", "--ops", "ops.json", "--out", Document],
                        Expect = new ScenarioExpectation { ExitCode = [0] },
                    },
                    new ScenarioStep
                    {
                        Args = [.. check.Tokens, Document],
                        Expect = new ScenarioExpectation { ExitCode = [8], Json = [new JsonAssertion { Path = "allAvailable", Value = false }] },
                    },
                ],
            };
            yield return new InvariantCase(id, "C-R24", InvariantCases.Platform, Slow: false, scenario);
        }
        if (catalog.Commands.FirstOrDefault(static command => command.Words == "words create") is { } create)
        {
            // Markdown that takes words create about fifteen seconds to pour in; Ctrl+C comes
            // after three, while the command works.
            Invocation invocation = Invocation.For(create);
            invocation.Files["source.md"] = new ScenarioFile
            {
                Text = string.Concat(Enumerable.Range(1, 30_000).Select(static index => $"Paragraph {index} of the interrupted document.\n\n")),
            };
            yield return Runs("C-R24", $"{create} interrupted by Ctrl+C", "words", slow: true, invocation, session =>
            {
                (int exitCode, string[] left) = session.Interrupt(TimeSpan.FromSeconds(3));
                var problems = new List<string>();
                if (exitCode != 130)
                {
                    problems.Add($"exit {exitCode}, expected 130");
                }
                if (left.Length > 0)
                {
                    problems.Add($"left {string.Join(", ", left.Select(static entry => R24GeneratedId().Replace(entry, "<id>")))}");
                }
                return problems;
            });
            // With --timeout a supervising parent cancels its worker on the first press; the second
            // press, while that runs, ends the parent at once, as it does without --timeout.
            Invocation timed = Invocation.For(create).Add("--timeout", "600");
            timed.Files["source.md"] = invocation.Files["source.md"];
            yield return Runs("C-R24", $"{create} --timeout interrupted by Ctrl+C twice", "words", slow: true, timed, session =>
            {
                (int exitCode, string[] left) = session.Interrupt(TimeSpan.FromSeconds(3), presses: 2);
                var problems = new List<string>();
                if (exitCode != StatusControlCExit)
                {
                    problems.Add($"exit {exitCode}, expected {StatusControlCExit} (ended by the second Ctrl+C)");
                }
                if (left.Length > 0)
                {
                    problems.Add($"left {string.Join(", ", left.Select(static entry => R24GeneratedId().Replace(entry, "<id>")))}");
                }
                return problems;
            });
        }
        if (catalog.Commands.FirstOrDefault(static command => command.Words == "doctor") is { } doctor)
        {
            yield return InvariantCases.Case("C-R24", $"{doctor} with a missing license file", InvariantCases.Platform, slow: false,
                new Invocation(doctor.Tokens).Add("--license", "missing.lic"),
                new ScenarioExpectation { ExitCode = [8], Json = [new JsonAssertion { Path = "ok", Value = false }] });
        }
    }

    /// <summary>The exit code of a Windows process that Ctrl+C ended: <c>STATUS_CONTROL_C_EXIT</c>.</summary>
    private const int StatusControlCExit = unchecked((int)0xC000013A);

    /// <summary>A generated name part, such as a staging directory's GUID.</summary>
    [GeneratedRegex("(?=[0-9a-f-]*[0-9])[0-9a-f][0-9a-f-]{14,}[0-9a-f]")]
    private static partial Regex R24GeneratedId();

    /// <summary>
    /// One misspelling of each kind of name per product: a command, an option, an option value
    /// (the first one of <c>inspect</c>, which the parser checks, and <c>--to</c>, which the
    /// command checks itself), an operation, an operation field and an operation enum value; a top-level command; and a
    /// target name, a cells sheet, and the positional names of <c>capabilities</c> (a product)
    /// and <c>docs</c> (a topic). The <c>unknown-*</c> and <c>invalid-enum</c> invariants already
    /// check the exit code, the error code and that <c>details.suggestions</c> holds the intended
    /// name; this rule adds that <c>details.available</c> lists at most 50 names and
    /// <c>details.suggestions</c> is present, the hint starts with "Did you mean", and the message
    /// is one line saying "Must be one of: a, b" with exactly <c>details.available</c>. The kinds
    /// no invariant covers, enum values and target names, also check the suggestion.
    /// </summary>
    private static IEnumerable<InvariantCase> R26(CliCatalog catalog)
    {
        string output = OutputOption(catalog);
        foreach (string product in ScenarioFixtures.Products)
        {
            if (catalog.Commands.FirstOrDefault(command => command.Words == product) is not { } group)
            {
                continue;
            }
            string[] siblings = [.. group.Subcommands];
            string name = siblings.First(sibling => !catalog.Command(Join(product, sibling)).Hidden);
            string commandTypo = InvariantCases.Typo(name, siblings);
            yield return R26Case(output, $"{product} command {name} as {commandTypo}", product,
                new Invocation([product, commandTypo]), Join(product, name), suggests: false);
            if (catalog.Commands.FirstOrDefault(command => command.Words == $"{product} inspect") is { } inspect
                && inspect.Options.FirstOrDefault(static option => !option.Hidden) is { } option)
            {
                string typo = "--" + InvariantCases.Typo(option.Name.TrimStart('-'), inspect.Options.Select(static candidate => candidate.Name.TrimStart('-')));
                yield return R26Case(output, $"{inspect} option {option.Name} as {typo}", product,
                    new Invocation([.. inspect.Tokens, typo]), option.Name, suggests: false);
            }
            if (catalog.Commands.FirstOrDefault(command => command.Words == $"{product} inspect") is { } described
                && described.Options.FirstOrDefault(static option => !option.Hidden && option.Type != "boolean" && option.AllowedValues.Count > 0) is { } choice)
            {
                string value = choice.AllowedValues[0];
                string typo = InvariantCases.Typo(value, choice.AllowedValues);
                yield return R26Case(output, $"{described} {choice.Name} {value} as {typo}", product,
                    Invocation.For(described).Add(choice.Name, typo), value, suggests: false);
            }
            if (catalog.Commands.FirstOrDefault(command => command.Words == $"{product} convert") is { } convert
                && convert.Option("--to") is { AllowedValues.Count: > 0 } to)
            {
                string value = to.AllowedValues[0];
                string typo = InvariantCases.Typo(value, to.AllowedValues);
                yield return R26Case(output, $"{convert} --to {value} as {typo}", product,
                    Invocation.For(convert, "--to").Add("--to", typo), value, suggests: false);

                // An --out extension names the format a convert writes when --to is omitted.
                string[] extensions = [.. Extensions(catalog, product, to.AllowedValues)];
                string extension = Extensions(catalog, product, [value]).First();
                string extensionTypo = "." + InvariantCases.Typo(extension[1..], extensions.Select(static candidate => candidate[1..]));
                yield return R26Case(output, $"{convert} --out extension {extension} as {extensionTypo}", product,
                    Invocation.For(convert, "--to").Add("--out", "output" + extensionTypo), extension, suggests: true);
            }
            foreach (InvariantCase item in R26Operations(catalog, output, catalog.Product(product)))
            {
                yield return item;
            }
        }
        string first = ScenarioFixtures.Products.First();
        string[] commands = [.. catalog.Commands.Single(static command => command.Words.Length == 0).Subcommands];
        string rootTypo = InvariantCases.Typo(first, commands);
        yield return R26Case(output, $"command {first} as {rootTypo}", InvariantCases.Platform,
            new Invocation([rootTypo]), first, suggests: false);
        string[] products = [.. catalog.Products.Select(static product => product.Id)];
        string productTypo = InvariantCases.Typo(first, products);
        yield return R26Case(output, $"capabilities product {first} as {productTypo}", InvariantCases.Platform,
            new Invocation(["capabilities", productTypo]), first, suggests: true);
        // editing is the platform guide every build ships (aspose-cli docs lists the topics).
        const string Topic = "editing";
        yield return R26Case(output, $"docs topic {Topic} as {InvariantCases.Typo(Topic, [Topic])}", InvariantCases.Platform,
            new Invocation(["docs", InvariantCases.Typo(Topic, [Topic])]), Topic, suggests: true);
        if (catalog.Commands.FirstOrDefault(static command => !command.Hidden && command.Product == "cells" && command.Has("--sheet") && !command.Has("--out")
            && command.Arguments.Any(static argument => argument.Required && argument.InputKind == "file")) is { } sheets)
        {
            // Every cells fixture has the one sheet that cells create names.
            const string Sheet = "Sheet1";
            string typo = InvariantCases.Typo(Sheet, [Sheet]);
            yield return R26Case(output, $"{sheets} --sheet {Sheet} as {typo}", "cells",
                Invocation.For(sheets).Add("--sheet", typo), Sheet, suggests: true);
        }
    }

    /// <summary>The extensions of the formats <paramref name="ids"/> of a product, in the order capabilities lists them.</summary>
    private static IEnumerable<string> Extensions(CliCatalog catalog, string product, IEnumerable<string> ids) =>
        ids.SelectMany(id => catalog.Document["products"]!.AsArray()
            .Single(entry => entry!["id"]!.GetValue<string>() == product)!["formats"]!.AsArray()
            .Single(format => format!["id"]!.GetValue<string>() == id)!["extensions"]!.AsArray()
            .Select(static extension => extension!.GetValue<string>()))
        .Distinct(StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<InvariantCase> R26Operations(CliCatalog catalog, string output, CliProduct product)
    {
        if (product.OpsSchemaId is null || product.Ops.Count == 0
            || catalog.Commands.FirstOrDefault(command => command.Words == $"{product.Id} edit") is not { } edit)
        {
            yield break;
        }
        JsonNode definitions = JsonNode.Parse(catalog.SchemaText(product.OpsSchemaId))!["$defs"]!;
        string op = product.Ops[0];
        string opTypo = InvariantCases.Typo(op, product.Ops);
        yield return R26Case(output, $"{product.Id} op {op} as {opTypo}", product.Id,
            R26Edit(edit, new JsonObject { ["op"] = opTypo }), op, suggests: false);

        foreach (string candidate in product.Ops)
        {
            if (definitions[candidate]?["properties"] is not JsonObject properties)
            {
                continue;
            }
            string[] fields = [.. properties.Select(static property => property.Key).Where(static key => key is not ("id" or "op"))];
            if (fields.Length == 0)
            {
                continue;
            }
            string fieldTypo = InvariantCases.Typo(fields[0], fields);
            yield return R26Case(output, $"{product.Id} op {candidate} field {fields[0]} as {fieldTypo}", product.Id,
                R26Edit(edit, new JsonObject { ["op"] = candidate, [fieldTypo] = "x" }), fields[0], suggests: false);
            break;
        }

        // The first operation with an enum field whose required fields are all enums, so it is
        // complete with their first values.
        foreach (string candidate in product.Ops)
        {
            if (definitions[candidate]?["properties"] is not JsonObject properties)
            {
                continue;
            }
            string[] required = [.. (definitions[candidate]!["required"] as JsonArray ?? []).Select(static name => name!.GetValue<string>()).Where(static name => name != "op")];
            string? field = properties.Where(static property => property.Key != "op" && property.Value?["enum"] is JsonArray { Count: > 0 })
                .Select(static property => property.Key).FirstOrDefault();
            if (field is null || required.Any(name => properties[name]?["enum"] is not JsonArray { Count: > 0 }))
            {
                continue;
            }
            var operation = new JsonObject { ["op"] = candidate };
            foreach (string name in required)
            {
                operation[name] = properties[name]!["enum"]![0]!.DeepClone();
            }
            string[] values = [.. properties[field]!["enum"]!.AsArray().Select(static value => value!.ToString())];
            string valueTypo = InvariantCases.Typo(values[0], values);
            operation[field] = valueTypo;
            yield return R26Case(output, $"{product.Id} op {candidate} {field} {values[0]} as {valueTypo}", product.Id,
                R26Edit(edit, operation), values[0], suggests: true);
            break;
        }
    }

    private static Invocation R26Edit(CliCommand edit, JsonObject operation)
    {
        Invocation invocation = Invocation.For(edit, "--ops").Add("--ops", "ops.json");
        invocation.Files["ops.json"] = new ScenarioFile { Text = new JsonObject { ["ops"] = new JsonArray(operation) }.ToJsonString() };
        if (edit.Has("--dry-run"))
        {
            invocation.Add("--dry-run");
        }
        return invocation;
    }

    private static InvariantCase R26Case(string output, string subject, string shard, Invocation invocation, string intended, bool suggests) =>
        Runs("C-R26", subject, shard, slow: false, invocation, session => R26Problems(session.Run(output, "json"), intended, suggests));

    private static IEnumerable<string> R26Problems(CliResult result, string intended, bool suggests)
    {
        const string Marker = "Must be one of: ";
        if (ParseJson(result.StdErr)?["error"] is not JsonObject error)
        {
            yield return $"exit {result.ExitCode} wrote no JSON error: {Excerpt(result.StdErr)}";
            yield break;
        }
        string[]? available = Strings(error["details"]?["available"]);
        string[]? suggestions = Strings(error["details"]?["suggestions"]);
        if (available is null)
        {
            yield return "details.available is missing";
        }
        else if (available.Length > 50)
        {
            yield return $"details.available lists {available.Length} names, more than 50";
        }
        if (suggestions is not { Length: > 0 })
        {
            yield return "details.suggestions is missing or empty";
        }
        else if (suggests && !suggestions.Contains(intended, StringComparer.Ordinal))
        {
            yield return $"details.suggestions [{string.Join(", ", suggestions)}] leaves out '{intended}'";
        }
        string hint = error["hint"]?.GetValue<string>() ?? string.Empty;
        if (!hint.StartsWith("Did you mean", StringComparison.Ordinal))
        {
            yield return $"the hint does not start with 'Did you mean': {Excerpt(hint)}";
        }
        string message = error["message"]?.GetValue<string>() ?? string.Empty;
        int lines = message.ReplaceLineEndings("\n").Split('\n').Length;
        if (lines > 1)
        {
            yield return $"the message has {lines} lines: {Excerpt(message)}";
        }
        int marker = message.IndexOf(Marker, StringComparison.Ordinal);
        if (marker < 0)
        {
            yield return $"the message does not say '{Marker}...': {Excerpt(message)}";
            yield break;
        }
        if (available is null)
        {
            yield break;
        }
        string[] listed = [.. message[(marker + Marker.Length)..].ReplaceLineEndings(" ").Trim().TrimEnd('.').Split(", ").Select(static value => value.Trim())];
        string[] unlisted = [.. available.Except(listed, StringComparer.Ordinal)];
        string[] extra = [.. listed.Except(available, StringComparer.Ordinal)];
        if (unlisted.Length > 0)
        {
            yield return $"the message leaves out {string.Join(", ", unlisted)} of details.available";
        }
        if (extra.Length > 0)
        {
            yield return $"the message lists {string.Join(", ", extra)}, which details.available does not";
        }
    }

    /// <summary>
    /// Help and version: <c>--help</c> and <c>-h</c> print the same help to stdout and exit 0,
    /// whatever the output format; <c>--version</c> prints one line; with JSON output it prints
    /// a result of the version schema. The root and the top-level commands are the sample;
    /// deeper commands complete it.
    /// </summary>
    private static IEnumerable<InvariantCase> R35(CliCatalog catalog)
    {
        string output = OutputOption(catalog);
        foreach (CliCommand command in catalog.Commands.Where(static command => !command.Hidden))
        {
            yield return Runs("C-R35", $"{command} --help", InvariantCases.ShardOf(command), slow: command.Tokens.Length > 1,
                new Invocation(command.Tokens), session => R35HelpProblems(session, output));
        }
        yield return Runs("C-R35", "--version", InvariantCases.Platform, slow: false, new Invocation([]), session =>
        {
            CliResult result = session.Run("--version");
            string[] lines = Lines(result.StdOut);
            var problems = new List<string>();
            if (result.ExitCode != 0)
            {
                problems.Add($"exit {result.ExitCode}: {Excerpt(result.StdErr)}");
            }
            if (lines.Length != 1)
            {
                problems.Add($"stdout has {lines.Length} lines: {Excerpt(result.StdOut)}");
            }
            return problems;
        });
        yield return Runs("C-R35", "--version in JSON mode", InvariantCases.Platform, slow: false, new Invocation([]), session =>
        {
            const string Prefix = "https://schemas.aspose.com/aspose-cli/";
            const string Suffix = ".schema.json";
            CliResult result = session.Run("--version", output, "json");
            JsonNode? document = ParseJson(result.StdOut);
            string schema = document?["schema"]?.GetValue<string>() ?? string.Empty;
            if (result.ExitCode != 0 || document is null)
            {
                return [$"exit {result.ExitCode} wrote no JSON result: {Excerpt(result.StdOut + " " + result.StdErr)}"];
            }
            if (!schema.StartsWith(Prefix, StringComparison.Ordinal) || !schema.EndsWith("/version" + Suffix, StringComparison.Ordinal))
            {
                return [$"the result names schema '{schema}', not the version schema"];
            }
            return ScenarioContract.Validate(schema[Prefix.Length..^Suffix.Length], document) is { } invalid ? [$"the result {invalid}"] : [];
        });
    }

    private static IEnumerable<string> R35HelpProblems(RuleWorkspace session, string output)
    {
        CliResult help = session.Run("--help");
        if (help.ExitCode != 0)
        {
            yield return $"--help exits {help.ExitCode}";
        }
        if (help.StdOut.Trim().Length == 0)
        {
            yield return $"--help writes nothing to stdout; stderr: {Excerpt(help.StdErr)}";
        }
        if (help.StdErr.Trim().Length > 0)
        {
            yield return $"--help writes to stderr: {Excerpt(help.StdErr)}";
        }
        CliResult shortHelp = session.Run("-h");
        if (shortHelp.ExitCode != help.ExitCode || shortHelp.StdOut != help.StdOut)
        {
            yield return $"-h differs from --help: exit {shortHelp.ExitCode}, stdout {Excerpt(shortHelp.StdOut)}";
        }
        CliResult json = session.Run("--help", output, "json");
        if (json.ExitCode != help.ExitCode || json.StdOut != help.StdOut || json.StdErr != help.StdErr)
        {
            yield return $"--help with {output} json differs: exit {json.ExitCode}, stdout {Excerpt(json.StdOut)}";
        }
    }

    /// <summary>
    /// <c>--quiet</c> and <c>--verbose</c> leave stdout and the exit code as they are, in table and
    /// in JSON mode: <c>--quiet</c> leaves a successful run's stderr without warnings or notices,
    /// so empty, and every line <c>--verbose</c> adds to stderr is a JSON object.
    /// </summary>
    private static IEnumerable<InvariantCase> R37(CliCatalog catalog)
    {
        string output = OutputOption(catalog);
        foreach ((string shard, CliCommand command) in R23Samples(catalog))
        {
            foreach (string format in new[] { "table", "json" })
            {
                yield return Runs("C-R37", $"{command} --quiet and --verbose in {format} mode", shard, slow: false, Invocation.For(command),
                    session => R37Problems(session, output, format));
            }
        }
    }

    private static IEnumerable<string> R37Problems(RuleWorkspace session, string output, string format)
    {
        CliResult plain = session.Run(output, format);
        CliResult quiet = session.Run(output, format, "--quiet");
        CliResult verbose = session.Run(output, format, "--verbose");
        if (plain.ExitCode != 0)
        {
            yield return $"the plain run exits {plain.ExitCode}: {Excerpt(plain.StdErr)}";
            yield break;
        }
        if (quiet.ExitCode != plain.ExitCode || quiet.StdOut != plain.StdOut)
        {
            yield return $"--quiet changes stdout or the exit code: exit {quiet.ExitCode}, stdout {Excerpt(quiet.StdOut)}";
        }
        if (quiet.StdErr.Trim().Length > 0)
        {
            yield return $"--quiet leaves stderr: {Excerpt(quiet.StdErr)}";
        }
        if (verbose.ExitCode != plain.ExitCode || verbose.StdOut != plain.StdOut)
        {
            yield return $"--verbose changes stdout or the exit code: exit {verbose.ExitCode}, stdout {Excerpt(verbose.StdOut)}";
        }
        string[] before = Lines(plain.StdErr);
        if (Lines(verbose.StdErr).FirstOrDefault(line => !before.Contains(line, StringComparer.Ordinal) && ParseJson(line) is not JsonObject) is { } added)
        {
            yield return $"--verbose adds a stderr line that is not a JSON object: {Excerpt(added)}";
        }
    }
}
