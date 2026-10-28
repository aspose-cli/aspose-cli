using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.Invariants.Tests;

/// <summary>
/// <see cref="ExecutableHints"/> runs what a hint names on the invocation's own files and reports
/// a hint whose command or operation the CLI refuses as written. Every hint text here is unique,
/// since a text is judged once per process.
/// </summary>
public sealed class ExecutableHintTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public ExecutableHintTests()
    {
        ScenarioLicense.Project(_workspace.Path);
        File.WriteAllBytes(_workspace.File("book.xlsx"), ScenarioFixtures.Read("cells.xlsx"));
    }

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public void QuotedCommand_ThatTheCliRefuses_IsReported()
    {
        IReadOnlyList<string> problems = ErrorHint("List the sheets with 'aspose-cli cells inspect --nope'.");

        string problem = Assert.Single(problems);
        Assert.Contains("'aspose-cli cells inspect --nope'", problem, StringComparison.Ordinal);
        Assert.Contains("fails with USAGE_ERROR", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void QuotedCommand_ThatRuns_IsNotReported() =>
        Assert.Empty(ErrorHint("List the sheets with 'aspose-cli cells inspect <file> --sample'."));

    [Fact]
    public void ShortCommand_TakesTheInvocationsInput()
    {
        Assert.Empty(ErrorHint("Read the cells with 'cells read --range A1:B2'."));
        Assert.Contains("fails with OPTION_INVALID", Assert.Single(ErrorHint("Read the cells with 'cells read --range NOT-A-RANGE'.")), StringComparison.Ordinal);
    }

    [Fact]
    public void CommandNamedByItsOptions_IsCheckedAgainstTheCatalog()
    {
        Assert.Empty(ErrorHint("Write a copy with 'aspose-cli cells edit --out'."));
        Assert.Contains("USAGE_ERROR", Assert.Single(ErrorHint("Write a copy with 'aspose-cli cells edit --copy-to'.")), StringComparison.Ordinal);
    }

    [Fact]
    public void UnquotedCommand_RunsToTheEndOfItsSentence()
    {
        Assert.Empty(ErrorHint("Convert it: aspose-cli cells convert book.xlsx --to csv (or another text format)."));
        Assert.Contains("fails with", Assert.Single(ErrorHint("Convert it: aspose-cli cells convert book.xlsx --to nope (or another text format).")), StringComparison.Ordinal);
    }

    [Fact]
    public void WritingCommand_RunsInACopyOfTheWorkspace()
    {
        string outside = Path.Combine(Path.GetTempPath(), "aspose-cli-hint-" + Guid.NewGuid().ToString("N") + ".csv");
        string[] before = [.. Directory.EnumerateFileSystemEntries(_workspace.Path)];

        Assert.Empty(ErrorHint($"Export it with 'aspose-cli cells convert <file> --to csv --out {outside}'."));

        Assert.Equal(before, Directory.EnumerateFileSystemEntries(_workspace.Path));
        Assert.False(File.Exists(outside));
    }

    [Fact]
    public void UnfilledPlaceholder_IsNotExecutable() =>
        Assert.Contains("cannot fill <sheet>", Assert.Single(ErrorHint("Render it with 'aspose-cli cells render <file> --sheet <sheet>'.")), StringComparison.Ordinal);

    [Fact]
    public void PlaceholderOfSeveralWords_NamedByAFile_TakesTheInput()
    {
        Assert.Empty(ErrorHint("Write xlsx, then run 'aspose-cli cells convert <that file> --to pdf'."));
        Assert.Contains("cannot fill <that sheet>", Assert.Single(ErrorHint("Then run 'aspose-cli cells read <file> --sheet <that sheet>'.")), StringComparison.Ordinal);
    }

    [Fact]
    public void FileTheInvocationDoesNotHave_LeavesTheCommandToTheCatalog()
    {
        HintRun create = new(["slides", "create", "x.ppt", "--output-format", "json"], _workspace.Path, Path.GetDirectoryName(_workspace.ConfigDirectory)!, Variables: null);

        Assert.Empty(ExecutableHints.Problems(create, Error("Create a .pptx, then run 'aspose-cli slides convert <that file> --to ppt'.")));
        Assert.Contains("--to of 'slides convert' does not accept 'nope'",
            Assert.Single(ExecutableHints.Problems(create, Error("Create a .pptx, then run 'aspose-cli slides convert <that file> --to nope'."))), StringComparison.Ordinal);
        Assert.Contains("'slides convert' has no option --nope",
            Assert.Single(ExecutableHints.Problems(create, Error("Create a .pptx, then run 'aspose-cli slides convert <that file> --nope'."))), StringComparison.Ordinal);
    }

    [Fact]
    public void GroupUsage_StandsForOneOfItsCommands()
    {
        Assert.Empty(ErrorHint("Run 'aspose-cli cells <command>'; 'aspose-cli cells --help' describes each one."));
        Assert.Contains("cannot fill <command>", Assert.Single(ErrorHint("Run 'aspose-cli cells inspect <command>'.")), StringComparison.Ordinal);
        Assert.Contains("fails with USAGE_ERROR", Assert.Single(ErrorHint("Run 'aspose-cli cells <workbook>' to open it.")), StringComparison.Ordinal);
    }

    [Fact]
    public void BareOption_IsCheckedAgainstTheInvocationsCommand()
    {
        Assert.Empty(ErrorHint("Add --sample to see rows, or --detail or more --sample-rows."));
        Assert.Empty(ErrorHint("A flag such as --sample takes no value, so --sample replaces nothing."));
        Assert.Contains("'cells inspect' has no option --dpi", Assert.Single(ErrorHint("Lower --dpi.")), StringComparison.Ordinal);
        Assert.Contains("--detail of 'cells inspect' does not accept 'nope'", Assert.Single(ErrorHint("Add --detail nope to see it.")), StringComparison.Ordinal);
    }

    [Fact]
    public void RepeatableOption_TakesOneValuePerOccurrenceInTheCatalogCheck()
    {
        HintRun create = new(["cells", "create", "new.xlsx", "--output-format", "json"], _workspace.Path, Path.GetDirectoryName(_workspace.ConfigDirectory)!, Variables: null);

        Assert.Empty(ExecutableHints.Problems(create, Error("Check them with 'aspose-cli cells inspect <file> --detail charts --detail names'.")));
        Assert.Contains("'cells inspect' takes no argument 'names'",
            Assert.Single(ExecutableHints.Problems(create, Error("Check them with 'aspose-cli cells inspect <file> --detail charts names'."))), StringComparison.Ordinal);
    }

    [Fact]
    public void LenientCatalogCheck_DoesNotStandInForARun()
    {
        const string Hint = "Read it with 'aspose-cli cells read <file> --range NOT-A-RANGE-KEY'.";
        HintRun create = new(["cells", "create", "new.xlsx", "--output-format", "json"], _workspace.Path, Path.GetDirectoryName(_workspace.ConfigDirectory)!, Variables: null);

        Assert.Empty(ExecutableHints.Problems(create, Error(Hint)));
        Assert.Contains("fails with OPTION_INVALID", Assert.Single(ErrorHint(Hint)), StringComparison.Ordinal);
    }

    /// <summary>
    /// A hint that runs on the invocation's file is judged on that file's content: the same file
    /// name in another workspace, holding another workbook, is run again, not answered from the
    /// first one's outcome.
    /// </summary>
    [Fact]
    public void SameFileNameWithOtherContent_IsJudgedAgain()
    {
        const string Hint = """Apply {"op":"set_active_sheet","sheet":"SameName"} to show that sheet first.""";
        using TempWorkspace visible = WorkbookWithSheet("visible");
        using TempWorkspace hidden = WorkbookWithSheet("hidden");

        // A visible sheet can be made active.
        Assert.Empty(ExecutableHints.Problems(InspectRun(visible), Warning(Hint)));
        // A hidden one cannot, so the CLI refuses the operation as written.
        Assert.Contains("fails with OPS_INVALID", Assert.Single(ExecutableHints.Problems(InspectRun(hidden), Warning(Hint))), StringComparison.Ordinal);

        TempWorkspace WorkbookWithSheet(string visibility)
        {
            var workspace = new TempWorkspace();
            ScenarioLicense.Project(workspace.Path);
            File.Copy(_workspace.File("book.xlsx"), workspace.File("book.xlsx"));
            string ops = $$"""{"ops":[{"op":"insert_sheet","name":"SameName"},{"op":"set_sheet_visibility","sheet":"SameName","visibility":"{{visibility}}"}]}""";
            Assert.Equal(0, workspace.Run("cells", "edit", "book.xlsx", "--in-place", "--ops", ops).ExitCode);
            return workspace;
        }

        static HintRun InspectRun(TempWorkspace workspace) =>
            new(["cells", "inspect", "book.xlsx", "--output-format", "json"], workspace.Path, Path.GetDirectoryName(workspace.ConfigDirectory)!, Variables: null);
    }

    /// <summary>
    /// The known-violations check runs a case with a check through the hint collector, so a hint
    /// of the check's runs that does not run is a problem of the case's invariant.
    /// </summary>
    [Fact]
    public void KnownViolations_RunsARuleCaseWithItsHints()
    {
        string envelope = new JsonObject
        {
            ["schema"] = "https://schemas.aspose.com/aspose-cli/v2/common/error.schema.json",
            ["schemaVersion"] = 2,
            ["error"] = new JsonObject { ["code"] = "RENDER_EMPTY", ["message"] = "Synthetic.", ["hint"] = "List the sheets with 'aspose-cli cells inspect --nope-known'." },
        }.ToJsonString();
        var item = new InvariantCase("C-R99 | synthetic", "C-R99", "cells", Slow: false, Scenario: null, Check: () =>
        {
            using var workspace = new RuleWorkspace();
            workspace.Seed("book.xlsx", ScenarioFixtures.Read("cells.xlsx"));
            Assert.Equal("RENDER_EMPTY", workspace.Json("cells", "inspect", "book.xlsx").Error);
            return [];
        });

        IReadOnlyList<ScenarioProblem> problems;
        using (RuleWorkspace.RewriteResults(_ => new CliResult(2, string.Empty, envelope)))
        {
            problems = KnownViolations.Problems(item);
        }

        ScenarioProblem hint = Assert.Single(problems);
        Assert.Equal("C-R99", KnownViolations.InvariantOf(item, hint.Check));
        Assert.StartsWith(ExecutableHints.Check + ": the RENDER_EMPTY hint 'aspose-cli cells inspect --nope-known'", hint.Message, StringComparison.Ordinal);
        Assert.Contains("fails with USAGE_ERROR", hint.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RuleCase_ReportsTheHintsOfItsRunsUnderItsInvariant()
    {
        string envelope = new JsonObject
        {
            ["schema"] = "https://schemas.aspose.com/aspose-cli/v2/common/error.schema.json",
            ["schemaVersion"] = 2,
            ["error"] = new JsonObject { ["code"] = "RENDER_EMPTY", ["message"] = "Synthetic.", ["hint"] = "List the sheets with 'aspose-cli cells inspect --nope-rule'." },
        }.ToJsonString();
        IReadOnlyList<ScenarioProblem> problems;
        using (RuleWorkspace.RewriteResults(_ => new CliResult(2, string.Empty, envelope)))
        {
            problems = RuleWorkspace.CollectHintProblems("C-R99", () =>
            {
                using var workspace = new RuleWorkspace();
                workspace.Seed("book.xlsx", ScenarioFixtures.Read("cells.xlsx"));
                Assert.Equal("RENDER_EMPTY", workspace.Json("cells", "inspect", "book.xlsx").Error);
                return [new ScenarioProblem(0, "C-R99", "the rule's own problem")];
            });
        }

        Assert.Equal(2, problems.Count);
        ScenarioProblem hint = problems[1];
        Assert.Equal("C-R99", hint.Check);
        Assert.StartsWith(ExecutableHints.Check + ": the RENDER_EMPTY hint 'aspose-cli cells inspect --nope-rule'", hint.Message, StringComparison.Ordinal);
        Assert.Contains("fails with USAGE_ERROR", hint.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void JsonOperation_ThatTheCliRefuses_IsReported()
    {
        Assert.Contains("fails with OPS_INVALID",
            Assert.Single(WarningHint("""Apply {"op":"set_values","sheet":"Sheet1","range":"A1"} to fill it.""")), StringComparison.Ordinal);
        Assert.Empty(WarningHint("""Apply {"op":"set_values","sheet":"Sheet1","range":"A1","values":[[1]]} to fill it."""));
        Assert.Empty(WarningHint("""Apply [{"op":"set_values","sheet":"Sheet1","range":"B1","values":[[2]]}] to fill them."""));
    }

    [Fact]
    public void OperationName_MustBeAnOperationOfTheProduct()
    {
        Assert.Empty(WarningHint("To remove the filter, use clear_autofilter."));
        Assert.Contains("cells has no operation 'clear_autofilters'", Assert.Single(WarningHint("To remove the filter, use clear_autofilters.")), StringComparison.Ordinal);
    }

    [Fact]
    public void ProseAndNames_AreNotCommands() =>
        Assert.Empty(ErrorHint("Choose a sheet such as 'Sales'; the sheet's name comes from 'Did you mean' lists, shift_jis or 'slide'."));

    /// <summary>
    /// The real <c>ENCRYPTION_REMOVED</c> hint of a Words document converted to PDF names the
    /// encrypting command by its path and <c>--in-place</c> alone: a quoted PDF command the catalog
    /// accepts, with no operations of its own to run against the Words input.
    /// </summary>
    [Fact]
    public void QuotedCommandPath_OfTheEncryptionHint_IsCheckedAgainstTheCatalog()
    {
        File.WriteAllBytes(_workspace.File("locked.docx"), ScenarioFixtures.Read("words.encrypted"));
        string[] args = ["words", "convert", "locked.docx", "--password-env", "DOC_PASSWORD", "--to", "pdf", "--out", "plain.pdf", "--output-format", "json"];
        var variables = new Dictionary<string, string?> { ["DOC_PASSWORD"] = ScenarioFixtures.Password };
        JsonNode result = _workspace.RunWithEnv(variables, args).Json();
        string hint = result["warnings"]!.AsArray().Single(static warning => warning!["code"]!.GetValue<string>() == "ENCRYPTION_REMOVED")!["hint"]!.GetValue<string>();

        HintAction action = Assert.Single(ExecutableHints.Extract(CliCatalog.Current, hint), static action => action.Kind != HintKind.Option);
        Assert.Equal(HintKind.Command, action.Kind);
        Assert.Equal(["pdf", "edit", "--in-place"], action.Tokens);
        Assert.Empty(ExecutableHints.Problems(new HintRun(args, _workspace.Path, Path.GetDirectoryName(_workspace.ConfigDirectory)!, variables), result));
    }

    /// <summary>
    /// A <c>&lt;...-env&gt;</c> placeholder in the operations of a quoted command takes the variable
    /// the run sets; without one the command is left to the catalog and its operations to the ops
    /// schema, which still reports a field the operation does not have.
    /// </summary>
    [Fact]
    public void VariablePlaceholder_TakesTheRunsVariableOrLeavesTheOperationsToTheSchema()
    {
        File.WriteAllBytes(_workspace.File("doc.pdf"), ScenarioFixtures.Read("pdf.pdf"));
        const string Hint = "If the output must stay protected, encrypt the pdf file afterwards with an owner password only, which restricts it as the input was and keeps it opening without a password, with each <...> naming the environment variable that holds that password: `aspose-cli pdf edit '<file>' --in-place --ops '{\"ops\":[{\"op\":\"encrypt_document\",\"ownerPasswordEnv\":\"<owner-password-env>\",\"permissions\":{\"print\":false,\"copy\":false}}]}'`.";
        string[] args = ["pdf", "split", "doc.pdf", "--by", "pages", "--out-dir", "parts", "--output-format", "json"];
        string config = Path.GetDirectoryName(_workspace.ConfigDirectory)!;

        Assert.Empty(ExecutableHints.Problems(new HintRun(args, _workspace.Path, config, Variables: null), Warning(Hint)));
        Assert.Empty(ExecutableHints.Problems(new HintRun(args, _workspace.Path, config, new Dictionary<string, string?> { ["OWNER_SECRET"] = "Owner-5e1d" }), Warning(Hint)));
        Assert.Contains("the operations does not conform to",
            Assert.Single(ExecutableHints.Problems(new HintRun(args, _workspace.Path, config, Variables: null), Warning(Hint.Replace("\"permissions\"", "\"permission\"", StringComparison.Ordinal)))),
            StringComparison.Ordinal);
    }

    /// <summary>Global options before the command path still leave that command to judge an option the hint names alone.</summary>
    [Fact]
    public void BareOption_IsJudgedByTheCommandAfterLeadingGlobalOptions()
    {
        Directory.CreateDirectory(_workspace.File("sub"));
        string[] args = ["--workdir", _workspace.File("sub"), "--max-input-bytes=64", "pdf", "create", "--from-text", "source.txt", "output.pdf", "--output-format", "json"];

        Assert.Empty(ExecutableHints.Problems(new HintRun(args, _workspace.Path, Path.GetDirectoryName(_workspace.ConfigDirectory)!, Variables: null),
            Error("Pass --overwrite to replace it, or choose a different path with file.")));
    }

    private static JsonObject Warning(string hint) => new()
    {
        ["warnings"] = new JsonArray(new JsonObject { ["code"] = "ENCRYPTION_REMOVED", ["message"] = "Synthetic.", ["hint"] = hint }),
    };

    private IReadOnlyList<string> ErrorHint(string hint) => ExecutableHints.Problems(Run(), Error(hint));

    private static JsonObject Error(string hint) => new()
    {
        ["error"] = new JsonObject { ["code"] = "RENDER_EMPTY", ["message"] = "Synthetic.", ["hint"] = hint },
    };

    private IReadOnlyList<string> WarningHint(string hint) => ExecutableHints.Problems(Run(), new JsonObject
    {
        ["warnings"] = new JsonArray(new JsonObject { ["code"] = "OP_NO_EFFECT", ["message"] = "Synthetic.", ["hint"] = hint }),
    });

    private HintRun Run() =>
        new(["cells", "inspect", "book.xlsx", "--output-format", "json"], _workspace.Path, Path.GetDirectoryName(_workspace.ConfigDirectory)!, Variables: null);
}
