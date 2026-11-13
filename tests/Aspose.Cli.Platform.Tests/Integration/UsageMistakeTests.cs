using System.CommandLine.Parsing;
using System.Text.Json.Nodes;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Host.Tests;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>
/// A command line written from another tool's habits is answered with what corrects it in one
/// step: the option that lacks its value or has one too many, the command line that runs a command
/// of the root or a create as it should have been written, and the option a search calls otherwise.
/// A suggestion that is not known to be right is not given; a command line the error shows runs as
/// written.
/// </summary>
public sealed class UsageMistakeTests : IDisposable
{
    private const string BookPassword = "BOOK_PASSWORD";

    private readonly TempWorkspace _workspace = new();

    public UsageMistakeTests() => ScenarioLicense.Project(_workspace.Path);

    public void Dispose() => _workspace.Dispose();

    // -- An option without its value ------------------------------------------------------------

    [Theory]
    [InlineData("cells read b.xlsx --content", "--content", "Must be one of: formulas, full, styles, values")]
    [InlineData("pdf read c.pdf --pages", "--pages", null)]
    [InlineData("words read a.docx --license", "--license", null)]
    [InlineData("pdf search c.pdf --pattern --max-hits 5", "--pattern", null)]
    [InlineData("cells render b.xlsx --dpi --quiet", "--dpi", null)]
    [InlineData("cells search b.xlsx --pattern total --content --quiet", "--content", "Must be one of: all, formulas, values")]
    public void AnOptionBeforeAnotherOption_NeedsAValue_AndTheTokenAfterItIsNotBlamed(string commandLine, string option, string? values)
    {
        JsonNode error = Refused(commandLine, "USAGE_ERROR");

        string message = error["message"]!.GetValue<string>();
        Assert.StartsWith($"{option} needs a value.", message, StringComparison.Ordinal);
        Assert.Equal(option, error["details"]!["option"]!.GetValue<string>());
        Assert.DoesNotContain("'json'", message, StringComparison.Ordinal);
        Assert.DoesNotContain("Unrecognized", message, StringComparison.Ordinal);
        if (values is not null)
        {
            Assert.EndsWith(values, message.TrimEnd('.'), StringComparison.Ordinal);
        }
    }

    /// <summary>The name of an option is no value of an option with a list of them, even when the line parses without it.</summary>
    [Fact]
    public void AnOptionWithAListOfValues_BeforeASwitch_NeedsAValueRatherThanRunningWithTheSwitchAsItsValue()
    {
        Created("cells", "b.xlsx");

        CliResult result = _workspace.Run("cells", "read", "b.xlsx", "--content", "--quiet", "--output-format", "json");

        JsonNode error = Error(result, "USAGE_ERROR");
        Assert.StartsWith("--content needs a value.", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public void AnOptionAtTheEndOfTheLine_NeedsAValue()
    {
        CliResult result = _workspace.Run("cells", "read", "b.xlsx", "--content");

        JsonNode error = Error(result, "USAGE_ERROR");
        Assert.StartsWith("--content needs a value.", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal("--content", error["details"]!["option"]!.GetValue<string>());
    }

    /// <summary>A value that starts with a dash, even one that names an option, stays the value when the command line parses.</summary>
    [Theory]
    [InlineData("-x")]
    [InlineData("--regex")]
    [InlineData("--quiet")]
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
        Assert.Contains(option, error["hint"]!.GetValue<string>(), StringComparison.Ordinal);
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

    /// <summary>A bare argument beside a missing option it may stand for still suggests that option.</summary>
    [Fact]
    public void ABareArgumentBesideAMissingOption_StillSuggestsThatOption()
    {
        JsonNode error = Refused("pdf search c.pdf --pages 1 speaker", "USAGE_ERROR");

        Assert.Equal("--pattern", error["details"]!["suggestions"]![0]!.GetValue<string>());
    }

    // -- A command of the root written below a product -------------------------------------------

    [Theory]
    [InlineData("cells", "cells.xlsx")]
    [InlineData("pdf", "pdf.pdf")]
    [InlineData("slides", "slides.pptx")]
    [InlineData("words", "words.docx")]
    public void ReviewBelowAProduct_ShowsTheLineThatRunsIt(string product, string file)
    {
        File.WriteAllBytes(_workspace.File(file), ScenarioFixtures.Read(file));
        string line = $"aspose-cli review {file} --output-format json";

        JsonNode error = Refused($"{product} review {file}", "USAGE_ERROR");

        Assert.Equal([line], Suggestions(error));
        Assert.Contains($"'{line}'", error["hint"]!.GetValue<string>(), StringComparison.Ordinal);
        Reviewed(_workspace.RunCommandLine(line), _workspace.File(file + ".review"));
    }

    /// <summary>
    /// A word after preview that names one of its commands is that command, any other is the file
    /// its open reads; the line is only parsed, by the command tree, since preview starts a service.
    /// </summary>
    [Theory]
    [InlineData("cells preview stop", "aspose-cli preview stop --output-format json", "preview stop", null)]
    [InlineData("cells preview status", "aspose-cli preview status --output-format json", "preview status", null)]
    [InlineData("cells preview open", "aspose-cli preview open <file> --output-format json", "preview open", "<file>")]
    [InlineData("cells preview", "aspose-cli preview open <file> --output-format json", "preview open", "<file>")]
    [InlineData("cells preview open b.xlsx", "aspose-cli preview open b.xlsx --output-format json", "preview open", "b.xlsx")]
    [InlineData("cells preview b.xlsx", "aspose-cli preview open b.xlsx --output-format json", "preview open", "b.xlsx")]
    [InlineData("cells preview data.txt", "aspose-cli preview open data.txt --product cells --output-format json", "preview open", "data.txt")]
    public void PreviewBelowAProduct_ShowsTheLineThatParsesAsTheCommandMeant(string commandLine, string line, string command, string? file)
    {
        JsonNode error = Refused(commandLine, "USAGE_ERROR");

        Assert.Equal([line], Suggestions(error));
        Assert.Contains($"'{line}'", error["hint"]!.GetValue<string>(), StringComparison.Ordinal);
        ParsedInvocation parsed = ActualCommandTree.Parser.Parse(line.Split(' ')[1..]);
        Assert.Empty(parsed.ParseResult.Errors);
        Assert.Equal(command, string.Join(' ', parsed.CommandPath.Select(static item => item.Name)));
        Assert.Equal(file, parsed.ParseResult.CommandResult.Children.OfType<ArgumentResult>().SelectMany(static argument => argument.Tokens).FirstOrDefault()?.Value);
    }

    /// <summary>A file another product owns by default stays with the product written, and a product the caller also gave is kept.</summary>
    [Theory]
    [InlineData("cells review data.txt", "aspose-cli review data.txt --product cells --output-format json", "cells", "data.txt")]
    [InlineData("words review pdf.pdf", "aspose-cli review pdf.pdf --product words --output-format json", "words", "pdf.pdf")]
    [InlineData("cells review data.txt --product words", "aspose-cli review data.txt --product words --output-format json", "words", "data.txt")]
    public void ReviewBelowAProduct_OfAFileAnotherProductOwnsByDefault_KeepsTheProduct(string commandLine, string line, string product, string file)
    {
        File.WriteAllText(_workspace.File("data.txt"), "Name\tScore\nAnn\t5\nBob\t7\n");
        File.WriteAllBytes(_workspace.File("pdf.pdf"), ScenarioFixtures.Read("pdf.pdf"));

        JsonNode error = Refused(commandLine, "USAGE_ERROR");

        Assert.Equal([line], Suggestions(error));
        CliResult ran = _workspace.RunCommandLine(line);
        Reviewed(ran, _workspace.File(file + ".review"));
        Assert.Equal(product, JsonNode.Parse(ran.StdOut)!["product"]!.GetValue<string>());
    }

    /// <summary>The input's password variable and the other options the command takes are passed on, so it runs as the caller meant.</summary>
    [Fact]
    public void ReviewBelowAProduct_OfAnEncryptedInput_PassesItsPasswordVariableOn()
    {
        Dictionary<string, string?> password = Encrypted("book.xlsx");
        const string Line = "aspose-cli review book.xlsx --password-env BOOK_PASSWORD --out evidence --output-format json";

        CliResult result = _workspace.RunWithEnv(password,
            "cells", "review", "book.xlsx", "--password-env", BookPassword, "--out", "evidence", "--output-format", "json");

        JsonNode error = Error(result, "USAGE_ERROR");
        Assert.Equal([Line], Suggestions(error));
        Reviewed(_workspace.RunCommandLine(Line, password), _workspace.File("evidence"));
    }

    [Fact]
    public void ReviewBelowAProduct_OfAPathWithASpace_QuotesIt()
    {
        File.WriteAllBytes(_workspace.File("my book.xlsx"), ScenarioFixtures.Read("cells.xlsx"));
        const string Line = "aspose-cli review \"my book.xlsx\" --output-format json";

        CliResult result = _workspace.Run("cells", "review", "my book.xlsx", "--output-format", "json");

        JsonNode error = Error(result, "USAGE_ERROR");
        Assert.Equal([Line], Suggestions(error));
        Reviewed(_workspace.RunCommandLine(Line), _workspace.File("my book.xlsx.review"));
    }

    /// <summary>A line that would not run is not shown; the command with a placeholder is.</summary>
    [Theory]
    [InlineData("cells review")]
    [InlineData("cells review b.xlsx --sheets 1")]
    public void ReviewBelowAProduct_ThatCouldNotBeRepeated_ShowsThePlaceholder(string commandLine)
    {
        CliResult result = _workspace.Run([.. commandLine.Split(' '), "--output-format", "json"]);

        JsonNode error = Error(result, "USAGE_ERROR");
        Assert.Equal(["aspose-cli review <file> --output-format json"], Suggestions(error));
    }

    /// <summary>A password the line gave on stdin or literally is passed on by the quoted placeholder of its variable, as every printed command passes one on, and never repeated.</summary>
    [Theory]
    [InlineData("cells review b.xlsx --password-stdin")]
    [InlineData("cells review b.xlsx --password hunter2")]
    public void ReviewBelowAProduct_OfAPassword_PassesItOnByAVariable(string commandLine)
    {
        CliResult result = _workspace.Run([.. commandLine.Split(' '), "--output-format", "json"]);

        Assert.Equal(["aspose-cli review b.xlsx --password-env \"<password-env>\" --output-format json"], Suggestions(Error(result, "USAGE_ERROR")));
        Assert.DoesNotContain("hunter2", result.StdErr, StringComparison.Ordinal);
    }

    /// <summary>A command of the root that reads no product's file, and a mistyped command, keep what they said.</summary>
    [Theory]
    [InlineData("cells doctor", null)]
    [InlineData("cells reed b.xlsx", "cells read")]
    public void AnotherUnknownCommandBelowAProduct_KeepsItsSuggestions(string commandLine, string? suggestion)
    {
        JsonNode error = Refused(commandLine, "USAGE_ERROR");

        Assert.Equal(suggestion, error["details"]!["suggestions"]?[0]?.GetValue<string>());
    }

    // -- Help beside a value the option refuses --------------------------------------------------

    [Theory]
    [InlineData("--help --output-format bogus", "--output-format")]
    [InlineData("doctor --help --output-format bogus", "--output-format")]
    [InlineData("cells inspect --help --output-format bogus", "--output-format")]
    [InlineData("--help --timeout abc", "--timeout")]
    [InlineData("cells inspect --output-format bogus --help", "--output-format")]
    public void HelpBesideARefusedGlobalValue_RefusesThatValue(string commandLine, string option)
    {
        JsonNode error = Error(_workspace.Run(commandLine.Split(' ')), "OPTION_INVALID");

        Assert.Equal(option, error["details"]!["option"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("--help --output-format table")]
    [InlineData("doctor --help")]
    [InlineData("cells inspect --help --timeout 5")]
    [InlineData("cells inspect -h")]
    public void HelpBesideAcceptedGlobalValues_PrintsHelp(string commandLine)
    {
        CliResult result = _workspace.Run(commandLine.Split(' '));

        Assert.True(result.ExitCode == 0, result.StdErr);
        Assert.Contains("Usage:", result.StdOut, StringComparison.Ordinal);
    }

    // -- A create given its file as an option ----------------------------------------------------

    [Theory]
    [InlineData("pdf create --from-markdown in.md --out new.pdf", "aspose-cli pdf create new.pdf --from-markdown in.md --output-format json", "new.pdf")]
    [InlineData("words create --from-markdown in.md --out new.docx", "aspose-cli words create new.docx --from-markdown in.md --output-format json", "new.docx")]
    [InlineData("slides create --from-markdown in.md --output new.pptx", "aspose-cli slides create new.pptx --from-markdown in.md --output-format json", "new.pptx")]
    [InlineData("cells create --sheet-names Data -o new.xlsx", "aspose-cli cells create new.xlsx --sheet-names Data --output-format json", "new.xlsx")]
    [InlineData("cells create --out=new.xlsx", "aspose-cli cells create new.xlsx --output-format json", "new.xlsx")]
    [InlineData("cells create -o=new.xlsx", "aspose-cli cells create new.xlsx --output-format json", "new.xlsx")]
    public void ACreateGivenItsFileAsAnOption_ShowsTheLineWithTheFileAsItsArgument(string commandLine, string line, string file)
    {
        File.WriteAllText(_workspace.File("in.md"), "# Title\n\nBody\n");

        JsonNode error = Refused(commandLine, "USAGE_ERROR");

        Assert.Equal([line], Suggestions(error));
        Assert.Contains($"'{line}'", error["hint"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.False(File.Exists(_workspace.File(file)));
        Assert.Empty(Directory.GetFiles(_workspace.Path, "-*"));
        CliResult ran = _workspace.RunCommandLine(line);
        Assert.True(ran.ExitCode == 0, ran.StdErr);
        Assert.True(File.Exists(_workspace.File(file)));
    }

    /// <summary>
    /// The line keeps the output's password by its variable, or, for a password written on the
    /// command line, which it never repeats, by the placeholder of one, so what it creates with a
    /// variable in its place is protected.
    /// </summary>
    [Theory]
    [InlineData("--encrypt-env", BookPassword, BookPassword)]
    [InlineData("--encrypt", "create-hint-secret-77c1", "\"<password-env>\"")]
    public void ACreateGivenItsFileAsAnOption_KeepsTheEncryption(string option, string value, string variable)
    {
        var password = new Dictionary<string, string?> { [BookPassword] = "create-hint-secret-77c1" };
        string line = $"aspose-cli cells create new.xlsx --encrypt-env {variable} --output-format json";

        CliResult result = _workspace.RunWithEnv(password, "cells", "create", "--out", "new.xlsx", option, value, "--output-format", "json");

        Assert.Equal([line], Suggestions(Error(result, "USAGE_ERROR")));
        Assert.DoesNotContain("create-hint-secret-77c1", result.StdErr, StringComparison.Ordinal);
        CliResult ran = _workspace.RunCommandLine(line.Replace("\"<password-env>\"", BookPassword, StringComparison.Ordinal), password);
        Assert.True(ran.ExitCode == 0, ran.StdErr);
        Error(_workspace.Run("cells", "inspect", "new.xlsx", "--output-format", "json"), "PASSWORD_REQUIRED", exitCode: 3);
    }

    [Fact]
    public void ACreateGivenItsFileAsAnOption_QuotesAPathWithASpace()
    {
        const string Line = "aspose-cli cells create \"my book.xlsx\" --output-format json";

        JsonNode error = Error(_workspace.Run("cells", "create", "--out", "my book.xlsx", "--output-format", "json"), "USAGE_ERROR");

        Assert.Equal([Line], Suggestions(error));
        Assert.True(_workspace.RunCommandLine(Line).ExitCode == 0);
        Assert.True(File.Exists(_workspace.File("my book.xlsx")));
    }

    /// <summary>
    /// With no value to move, or the file already given, the command is shown with a placeholder;
    /// the options that merely look like the refused one are never suggested.
    /// </summary>
    [Theory]
    [InlineData("cells create y.xlsx --out z.xlsx", "aspose-cli cells create <file> --output-format json")]
    [InlineData("cells create --out", "aspose-cli cells create <file> --output-format json")]
    [InlineData("pdf create --out new.pdf --from-markdown in.md --bogus", "aspose-cli pdf create <file> --output-format json")]
    public void ACreateGivenItsFileAsAnOption_ThatCouldNotBeRepeated_ShowsThePlaceholder(string commandLine, string line)
    {
        CliResult result = _workspace.Run([.. commandLine.Split(' '), "--output-format", "json"]);

        JsonNode error = Error(result, "USAGE_ERROR");
        Assert.Equal([line], Suggestions(error));
        Assert.Equal("--out", error["details"]!["option"]!.GetValue<string>());
    }

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

    [Theory]
    [InlineData("pdf search c.pdf --query total")]
    [InlineData("pdf search c.pdf --find total")]
    [InlineData("pdf search c.pdf --text total")]
    [InlineData("words search a.docx --query total")]
    [InlineData("slides search d.pptx --find total")]
    [InlineData("cells search b.xlsx --query total")]
    public void ASearchGivenAnotherNameForItsPattern_PointsToPattern(string commandLine)
    {
        JsonNode error = Refused(commandLine, "USAGE_ERROR");

        Assert.Equal("--pattern", error["details"]!["suggestions"]![0]!.GetValue<string>());
        Assert.StartsWith("Did you mean '--pattern'?", error["hint"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public void ACellsSearchGivenTheScopeOfOtherProducts_PointsToContentAndSheet()
    {
        Created("cells", "b.xlsx");

        JsonNode error = Refused("cells search b.xlsx --scope formulas --pattern total", "USAGE_ERROR");

        Assert.Equal("--content", error["details"]!["suggestions"]![0]!.GetValue<string>());
        string hint = error["hint"]!.GetValue<string>();
        Assert.Contains("--content values|formulas|all", hint, StringComparison.Ordinal);
        Assert.Contains("--sheet", hint, StringComparison.Ordinal);
        Assert.Equal(0, _workspace.Run("cells", "search", "b.xlsx", "--content", "formulas", "--pattern", "total", "--output-format", "json").ExitCode);
    }

    /// <summary>The products whose search has <c>--scope</c> keep it.</summary>
    [Fact]
    public void TheScopeOfTheOtherProducts_StillParses()
    {
        Created("words", "a.docx");

        Assert.Equal(0, _workspace.Run("words", "search", "a.docx", "--pattern", "total", "--scope", "all", "--output-format", "json").ExitCode);
    }

    // -- A correction of a command line that holds a password --------------------------------------

    /// <summary>A password written on the command line is not repeated by the correction, which passes it on by the placeholder of its variable.</summary>
    [Theory]
    [InlineData("cells read file b.xlsx --password hunter2", "Run 'aspose-cli cells read b.xlsx --password-env \"<password-env>\" --output-format json'.")]
    [InlineData("cells read file b.xlsx --password=hunter2", "Run 'aspose-cli cells read b.xlsx --password-env \"<password-env>\" --output-format json'.")]
    [InlineData("words create file new.docx --encrypt hunter2", "Run 'aspose-cli words create new.docx --encrypt-env \"<password-env>\" --output-format json'.")]
    public void AnArgumentsNameBeforeItsValue_BesideALiteralPassword_PassesItOnByAVariableAndDoesNotRepeatIt(string commandLine, string hint)
    {
        CliResult result = _workspace.Run([.. commandLine.Split(' '), "--output-format", "json"]);

        JsonNode error = Error(result, "USAGE_ERROR");
        Assert.Equal("'file' stands for the <file> argument, whose value follows it.", error["message"]!.GetValue<string>());
        Assert.Equal(hint, error["hint"]!.GetValue<string>());
        Assert.DoesNotContain("hunter2", result.StdErr, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", result.StdOut, StringComparison.Ordinal);
    }

    /// <summary>The command meant is suggested, and its line passes the password on by the placeholder of its variable.</summary>
    [Theory]
    [InlineData("read b.xlsx --password hunter2", "cells read")]
    [InlineData("read file b.xlsx --password hunter2", "cells read")]
    [InlineData("inspect a.docx --password=hunter2", "words inspect")]
    public void AProductCommandWithoutItsProduct_BesideALiteralPassword_SuggestsTheCommandAndDoesNotRepeatIt(string commandLine, string suggestion)
    {
        CliResult result = _workspace.Run([.. commandLine.Split(' '), "--output-format", "json"]);

        JsonNode error = Error(result, "USAGE_ERROR");
        Assert.Equal(suggestion, error["details"]!["suggestions"]![0]!.GetValue<string>());
        Assert.StartsWith($"Did you mean '{suggestion}'?", error["hint"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Contains(" --password-env \"<password-env>\" ", error["hint"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", result.StdErr, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", result.StdOut, StringComparison.Ordinal);
    }

    /// <summary>The name of a variable is no secret, a password on stdin is passed on by the placeholder of its variable, and a line without a password is repeated as before.</summary>
    [Theory]
    [InlineData("cells read file b.xlsx --password-env BOOK_PASSWORD", "Run 'aspose-cli cells read b.xlsx --password-env BOOK_PASSWORD --output-format json'.")]
    [InlineData("words create file new.docx --encrypt-env BOOK_PASSWORD", "Run 'aspose-cli words create new.docx --encrypt-env BOOK_PASSWORD --output-format json'.")]
    [InlineData("read b.xlsx --password-env BOOK_PASSWORD", "Did you mean 'cells read'? Run 'aspose-cli cells read b.xlsx --password-env BOOK_PASSWORD --output-format json'.")]
    [InlineData("cells read file b.xlsx --password-stdin", "Run 'aspose-cli cells read b.xlsx --password-env \"<password-env>\" --output-format json'.")]
    [InlineData("cells read file b.xlsx --range A1", "Run 'aspose-cli cells read b.xlsx --range A1 --output-format json'.")]
    [InlineData("read b.xlsx --range A1", "Did you mean 'cells read'? Run 'aspose-cli cells read b.xlsx --range A1 --output-format json'.")]
    public void ACorrectionThatHoldsNoLiteralSecret_RepeatsTheCommandLine(string commandLine, string hint)
    {
        var password = new Dictionary<string, string?> { [BookPassword] = "correction-hint-secret-90be" };

        CliResult result = _workspace.RunWithEnv(password, [.. commandLine.Split(' '), "--output-format", "json"]);

        JsonNode error = Error(result, "USAGE_ERROR");
        Assert.Equal(hint, error["hint"]!.GetValue<string>());
    }

    // -- Support ------------------------------------------------------------------------------------

    private void Created(string product, string file) =>
        Assert.Equal(0, _workspace.Run(product, "create", file, "--output-format", "json").ExitCode);

    /// <summary>The fixture workbook written as <paramref name="file"/>, encrypted; returns the variable that opens it.</summary>
    private Dictionary<string, string?> Encrypted(string file)
    {
        var password = new Dictionary<string, string?> { [BookPassword] = "review-hint-secret-3a1f" };
        File.WriteAllBytes(_workspace.File("plain.xlsx"), ScenarioFixtures.Read("cells.xlsx"));
        CliResult created = _workspace.RunWithEnv(password,
            "cells", "convert", "plain.xlsx", "--to", "xlsx", "--out", file, "--encrypt-env", BookPassword, "--output-format", "json");
        Assert.True(created.ExitCode == 0, created.StdErr);
        return password;
    }

    /// <summary>A review ran: exit 8 is a review whose checks found errors, and it still writes its evidence.</summary>
    private static void Reviewed(CliResult result, string evidence)
    {
        Assert.True(result.ExitCode is 0 or 8, result.StdErr);
        Assert.True(File.Exists(Path.Combine(evidence, "review.json")));
    }

    private JsonNode Refused(string commandLine, string code) =>
        Error(_workspace.Run([.. commandLine.Split(' '), "--output-format", "json"]), code);

    private static JsonNode Error(CliResult result, string code, int exitCode = 2)
    {
        Assert.Equal(exitCode, result.ExitCode);
        JsonNode error = JsonNode.Parse(result.StdErr)!["error"]!;
        Assert.Equal(code, error["code"]!.GetValue<string>());
        return error;
    }

    private static string[] Suggestions(JsonNode error) =>
        [.. error["details"]!["suggestions"]!.AsArray().Select(static suggestion => suggestion!.GetValue<string>())];
}
