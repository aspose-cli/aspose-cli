using System.CommandLine;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Ports;
using Aspose.Cli.Sdk.Rendering;
using Aspose.Cli.Sdk.Tests;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Sdk;

public sealed class StandardCommandTests : IDisposable
{
    private static readonly InputDocument Report = new("Report to open.", "the report");
    private static readonly EncryptedOutput Encrypted = new("the output report", ["secure"]);

    private readonly TempDirectory _temp = new();
    private readonly TestHost _host;

    public StandardCommandTests()
    {
        File.WriteAllText(_temp.File("report.test"), "report");
        File.WriteAllText(_temp.File("other.test"), "other");
        _host = new TestHost(_temp.Path);
    }

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Create_PutsTheArgumentThenProductOptionsThenCommonOptionsInTheFixedOrder()
    {
        Command command = Create(
            new CommandTraits
            {
                Input = Report,
                Output = OutputTarget.File("Output path."),
                Encrypt = Encrypted,
                UsesFonts = true,
            },
            (_, _) => Result());

        Assert.Equal(["file"], command.Arguments.Select(static argument => argument.Name));
        Assert.Equal(InputKind.File, command.Arguments[0].GetParameterMetadata().InputKind);
        Assert.Equal(
            ["--mode", "--out", "--overwrite", "--password", "--password-env", "--password-stdin", "--encrypt", "--encrypt-env", "--font-dir"],
            command.Options.Select(static option => option.Name));
        Assert.Equal("Password for the report. Discouraged: visible in the process list; prefer --password-env.",
            command.Options.Single(static option => option.Name == "--password").Description);
        Assert.Equal("Name of an environment variable holding the password for the output report.",
            command.Options.Single(static option => option.Name == "--encrypt-env").Description);
        command.ValidateParameters();
    }

    [Fact]
    public void Create_NamesEachPasswordAfterItsDocumentWhenTwoAreRead()
    {
        (string? Left, string? Right) passwords = default;
        Command command = Create(
            new CommandTraits
            {
                Input = new InputDocument("Baseline.", "the baseline", "left"),
                Other = new InputDocument("Candidate.", "the candidate", "right"),
            },
            (_, standard) =>
            {
                passwords = (standard.InputPassword, standard.OtherPassword);
                return Result(standard.Input + "|" + standard.Other);
            });

        Assert.Equal(["left", "right"], command.Arguments.Select(static argument => argument.Name));
        Assert.Equal(
            ["--mode", "--left-password", "--left-password-env", "--right-password", "--right-password-env"],
            command.Options.Select(static option => option.Name));
        Assert.Equal(
            _temp.File("report.test") + "|" + _temp.File("other.test"),
            Run(command, "report.test", "other.test", "--left-password", "a", "--right-password-env", "RIGHT"));
        Assert.Equal(("a", "b"), passwords);
    }

    [Fact]
    public void Create_PublishesAFileSetOrACreatedFileBesideOverwrite()
    {
        Command directory = Create(
            new CommandTraits { Input = Report, Output = OutputTarget.Directory("Parts.") },
            (_, standard) => Result(standard.OutputDirectory + "|" + standard.Overwrite));
        Command created = Create(
            new CommandTraits { Output = OutputTarget.CreatedFile("File to create.") },
            (_, standard) => Result(standard.CreatedPath + "|" + standard.Overwrite));

        Assert.Equal(["--mode", "--out-dir", "--overwrite", "--password", "--password-env", "--password-stdin"],
            directory.Options.Select(static option => option.Name));
        Assert.NotEmpty(directory.Parse(["report.test"]).Errors);
        Assert.Equal(_temp.File("parts") + "|True", Run(directory, "report.test", "--out-dir", "parts", "--overwrite"));
        Assert.Equal(["file"], created.Arguments.Select(static argument => argument.Name));
        Assert.Equal(InputKind.None, created.Arguments[0].GetParameterMetadata().InputKind);
        Assert.Equal(["--mode", "--overwrite"], created.Options.Select(static option => option.Name));
        Assert.Equal(_temp.File("new.test") + "|False", Run(created, "new.test"));
    }

    [Fact]
    public void Create_RejectsTraitsThatContradictEachOther()
    {
        Assert.Throws<ArgumentException>(() => Create(new CommandTraits { Other = Report }, (_, _) => Result()));
        Assert.Throws<ArgumentException>(() => Create(
            new CommandTraits { Input = Report, Output = OutputTarget.CreatedFile("New.") }, (_, _) => Result()));
        Assert.Throws<ArgumentException>(() => Create(
            new CommandTraits { Input = Report, Other = Report }, (_, _) => Result()));
    }

    [Fact]
    public void OutputPath_DerivesASiblingAndNeverNamesAnInput()
    {
        Command command = Create(
            new CommandTraits { Input = Report, Output = OutputTarget.File("Output path.") },
            (_, standard) => Result(standard.OutputPath(".out")));

        Assert.Equal(_temp.File("report.out"), Run(command, "report.test"));
        Assert.Equal(_temp.File("copy.out"), Run(command, "report.test", "--out", "copy.out"));
        CliException error = RunFailing(command, "report.test", "--out", "REPORT.TEST");
        Assert.Equal(ErrorCodes.OptionInvalid, error.Code);
        Assert.Equal("--out", error.Details!["option"]!.GetValue<string>());
    }

    [Fact]
    public void CreatedPath_NeverNamesAFileAProductOptionReads()
    {
        var template = new Option<string?>("--template").WithInput(InputKind.File);
        Command command = StandardCommand.Create(
            _host,
            "create",
            "Creates.",
            new CommandTraits { Output = OutputTarget.CreatedFile("File to create.") },
            [template],
            (_, standard) => Result(standard.CreatedPath));

        CliException error = RunFailing(command, "Report.test", "--template", "report.test");

        Assert.Equal(ErrorCodes.OptionInvalid, error.Code);
        Assert.Equal("file", error.Details!["option"]!.GetValue<string>());
        Assert.Equal(_temp.File("new.test"), Run(command, "new.test", "--template", "report.test"));
    }

    [Fact]
    public void ProductArguments_FollowTheDocumentsAndTheirFilesAreNeverAnOutput()
    {
        var files = new Argument<string[]>("files") { Arity = ArgumentArity.OneOrMore }.WithInput(InputKind.File);
        Command command = StandardCommand.Create(
            _host,
            "merge",
            "Merges.",
            new CommandTraits { PasswordSubject = "all inputs", Output = OutputTarget.File("Merged file.", required: true) },
            [Mode(), files],
            (_, standard) => Result(standard.RequestedOutputPath() + "|" + standard.InputPassword));

        Assert.Equal(["files"], command.Arguments.Select(static argument => argument.Name));
        Assert.Equal(["--mode", "--out", "--overwrite", "--password", "--password-env", "--password-stdin"],
            command.Options.Select(static option => option.Name));
        Assert.NotEmpty(command.Parse(["report.test", "other.test"]).Errors);
        Assert.Equal(_temp.File("merged.test") + "|a", Run(command, "report.test", "other.test", "--out", "merged.test", "--password-env", "LEFT"));
        CliException error = RunFailing(command, "report.test", "other.test", "--out", "OTHER.test");
        Assert.Equal(ErrorCodes.OptionInvalid, error.Code);
        Assert.Equal("--out", error.Details!["option"]!.GetValue<string>());
        Assert.Throws<ArgumentException>(() => Create(
            new CommandTraits { Input = Report, PasswordSubject = "all inputs" }, (_, _) => Result()));
    }

    [Fact]
    public void FileOrDirectory_LeavesTheModeToTheCommand()
    {
        Command command = Create(
            new CommandTraits
            {
                Input = Report,
                Output = OutputTarget.FileOrDirectory("Form file.", "Parts."),
            },
            (_, standard) => Result(standard.RequestedOutputDirectory is { } directory
                ? directory
                : standard.OutputPath(".form") + "|" + standard.Overwrite));

        Assert.Equal(["--mode", "--out-dir", "--out", "--overwrite", "--password", "--password-env", "--password-stdin"],
            command.Options.Select(static option => option.Name));
        Assert.Equal(_temp.File("parts"), Run(command, "report.test", "--out-dir", "parts"));
        Assert.Equal(_temp.File("report.form") + "|False", Run(command, "report.test"));
        Assert.Equal(_temp.File("data.xfdf") + "|True", Run(command, "report.test", "--out", "data.xfdf", "--overwrite"));
    }

    [Fact]
    public void RenderFormat_TakesAnExplicitToThenTheOutputExtensionThenTheDefault()
    {
        var to = new Option<string>("--to") { DefaultValueFactory = _ => "png" }.WithInput(InputKind.None);
        FormatDescriptor[] formats =
        [
            FormatDescriptor.Declare("png", FormatUse.Render, null, null, 0, false, ".png"),
            FormatDescriptor.Declare("svg", FormatUse.Render, null, null, 1, false, ".svg"),
        ];
        Command command = StandardCommand.Create(
            _host,
            "render",
            "Renders.",
            new CommandTraits { Input = Report, Output = OutputTarget.File("Output path.") },
            [to],
            (_, standard) => Result(standard.RenderFormat(to, formats)));

        Assert.Equal("png", Run(command, "report.test"));
        Assert.Equal("svg", Run(command, "report.test", "--out", "page.SVG"));
        Assert.Equal("png", Run(command, "report.test", "--out", "page.dat"));
        Assert.Equal("png", Run(command, "report.test", "--to", "png", "--out", "page.svg"));
    }

    [Theory]
    [InlineData("--encrypt", "secret")]
    [InlineData("--encrypt-env", "MISSING")]
    public void EncryptPassword_RefusesAnUnprotectableFormatBeforeReadingTheSecret(string option, string value)
    {
        Command command = Create(
            new CommandTraits { Input = Report, Output = OutputTarget.File("Output path."), Encrypt = Encrypted },
            (parse, standard) => Result(standard.EncryptPassword(parse.GetRequiredValue(ModeOption(parse)))));

        CliException refused = RunFailing(command, "report.test", "--mode", "plain", option, value);

        Assert.Equal(ErrorCodes.OptionInvalid, refused.Code);
        Assert.Equal(option, refused.Details!["option"]!.GetValue<string>());
        Assert.Contains("'plain' format cannot be password-protected", refused.Message, StringComparison.Ordinal);
        Assert.Equal("secret", Run(command, "report.test", "--mode", "secure", "--encrypt", "secret"));
        Assert.Equal("b", Run(command, "report.test", "--mode", "secure", "--encrypt-env", "RIGHT"));
        Assert.Null(Run(command, "report.test", "--mode", "plain"));
    }

    [Fact]
    public void InputPassword_RefusesStandardInputThatCarriesTheCommandData()
    {
        Command command = StandardCommand.Create(
            _host,
            "run",
            "Runs.",
            new CommandTraits { Input = Report },
            [],
            standardInputTaken: static _ => true,
            (_, standard) => Result(standard.InputPassword));

        CliException error = RunFailing(command, "report.test", "--password-stdin");

        Assert.Equal(ErrorCodes.OptionInvalid, error.Code);
        Assert.Equal("--password-stdin", error.Details!["option"]!.GetValue<string>());
        Assert.Equal("a", Run(command, "report.test", "--password-env", "LEFT"));
    }

    [Fact]
    public void Port_ResolvesTheInputAndAppliesTheFontsUntilTheInvocationEnds()
    {
        string fonts = Directory.CreateDirectory(_temp.File("fonts")).FullName;
        Command command = Create(
            new CommandTraits { Input = Report, UsesFonts = true },
            (_, standard) => Result(standard.Port.ActiveDirectories()));

        Assert.Equal(fonts, Run(command, "report.test", "--font-dir", "fonts"));
        Assert.Equal(1, _host.Engine.ScopesEntered);
        Assert.Equal(0, _host.Engine.ScopesOpen);

        CliException missing = RunFailing(command, "missing.test", "--font-dir", "fonts");
        Assert.Equal(ErrorCodes.FileNotFound, missing.Code);
        Assert.Equal(1, _host.Engine.ScopesEntered);
    }

    [Fact]
    public void MaxCharactersOption_SharesTheDefaultAndTheRange()
    {
        var option = new MaxCharactersOption("Characters.");
        var command = new Command("read");
        foreach (Option symbol in option.Options)
        {
            command.Options.Add(symbol);
        }

        Assert.Equal(MaxCharactersOption.DefaultCharacters, option.Read(command.Parse([])));
        Assert.Equal(ErrorCodes.OptionInvalid, Assert.Throws<CliException>(
            () => option.Read(command.Parse([MaxCharactersOption.Name, "0"]))).Code);
    }

    private Command Create(CommandTraits traits, Func<ParseResult, StandardInvocation<ITestPort>, ResultEnvelope> handler) =>
        StandardCommand.Create(_host, "run", "Runs.", traits, [Mode()], handler);

    private static Option<string> Mode() =>
        new Option<string>("--mode") { DefaultValueFactory = _ => "plain" }.WithInput(InputKind.None);

    private static Option<string> ModeOption(ParseResult parse) =>
        (Option<string>)parse.CommandResult.Command.Options.Single(static option => option.Name == "--mode");

    private string? Run(Command command, params string[] arguments)
    {
        _host.Error = null;
        ParseResult parse = command.Parse(arguments);
        Assert.Empty(parse.Errors);
        Assert.Equal(0, parse.Invoke());
        return _host.Error is { } error ? throw error : _host.Result?.Value;
    }

    private CliException RunFailing(Command command, params string[] arguments)
    {
        _host.Error = null;
        ParseResult parse = command.Parse(arguments);
        Assert.Empty(parse.Errors);
        parse.Invoke();
        return Assert.IsType<CliException>(_host.Error);
    }

    private static TestResult Result(string? value = null) => new(value);

    public interface ITestPort
    {
        string ActiveDirectories();
    }

    private sealed record TestResult(string? Value) : ResultEnvelope("test/result", 1);

    private sealed class TestHost(string workDirectory) : IProductCommandHost<ITestPort>
    {
        public TestEngine Engine { get; } = new();

        public TestResult? Result { get; private set; }

        public Exception? Error { get; set; }

        public int Run(ParseResult parseResult, Func<ProductCommandContext<ITestPort>, ResultEnvelope> handler)
        {
            TestEngine engine = Engine;
            var context = new ProductCommandContext<ITestPort>
            {
                Binding = ProductBinding.CreateLicenseFree<ITestPort, TestEngine>("test", _ => engine),
                Paths = new PathResolver(workDirectory),
                Inputs = TestBudgets.Create().Inputs,
                ReadEnvironment = static name => name switch { "LEFT" => "a", "RIGHT" => "b", _ => null },
            };
            try
            {
                Result = (TestResult)handler(context);
            }
            catch (Exception exception)
            {
                Result = null;
                Error = exception;
            }

            return 0;
        }
    }

    private sealed class TestEngine : ITestPort, IFontEnvironment
    {
        private FontSearchProfile? _active;

        public int ScopesEntered { get; private set; }

        public int ScopesOpen { get; private set; }

        public string ActiveDirectories() => string.Join(';', _active?.Directories ?? []);

        public FontListResult ListFonts() => throw new NotSupportedException();

        public FontCheckResult CheckFonts(string filePath, FontCheckRequest request) => throw new NotSupportedException();

        public IDisposable UseFonts(FontSearchProfile profile)
        {
            _active = profile;
            ScopesEntered++;
            ScopesOpen++;
            return new Scope(this);
        }

        private sealed class Scope(TestEngine engine) : IDisposable
        {
            public void Dispose()
            {
                engine._active = null;
                engine.ScopesOpen--;
            }
        }
    }
}
