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
    [InlineData("words read a.docx --bogus", "option", "--bogus")]
    [InlineData("words read", "argument", "file")]
    public void ACommandLineThatDoesNotParse_IsAUsageErrorNamingTheParameter(string commandLine, string key, string parameter)
    {
        JsonNode error = Refused(commandLine, "USAGE_ERROR");

        Assert.Equal(parameter, error["details"]![key]!.GetValue<string>());
    }

    [Fact]
    public void AnUnknownCommand_IsAUsageError()
    {
        Refused("words inspct a.docx", "USAGE_ERROR");
    }

    [Theory]
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
        JsonNode error = Refused("words read a.docx --password-env ASPOSE_CLI_TEST_UNSET_SECRET", "OPTION_INVALID");

        Assert.Equal("--password-env", error["details"]!["option"]!.GetValue<string>());
    }

    [Fact]
    public void ANumberThatDoesNotParse_SaysWhatTheOptionTakes()
    {
        JsonNode error = Refused("words render a.docx --dpi abc", "OPTION_INVALID");

        Assert.Equal("'abc' is not a whole number, which --dpi takes.", error["message"]!.GetValue<string>());
    }

    [Fact]
    public void AGroupGivenAnUnknownOption_ListsItsCommands()
    {
        JsonNode error = Refused("cells --bogus", "USAGE_ERROR");

        Assert.Equal("--bogus", error["details"]!["option"]!.GetValue<string>());
        Assert.StartsWith("'--bogus' is not an option of 'aspose-cli cells'. 'aspose-cli cells' needs a command.", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.DoesNotContain("Required command was not provided", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.All(error["details"]!["available"]!.AsArray(), command => Assert.StartsWith("cells ", command!.GetValue<string>(), StringComparison.Ordinal));
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
    /// included.
    /// </summary>
    [Theory]
    [InlineData("pdf edit x.pdf --encryptt S3cretPw", "S3cretPw", "--encryptt")]
    [InlineData("pdf edit x.pdf --encryptt=S3cretPw", "S3cretPw", "--encryptt")]
    [InlineData("pdf edit x.pdf --encryptt:S3cretPw", "S3cretPw", "--encryptt")]
    [InlineData("words create b.docx --pasword hunter2", "hunter2", "--pasword")]
    [InlineData("words create b.docx --pasword=hunter2", "hunter2", "--pasword")]
    [InlineData("words create b.docx --pasword:hunter2", "hunter2", "--pasword")]
    [InlineData("pdf merge a.pdf --encryptt=S3cretPw b.pdf --out o.pdf", "S3cretPw", "--encryptt")]
    [InlineData("pdf --encryptt=S3cretPw", "S3cretPw", "--encryptt")]
    [InlineData("cells read b.xlsx --content --password S3cretPw", "S3cretPw", "--content")]
    public void TheValueOfAnUnknownOption_IsNotRepeated(string commandLine, string value, string option)
    {
        foreach (string[] extra in (string[][])[["--output-format", "json"], ["--output-format", "json", "-v"], ["--output-format", "compact", "-v"]])
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
            }
        }
    }

    /// <summary>An argument left over that follows no unknown option is still named.</summary>
    [Theory]
    [InlineData("words read a.docx extra.docx", "Unrecognized command or argument 'extra.docx'.")]
    [InlineData("words read a.docx --bogus", "Unrecognized command or argument '--bogus'.")]
    [InlineData("words read a.docx --bogus --kind paragraph", "Unrecognized command or argument '--bogus'.")]
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
