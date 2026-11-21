using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.TestKit.Scenarios;

namespace Aspose.Cli.Invariants.Tests;

// What writes and windows do when run. Each case runs the CLI in its product's shard; the runs and
// comparisons are OperationRuns.
internal static partial class ContractRules
{
    private const string NoEffectWarning = "OP_NO_EFFECT";

    /// <summary>The unit a read command counts, by the option that selects its units.</summary>
    private static readonly Dictionary<string, string> SelectionUnits = new(StringComparer.Ordinal)
    {
        ["--pages"] = "page",
        ["--slides"] = "slide",
        ["--range"] = "cell",
    };

    /// <summary>A selection of every unit of the fixture, by selection option.</summary>
    private static readonly Dictionary<string, string> WholeSelections = new(StringComparer.Ordinal)
    {
        ["--pages"] = "1-",
        ["--slides"] = "1-",
        ["--range"] = "A1:B3",
    };

    /// <summary>The operations that give a product's fixture a second page or sheet; null when the fixture has two units or the product merges.</summary>
    private static readonly Dictionary<string, string?> SecondUnit = new(StringComparer.Ordinal)
    {
        ["cells"] = """[{"add":"sheet","args":{"index":1,"sheetType":"Worksheet","sheetName":"Extra"}},{"set":"sheet/Extra/cell/A1","props":{"value":"Second sheet"}}]""",
        ["pdf"] = null,
        ["slides"] = null,
        ["words"] = """[{"do":"insert_break","args":{"paragraph":"section/0/body/paragraph/0","breakType":"PageBreak"}}]""",
    };


    // ----- C-R11: a dry run writes nothing and says what it checked ------------------------------

    /// <summary>
    /// Every command that writes files (<see cref="Writes"/>) and declares <c>--dry-run</c>, run
    /// with it: it succeeds, says <c>dryRun: true</c>, lists what it checked and writes nothing,
    /// in the workspace, the user configuration or the installation directory; an edit lists its
    /// operations in <c>applied[]</c> and their targets among its checks. Whether a writing
    /// command declares <c>--dry-run</c> at all is <c>R11Declared</c>'s question.
    /// </summary>
    private static IEnumerable<InvariantCase> R11Run(CliCatalog catalog)
    {
        foreach (TreeCommand tree in CommandTree.Of(catalog).Where(static command => Writes(command) && command.Has("--dry-run")))
        {
            CliCommand command = tree.Command;
            Invocation invocation = DryRunInvocation(command);
            invocation.Add("--dry-run");
            var json = new List<JsonAssertion>
            {
                new() { Path = "dryRun", Value = true },
                new() { Path = "checked", Exists = true },
            };
            if (command.Has("--ops"))
            {
                json.Add(new JsonAssertion { Path = "applied[0].op", Exists = true });
                json.Add(new JsonAssertion { Path = "checked", Contains = "targets" });
            }
            ScenarioExpectation expect = new()
            {
                ExitCode = [0],
                Json = json,
                Files = new ScenarioFiles { NothingWritten = true, NothingInstalled = true },
            };
            yield return InvariantCases.Case("C-R11", $"{command} --dry-run", InvariantCases.ShardOf(command), slow: false, invocation, expect);
        }
    }

    /// <summary>
    /// The smallest valid invocation of a writing command for its dry run: a license installation
    /// an existing file, and a Skill
    /// installation a bundled Skill for a host's project directory, the workspace.
    /// </summary>
    private static Invocation DryRunInvocation(CliCommand command)
    {
        Invocation invocation = Invocation.For(command);
        switch (command.Words)
        {
            case "license install":
                invocation.Replace(invocation.Args[^1], "license.lic");
                invocation.Files["license.lic"] = new ScenarioFile { Text = "<License />" };
                break;
            case "skill install":
                invocation.Replace(invocation.Args[^1], "aspose-cli-platform");
                invocation.Add("--host", "codex");
                break;
        }
        return invocation;
    }

    // ----- C-R12: one naming scheme for multi-file outputs ---------------------------------------

    /// <summary>
    /// Every render, split and image convert of a product writes <c>&lt;stem&gt;.&lt;n&gt;.&lt;ext&gt;</c>,
    /// <c>n</c> padded to the digits of the file count, or <c>&lt;stem&gt;.&lt;sheet&gt;.&lt;ext&gt;</c>
    /// per sheet, and a render of one unit writes <c>--out</c> itself. A render without an
    /// <c>--all-&lt;units&gt;</c> option renders every unit by default. The stem is the <c>--out</c>
    /// file's, or the input's in an <c>--out-dir</c>.
    /// </summary>
    private static IEnumerable<InvariantCase> R12Run(CliCatalog catalog)
    {
        foreach (string product in catalog.Products.Select(static product => product.Id).Where(ScenarioFixtures.Products.Contains))
        {
            string extension = ScenarioFixtures.PrimaryFormat(product);
            if (catalog.Commands.FirstOrDefault(command => command.Words == $"{product} render") is { } render)
            {
                string[] image = ImageTarget(render);
                if (render.Options.FirstOrDefault(static option => option.Name.StartsWith("--all-", StringComparison.Ordinal)) is { } all)
                {
                    yield return OutputNames(product, $"{render} {all.Name}", units: 2,
                        [.. render.Tokens, "<two>", all.Name, .. image, "--out", "page.png"],
                        static names => [.. names.Select(static name => $"page.{name}.png")], perSheet: render.Has("--sheets") || render.Has("--sheet"));
                }
                else
                {
                    yield return OutputNames(product, $"{render} of every unit by default", units: 2,
                        [.. render.Tokens, "<two>", .. image, "--out", "page.png"],
                        static names => [.. names.Select(static name => $"page.{name}.png")], perSheet: render.Has("--sheets") || render.Has("--sheet"));
                }
                // The seed document may hold more than one unit, so a selection names the first.
                string[] first = render.Options.FirstOrDefault(static option => option.Name is "--pages" or "--slides" or "--sheets" or "--sheet") is { } selection
                    ? [selection.Name, selection.Name == "--sheet" ? "Sheet1" : "1"]
                    : [];
                yield return OutputNames(product, $"{render} one unit", units: 1,
                    [.. render.Tokens, "<one>", .. first, .. image, "--out", "page.png"], static _ => ["page.png"]);
            }
            if (catalog.Commands.FirstOrDefault(command => command.Words == $"{product} split") is { } split)
            {
                string[] parting = split.Option("--by") is { } by && by.AllowedValues.Contains("pages")
                    ? ["--by", "pages", .. split.Has("--every") ? new[] { "--every", "1" } : []]
                    : [];
                yield return OutputNames(product, $"{split} {string.Join(' ', parting)}", units: 2,
                    [.. split.Tokens, "<two>", .. parting, "--out-dir", "parts"],
                    names => [.. names.Select(name => $"parts/two.{name}.{extension}")]);
                if (split.Has("--every") && catalog.Commands.Any(command => command.Words == $"{product} merge"))
                {
                    yield return OutputNames(product, $"{split} {string.Join(' ', parting)} of ten pages", units: 10,
                        [.. split.Tokens, "<ten>", .. parting, "--out-dir", "parts"],
                        names => [.. names.Select(name => $"parts/ten.{name}.{extension}")], slow: true);
                }
            }
            if (catalog.Commands.FirstOrDefault(command => command.Words == $"{product} convert") is { } convert
                && convert.Option("--to") is { } to && to.AllowedValues.Contains("png"))
            {
                yield return OutputNames(product, $"{convert} --to png", units: 2,
                    [.. convert.Tokens, "<two>", "--to", "png", "--out", "page.png"],
                    static names => [.. names.Select(static name => $"page.{name}.png")]);
            }
        }
    }

    private static string[] ImageTarget(CliCommand render) =>
        render.Option("--to") is { } to && to.AllowedValues.Contains("png") ? ["--to", "png"] : [];

    /// <summary>
    /// A case that runs <paramref name="args"/> on a document of <paramref name="units"/> pages,
    /// slides or sheets (<c>&lt;one&gt;</c>, <c>&lt;two&gt;</c> or <c>&lt;ten&gt;</c> in the
    /// arguments) and compares the files it writes with <paramref name="expected"/> of the unit
    /// names: the numbers padded to the digits of the count, or the sheet names.
    /// </summary>
    private static InvariantCase OutputNames(
        string product, string subject, int units, string[] args, Func<IEnumerable<string>, string[]> expected, bool perSheet = false, bool slow = false) =>
        Check("C-R12", subject, () => OutputNameProblems(product, units, args, expected, perSheet), shard: product, slow: slow);

    private static IEnumerable<string> OutputNameProblems(string product, int units, string[] args, Func<IEnumerable<string>, string[]> expected, bool perSheet)
    {
        using var workspace = new RuleWorkspace();
        string input = OperationRuns.SeedInput(workspace, product);
        (string? document, string[] sheets, string? failure) = MultiUnit(workspace, product, input, units);
        if (failure is not null)
        {
            yield return failure;
            yield break;
        }
        if (document is null)
        {
            yield break;
        }
        IReadOnlyDictionary<string, string> before = workspace.Snapshot();
        string token = units switch { 1 => "<one>", 2 => "<two>", _ => "<ten>" };
        RuleRun run = workspace.Json([.. args.Select(arg => arg == token ? document! : arg)]);
        if (run.ExitCode != 0)
        {
            if (!run.Unjudged)
            {
                yield return run.Describe();
            }
            yield break;
        }
        int digits = units.ToString(CultureInfo.InvariantCulture).Length;
        IEnumerable<string> names = perSheet
            ? sheets
            : Enumerable.Range(1, units).Select(number => number.ToString(CultureInfo.InvariantCulture).PadLeft(digits, '0'));
        string[] wanted = [.. expected(names).Order(StringComparer.Ordinal)];
        string[] written = [.. RuleWorkspace.Written(before, workspace.Snapshot()).Where(static path => !path.StartsWith('<'))];
        if (!written.SequenceEqual(wanted, StringComparer.Ordinal))
        {
            yield return $"wrote {string.Join(", ", written)}; expected {string.Join(", ", wanted)}";
        }
    }

    /// <summary>
    /// The fixture with one, two or ten pages, slides or sheets, with its sheet names; neither a
    /// document nor a failure when evaluation mode cannot build it.
    /// </summary>
    private static (string? Document, string[] Sheets, string? Failure) MultiUnit(RuleWorkspace workspace, string product, string input, int units)
    {
        string extension = ScenarioFixtures.PrimaryFormat(product);
        if (units == 1)
        {
            return (input, [], null);
        }
        string name = (units == 2 ? "two." : "ten.") + extension;
        RuleRun? run = null;
        if (CliCatalog.Current.Commands.Any(command => command.Words == $"{product} merge"))
        {
            run = workspace.Json([product, "merge", .. Enumerable.Repeat(input, units), "--out", name]);
        }
        else if (units == 2 && SecondUnit[product] is { } operations)
        {
            run = OperationRuns.Edit(workspace, product, input, name, JsonNode.Parse(operations)!.AsArray());
        }
        else if (units == 2)
        {
            File.Copy(workspace.File(input), workspace.File(name));
        }
        else
        {
            return (null, [], $"{product} cannot build a document of {units} units");
        }
        if (run is { ExitCode: not 0 })
        {
            return (null, [], run.Unjudged ? null : "building the document failed: " + run.Describe());
        }
        // The sheets the product sees, the warning sheet evaluation mode adds among them, as a
        // render of every sheet draws each one.
        RuleRun read = workspace.Json(product, "read", name, "--kind", "sheet");
        return (name, [.. OperationRuns.SheetObjects(read.Document)], null);
    }

    // ----- C-R15: a read or search over its budget truncates and continues -----------------------

    /// <summary>
    /// Every read and search, with each <c>--max-&lt;unit&gt;</c> budget at 1: it succeeds with a
    /// truncated window that names the next call, in the unit the command selects by (a search
    /// counts hits; a read that selects by no unit counts the unit its budget names, such as
    /// <c>object</c> for <c>--max-objects</c>), both for its default selection and for a selection
    /// of every unit.
    /// </summary>
    private static IEnumerable<InvariantCase> R15Run(CliCatalog catalog)
    {
        foreach (CliCommand command in catalog.Commands.Where(static command => command.Product is not null
            && command.Verb is "read" or "search"))
        {
            bool search = command.Has("--pattern");
            string? selection = SelectionUnits.Keys.FirstOrDefault(command.Has);
            foreach (CliOption budget in command.Options.Where(static option => option.Name.StartsWith("--max-", StringComparison.Ordinal)
                && option.Name != "--max-input-bytes"))
            {
                string unit = search ? "hit" : selection is null ? budget.Name["--max-".Length..].TrimEnd('s') : SelectionUnits[selection];
                string[][] selections = selection is null ? [[]] : [[], [selection, WholeSelections[selection]]];
                foreach (string[] selected in selections)
                {
                    Invocation invocation = Invocation.For(command, "--pattern");
                    if (search)
                    {
                        // A pattern with several matches in every fixture.
                        invocation.Add("--pattern", "o");
                    }
                    invocation.Add(selected).Add(budget.Name, "1");
                    string subject = $"{command}{(selected.Length == 0 ? string.Empty : " " + string.Join(' ', selected))} {budget.Name} 1";
                    yield return InvariantCases.Case("C-R15", subject, command.Product!, slow: false, invocation, new ScenarioExpectation
                    {
                        ExitCode = [0],
                        Json =
                        [
                            new JsonAssertion { Path = "window.truncated", Value = true },
                            new JsonAssertion { Path = "window.next", Exists = true },
                            new JsonAssertion { Path = "window.unit", Value = unit },
                        ],
                    });
                }
            }
        }
    }
}
