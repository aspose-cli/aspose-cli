using System.CommandLine.Parsing;
using System.Text.Json.Nodes;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Host.Tests;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>
/// A command line written from another tool's habits is refused with the option that lacks its
/// value or has one too many, and no output repeats a password written on it.
/// </summary>
public sealed class UsageMistakeTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public UsageMistakeTests() => ScenarioLicense.Project(_workspace.Path);

    public void Dispose() => _workspace.Dispose();

    // -- An option without its value ------------------------------------------------------------

    /// <summary>The name of an option is no value of an option with a list of them, even when the line parses without it.</summary>
    [Fact]
    public void AnOptionWithAListOfValues_BeforeASwitch_NeedsAValueRatherThanRunningWithTheSwitchAsItsValue()
    {
        Created("cells", "b.xlsx");

        CliResult result = _workspace.Run("cells", "read", "b.xlsx", "--content", "--verbose", "--output-format", "json");

        Error(result, "OPTION_INVALID");
    }

    [Fact]
    public void AnOptionAtTheEndOfTheLine_NeedsAValue()
    {
        CliResult result = _workspace.Run("cells", "read", "b.xlsx", "--content");

        JsonNode error = Error(result, "USAGE_ERROR");
        Assert.Contains("--content", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    /// <summary>A value that starts with a dash, even one that names an option, stays the value when the command line parses.</summary>
    [Theory]
    [InlineData("-x")]
    [InlineData("--regex")]
    [InlineData("--verbose")]
    [InlineData("--max-hits")]
    public void AValueStartingWithADash_StaysTheValueOfItsOption(string pattern)
    {
        Created("cells", "b.xlsx");

        CliResult result = _workspace.Run("cells", "search", "b.xlsx", "--pattern", pattern, "--output-format", "json");

        Assert.True(result.ExitCode == 0, result.StdErr);
        Assert.Equal(pattern, JsonNode.Parse(result.StdOut)!["pattern"]!.GetValue<string>());
    }

    /// <summary>A switch takes a value only when one is written, and an option beside it keeps its own.</summary>
    [Theory]
    [InlineData("cells create s1.xlsx --overwrite --sheet-names Data", "s1.xlsx")]
    [InlineData("cells create s2.xlsx --overwrite true", "s2.xlsx")]
    public void ASwitch_StaysOptional(string commandLine, string file)
    {
        CliResult result = _workspace.Run([.. commandLine.Split(' '), "--output-format", "json"]);

        Assert.True(result.ExitCode == 0, result.StdErr);
        Assert.True(File.Exists(_workspace.File(file)));
    }

    /// <summary>A value that starts with a dash is not blamed for a mistake it did not cause.</summary>
    [Fact]
    public void AValueStartingWithADash_IsNotBlamedForAnotherMistake()
    {
        JsonNode error = Refused("cells search b.xlsx --pattern --regex --bogus", "USAGE_ERROR");

        Assert.Equal("--bogus", error["details"]!["option"]!.GetValue<string>());
        Assert.DoesNotContain("needs a value", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    // -- An option with more values than it takes ------------------------------------------------

    [Theory]
    [InlineData("cells read b.xlsx --range A1 B2", "--range", "--range takes one value; 'B2' is one too many.")]
    [InlineData("cells read b.xlsx --range A1 A1", "--range", "--range takes one value; 'A1' is one too many.")]
    [InlineData("pdf search c.pdf --pattern total due", "--pattern", "--pattern takes one value; 'due' is one too many.")]
    [InlineData("pdf create new.pdf --from-images a.png b.png", "--from-images", "--from-images takes one value each time it is given; 'b.png' is left over.")]
    public void AnOptionGivenAnExtraValue_SaysHowManyItTakesAndNamesTheExtra(string commandLine, string option, string message)
    {
        JsonNode error = Refused(commandLine, "USAGE_ERROR");

        Assert.Equal(message, error["message"]!.GetValue<string>());
        Assert.Equal(option, error["details"]!["option"]!.GetValue<string>());
    }

    [Fact]
    public void EveryExtraValue_IsNamed()
    {
        JsonNode error = Refused("cells read b.xlsx --range A1 B2 C3", "USAGE_ERROR");

        Assert.Equal(
            ["--range takes one value; 'B2' is one too many.", "--range takes one value; 'C3' is one too many."],
            error["details"]!["errors"]!.AsArray().Select(static problem => problem!.GetValue<string>()));
    }

    /// <summary>What follows a password may be the rest of it, so no output repeats it, even beside a missing option a bare argument may stand for.</summary>
    [Theory]
    [InlineData("words read a.docx --password first-part second-part")]
    [InlineData("pdf search c.pdf --password first-part second-part")]
    public void TheExtraValueOfASecretOption_IsNotRepeated(string commandLine)
    {
        CliResult result = _workspace.Run([.. commandLine.Split(' '), "--output-format", "json"]);

        JsonNode error = Error(result, "USAGE_ERROR");
        Assert.Equal("--password", error["details"]!["option"]!.GetValue<string>());
        Assert.DoesNotContain("second-part", result.StdErr, StringComparison.Ordinal);
        Assert.DoesNotContain("first-part", result.StdErr, StringComparison.Ordinal);
    }

    // -- Help beside values ----------------------------------------------------------------------

    [Theory]
    [InlineData("--help --output-format compact")]
    [InlineData("cells inspect --help --timeout 5")]
    [InlineData("cells inspect -h")]
    public void HelpBesideAcceptedGlobalValues_PrintsHelp(string commandLine)
    {
        CliResult result = _workspace.Run(commandLine.Split(' '));

        Assert.True(result.ExitCode == 0, result.StdErr);
        Assert.Contains("Usage:", result.StdOut, StringComparison.Ordinal);
    }

    /// <summary>The parser gives help without checking values, so a value outside its option's list beside --help is not refused.</summary>
    [Theory]
    [InlineData("cells read --help --content bogus")]
    [InlineData("--help --output-format bogus")]
    public void HelpBesideAValueOutsideItsList_PrintsHelp(string commandLine)
    {
        CliResult result = _workspace.Run(commandLine.Split(' '));

        Assert.True(result.ExitCode == 0, result.StdErr);
        Assert.Contains("Usage:", result.StdOut, StringComparison.Ordinal);
    }

    // -- A create given its file as an option ----------------------------------------------------

    /// <summary>A create that names its file as its argument, and a value that only looks like the refused option, are left alone.</summary>
    [Fact]
    public void ACreate_WrittenAsItShouldBe_StillRuns()
    {
        File.WriteAllText(_workspace.File("in.md"), "# Title\n\nBody\n");

        Assert.Equal(0, _workspace.Run("pdf", "create", "new.pdf", "--from-markdown", "in.md", "--output-format", "json").ExitCode);
        Assert.Equal(0, _workspace.Run("words", "create", "titled.docx", "--title", "--out", "--output-format", "json").ExitCode);
        Assert.True(File.Exists(_workspace.File("new.pdf")));
        Assert.True(File.Exists(_workspace.File("titled.docx")));
    }

    // -- The names a search is written with from other tools ---------------------------------------

    /// <summary>The products whose search has <c>--scope</c> keep it.</summary>
    [Fact]
    public void TheScopeOfTheOtherProducts_StillParses()
    {
        Created("words", "a.docx");

        Assert.Equal(0, _workspace.Run("words", "search", "a.docx", "--pattern", "total", "--scope", "all", "--output-format", "json").ExitCode);
    }

    // -- A command line that holds a password -------------------------------------------------------

    /// <summary>A password written on the command line is not repeated by the error.</summary>
    [Theory]
    [InlineData("cells read file b.xlsx --password hunter2")]
    [InlineData("cells read file b.xlsx --password=hunter2")]
    [InlineData("words create file new.docx --encrypt hunter2")]
    public void AnArgumentsNameBeforeItsValue_BesideALiteralPassword_DoesNotRepeatIt(string commandLine)
    {
        CliResult result = _workspace.Run([.. commandLine.Split(' '), "--output-format", "json"]);

        Error(result, "USAGE_ERROR");
        Assert.DoesNotContain("hunter2", result.StdErr, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", result.StdOut, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("read b.xlsx --password hunter2")]
    [InlineData("read file b.xlsx --password hunter2")]
    [InlineData("inspect a.docx --password=hunter2")]
    public void AProductCommandWithoutItsProduct_BesideALiteralPassword_DoesNotRepeatIt(string commandLine)
    {
        CliResult result = _workspace.Run([.. commandLine.Split(' '), "--output-format", "json"]);

        Error(result, "USAGE_ERROR");
        Assert.DoesNotContain("hunter2", result.StdErr, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", result.StdOut, StringComparison.Ordinal);
    }

    // -- Support ------------------------------------------------------------------------------------

    private void Created(string product, string file) =>
        Assert.Equal(0, _workspace.Run(product, "create", file, "--output-format", "json").ExitCode);

    private JsonNode Refused(string commandLine, string code) =>
        Error(_workspace.Run([.. commandLine.Split(' '), "--output-format", "json"]), code);

    private static JsonNode Error(CliResult result, string code, int exitCode = 2)
    {
        Assert.Equal(exitCode, result.ExitCode);
        JsonNode error = JsonNode.Parse(result.StdErr)!["error"]!;
        Assert.Equal(code, error["code"]!.GetValue<string>());
        return error;
    }
}
