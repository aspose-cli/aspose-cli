using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>
/// A command line the parser cannot read is a <c>USAGE_ERROR</c>; one whose values the command
/// refuses is an <c>OPTION_INVALID</c>. Both name the option or argument they are about.
/// </summary>
public sealed class UsageErrorTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public UsageErrorTests()
    {
        CliResult created = _workspace.Run("words", "create", "a.docx", "--output-format", "json");
        Assert.True(created.ExitCode == 0, created.StdErr);
    }

    public void Dispose() => _workspace.Dispose();

    [Theory]
    [InlineData("words inspect a.docx --bogus", "option", "--bogus")]
    [InlineData("words split a.docx --out-dir parts", "option", "--by")]
    [InlineData("words read", "argument", "file")]
    public void ACommandLineThatDoesNotParse_IsAUsageErrorNamingTheParameter(string commandLine, string key, string parameter)
    {
        JsonNode error = Refused(commandLine, "USAGE_ERROR");

        Assert.Equal(parameter, error["details"]![key]!.GetValue<string>());
    }

    [Fact]
    public void AnUnknownCommand_IsAUsageError()
    {
        JsonNode error = Refused("words inspct a.docx", "USAGE_ERROR");

        Assert.Equal("words inspect", error["details"]!["suggestions"]![0]!.GetValue<string>());
    }

    /// <summary>
    /// A product's command written without its product, such as <c>read a.docx</c>, suggests the
    /// command of the product whose format the file's extension names, ready to run.
    /// </summary>
    [Theory]
    [InlineData("read file a.docx", "words read", "Run 'aspose-cli words read a.docx ")]
    [InlineData("read a.docx", "words read", "Run 'aspose-cli words read a.docx ")]
    [InlineData("inspect b.xlsx", "cells inspect", "Run 'aspose-cli cells inspect b.xlsx ")]
    public void AProductCommandWithoutItsProduct_SuggestsTheProductOfItsFile(string commandLine, string suggestion, string run)
    {
        Assert.Equal(0, _workspace.Run("cells", "create", "b.xlsx", "--output-format", "json").ExitCode);

        JsonNode error = Refused(commandLine, "USAGE_ERROR");

        Assert.Equal(suggestion, error["details"]!["suggestions"]![0]!.GetValue<string>());
        Assert.Contains(run, error["hint"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    /// <summary>A file whose extension no product claims suggests no product's command.</summary>
    [Fact]
    public void AProductCommandWithoutItsProduct_SuggestsNoProductForAnUnknownExtension()
    {
        JsonNode error = Refused("read a.zzq", "USAGE_ERROR");

        Assert.Null(error["details"]!["suggestions"]);
        Assert.DoesNotContain("read", error["hint"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("words inspect a.docx --detail bogus", "--detail")]
    [InlineData("words convert a.docx --to docx --out a.foo", "--out")]
    [InlineData("words render a.docx --dpi 99999", "--dpi")]
    [InlineData("words render a.docx --pages 1-x", "--pages")]
    [InlineData("words render a.docx --dpi abc", "--dpi")]
    public void AValueTheCommandRefuses_IsAnInvalidOptionNamingIt(string commandLine, string option)
    {
        JsonNode error = Refused(commandLine, "OPTION_INVALID");

        Assert.Equal(option, error["details"]!["option"]!.GetValue<string>());
        Assert.DoesNotContain("System.", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("words split a.docx --out-dir parts", "--by", "heading1, pages, sections")]
    [InlineData("pdf validate report.pdf", "--profile", "pdfa-1b, pdfa-2b, pdfa-3b")]
    public void AMissingOptionWithValues_ListsThem(string commandLine, string option, string values)
    {
        JsonNode error = Refused(commandLine, "USAGE_ERROR");

        Assert.Equal($"Option '{option}' is required. Must be one of: {values}", error["message"]!.GetValue<string>());
        Assert.Equal(values.Split(", "), error["details"]!["available"]!.AsArray().Select(static value => value!.GetValue<string>()));
        Assert.Equal(option, error["details"]!["option"]!.GetValue<string>());
    }

    [Fact]
    public void AMissingSecretVariable_OffersStandardInput()
    {
        JsonNode error = Refused("words inspect a.docx --password-env ASPOSE_CLI_TEST_UNSET_SECRET", "OPTION_INVALID");

        Assert.Equal("--password-env", error["details"]!["option"]!.GetValue<string>());
        Assert.Contains("--password-stdin", error["hint"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public void ANumberThatDoesNotParse_SaysWhatTheOptionTakes()
    {
        JsonNode error = Refused("words render a.docx --dpi abc", "OPTION_INVALID");

        Assert.Equal("'abc' is not a whole number, which --dpi takes.", error["message"]!.GetValue<string>());
    }

    [Fact]
    public void AGroupGivenAnUnknownOption_ListsItsCommandsAndPrintsItsHelp()
    {
        JsonNode error = Refused("cells --bogus", "USAGE_ERROR");
        CliResult table = _workspace.Run("cells", "--bogus", "--output-format", "table");
        string help = _workspace.Run("cells", "--help").StdOut;

        Assert.Equal("--bogus", error["details"]!["option"]!.GetValue<string>());
        Assert.StartsWith("'--bogus' is not an option of 'aspose-cli cells'. 'aspose-cli cells' needs a command.", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.DoesNotContain("Required command was not provided", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.All(error["details"]!["available"]!.AsArray(), command => Assert.StartsWith("cells ", command!.GetValue<string>(), StringComparison.Ordinal));
        Assert.Equal(2, table.ExitCode);
        Assert.Contains(help.ReplaceLineEndings("\n").Trim(), table.StdErr.ReplaceLineEndings("\n"), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("-?", "--help")]
    [InlineData("-o json", "--output-format")]
    public void AShortOptionTheRootDoesNotKnow_SuggestsTheLongOne(string commandLine, string suggestion)
    {
        JsonNode error = Refused(commandLine, "USAGE_ERROR");

        Assert.Equal(suggestion, error["details"]!["suggestions"]![0]!.GetValue<string>());
        Assert.StartsWith($"Did you mean '{suggestion}'?", error["hint"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public void AnArgumentsNameWrittenBeforeItsValue_HintsTheCommandLineWithoutIt()
    {
        JsonNode error = Refused("words read file a.docx", "USAGE_ERROR");

        Assert.Equal("'file' stands for the <file> argument, whose value follows it.", error["message"]!.GetValue<string>());
        Assert.Equal("Run 'aspose-cli words read a.docx --output-format json'.", error["hint"]!.GetValue<string>());
        Assert.Equal(0, _workspace.Run("words", "read", "a.docx", "--output-format", "json").ExitCode);
    }

    [Theory]
    [InlineData("words convert a.docx --to markdown", "md")]
    [InlineData("words inspect a.docx --detail all", "--detail bookmarks --detail comments --detail fields --detail fonts --detail images --detail outline --detail properties --detail revisions --detail sections --detail styles --detail tables")]
    public void AValueStandingForAnother_SuggestsIt(string commandLine, string suggestion)
    {
        JsonNode error = Refused(commandLine, "OPTION_INVALID");

        Assert.Equal(suggestion, error["details"]!["suggestions"]![0]!.GetValue<string>());
        Assert.StartsWith($"Did you mean '{suggestion}'?", error["hint"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public void HelpAsACommand_SaysHelpIsAnOption()
    {
        JsonNode error = Refused("words help edit", "USAGE_ERROR");

        Assert.Equal("'help' is not a command of 'aspose-cli words'; help is the --help option.", error["message"]!.GetValue<string>());
        Assert.Equal("words edit --help", error["details"]!["suggestions"]![0]!.GetValue<string>());
    }

    [Fact]
    public void AnOptionBesideTheVersion_ListsOnlyTheOptionTheVersionTakes()
    {
        JsonNode error = Refused("--version --bogus", "USAGE_ERROR");

        Assert.Equal(["--output-format"], error["details"]!["available"]!.AsArray().Select(static name => name!.GetValue<string>()));
        Assert.EndsWith("Must be one of: --output-format", error["message"]!.GetValue<string>().TrimEnd('.'), StringComparison.Ordinal);
    }

    /// <summary>
    /// The value written after an unknown option, or joined to it by '=' or ':', may be a secret
    /// meant for the option the caller mistyped: no output repeats it, the verbose diagnostics
    /// included, and the option it was meant for is still suggested.
    /// </summary>
    [Theory]
    [InlineData("pdf edit x.pdf --encryptt S3cretPw", "S3cretPw", "--encryptt", "--encrypt")]
    [InlineData("pdf edit x.pdf --encryptt=S3cretPw", "S3cretPw", "--encryptt", "--encrypt")]
    [InlineData("pdf edit x.pdf --encryptt:S3cretPw", "S3cretPw", "--encryptt", "--encrypt")]
    [InlineData("words create b.docx --pasword hunter2", "hunter2", "--pasword", null)]
    [InlineData("words create b.docx --pasword=hunter2", "hunter2", "--pasword", null)]
    [InlineData("words create b.docx --pasword:hunter2", "hunter2", "--pasword", null)]
    [InlineData("pdf merge a.pdf --encryptt=S3cretPw b.pdf --out o.pdf", "S3cretPw", "--encryptt", "--encrypt")]
    [InlineData("pdf --encryptt=S3cretPw", "S3cretPw", "--encryptt", null)]
    public void TheValueOfAnUnknownOption_IsNotRepeated(string commandLine, string value, string option, string? suggestion)
    {
        foreach (string[] extra in (string[][])[["--output-format", "json"], ["--output-format", "json", "-v"], ["--output-format", "compact", "-v"], ["--output-format", "table"]])
        {
            CliResult result = _workspace.Run([.. commandLine.Split(' '), .. extra]);

            Assert.Equal(2, result.ExitCode);
            Assert.DoesNotContain(value, result.StdOut, StringComparison.Ordinal);
            Assert.DoesNotContain(value, result.StdErr, StringComparison.Ordinal);
            Assert.Contains(option, result.StdErr, StringComparison.Ordinal);
            if (extra[1] == "json")
            {
                JsonNode error = JsonNode.Parse(result.StdErr)!["error"]!;
                Assert.Equal("USAGE_ERROR", error["code"]!.GetValue<string>());
                Assert.Equal(option, error["details"]!["option"]!.GetValue<string>());
                Assert.Equal(suggestion, error["details"]!["suggestions"]?[0]?.GetValue<string>());
            }
        }
    }

    /// <summary>An argument left over that follows no unknown option is still named.</summary>
    [Theory]
    [InlineData("words read a.docx extra.docx", "Unrecognized command or argument 'extra.docx'.")]
    [InlineData("words inspect a.docx --bogus", "Unrecognized command or argument '--bogus'.")]
    [InlineData("words inspect a.docx --bogus --detail fonts", "Unrecognized command or argument '--bogus'.")]
    public void AStrayArgument_IsStillNamed(string commandLine, string problem)
    {
        JsonNode error = Refused(commandLine, "USAGE_ERROR");

        Assert.Contains(problem, error["message"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    private JsonNode Refused(string commandLine, string code)
    {
        CliResult result = _workspace.Run([.. commandLine.Split(' '), "--output-format", "json"]);

        Assert.Equal(2, result.ExitCode);
        JsonNode error = JsonNode.Parse(result.StdErr)!["error"]!;
        Assert.Equal(code, error["code"]!.GetValue<string>());
        return error;
    }
}
