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

    private static CommandTraits Paired => new()
    {
        Input = new InputDocument("Baseline.", "the baseline", "left"),
        Other = new InputDocument("Candidate.", "the candidate", "right"),
    };

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
            Paired,
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

    /// <summary>
    /// The loader that finds a document encrypted cannot know which of two inputs it was given;
    /// the command restates the error with that input's own password option.
    /// </summary>
    [Fact]
    public void APasswordErrorOfOneOfTwoDocuments_NamesThatDocumentsOption()
    {
        Command command = Create(
            Paired,
            (_, standard) => throw CliErrors.PasswordRequired(standard.Other));
        Command invalidLeft = Create(
            Paired,
            (_, standard) => throw CliErrors.PasswordInvalid(standard.Input));

        CliException right = RunFailing(command, "report.test", "other.test");
        CliException left = RunFailing(invalidLeft, "report.test", "other.test");

        Assert.Equal(ErrorCodes.PasswordRequired, right.Code);
        Assert.Equal(("right", _temp.File("other.test")),
            (right.Details!["input"]!.GetValue<string>(), right.Details["path"]!.GetValue<string>()));
        Assert.Contains("--right-password-env", right.Hint, StringComparison.Ordinal);
        Assert.Equal((ErrorCodes.PasswordInvalid, "left"), (left.Code, left.Details!["input"]!.GetValue<string>()));
        Assert.Contains("--left-password-env", left.Hint, StringComparison.Ordinal);
    }

    /// <summary>A password error about neither input keeps the hint that names options per input.</summary>
    [Fact]
    public void APasswordErrorOfAThirdDocument_PointsToThePerInputOptions()
    {
        Command command = Create(
            Paired,
            (_, _) => throw CliErrors.PasswordRequired(_temp.File("third.test")));

        CliException error = RunFailing(command, "report.test", "other.test");

        Assert.Equal(ErrorCodes.PasswordRequired, error.Code);
        Assert.Null(error.Details!["input"]);
        Assert.Contains("--left-password-env", error.Hint, StringComparison.Ordinal);
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
    public void OutputPath_RefusesADerivedSiblingThatAProductOptionReads()
    {
        var template = new Option<string?>("--template").WithInput(InputKind.File);
        File.WriteAllText(_temp.File("report.out"), "template");
        Command command = StandardCommand.Create(
            _host,
            "convert",
            "Converts.",
            new CommandTraits { Input = Report, Output = OutputTarget.File("Output path.") },
            [template],
            (_, standard) => Result(standard.OutputPath(".out")));

        CliException error = RunFailing(command, "report.test", "--template", "report.out");

        Assert.Equal(ErrorCodes.OptionInvalid, error.Code);
        Assert.Equal("--out", error.Details!["option"]!.GetValue<string>());
        Assert.Equal(_temp.File("copy.out"), Run(command, "report.test", "--template", "report.out", "--out", "copy.out"));
    }

    [Fact]
    public void TargetFormat_RendersTheExplicitToThenTheOutputExtensionThenTheDefault()
    {
        FormatDescriptor[] formats =
        [
            FormatDescriptor.Declare("png", FormatUse.Render, null, null, 0, false, ".png"),
            FormatDescriptor.Declare("jpeg", FormatUse.Render, null, null, 1, false, ".jpg", ".jpeg") with { Aliases = ["jpg"] },
            FormatDescriptor.Declare("svg", FormatUse.Render, null, null, 2, false, ".svg"),
            FormatDescriptor.Declare("doc", FormatUse.Convert, null, 0, null, false, ".doc"),
        ];
        var traits = new CommandTraits
        {
            Input = Report,
            Output = OutputTarget.File("Output path."),
            Target = TargetFormat.Render("Image format.", formats),
        };
        Command command = Create(traits, (_, standard) => Result(standard.TargetFormat()));

        Assert.Equal(["--to", "--mode", "--out", "--overwrite", "--password", "--password-env", "--password-stdin"],
            command.Options.Select(static option => option.Name));
        Assert.Equal("png", Run(command, "report.test"));
        Assert.Equal("svg", Run(command, "report.test", "--out", "page.SVG"));
        Assert.Equal("png", Run(command, "report.test", "--out", "page"));
        Assert.Equal("svg", Run(command, "report.test", "--to", "SVG", "--out", "page.svg"));
        Assert.Equal("jpeg", Run(command, "report.test", "--to", "JPG", "--out", "page.jpeg"));
        Assert.NotEmpty(command.Parse(["report.test", "--to", "doc"]).Errors);
        foreach (string[] arguments in new[] { new[] { "report.test", "--out", "page.doc" }, new[] { "report.test", "--to", "png", "--out", "page.doc" } })
        {
            CliException foreign = RunFailing(command, arguments);
            Assert.Equal(ErrorCodes.UsageError, foreign.Code);
            Assert.Contains("--out 'page.doc' has the .doc extension, which names no render format; use .png, .jpg, .jpeg, .svg", foreign.Message, StringComparison.Ordinal);
        }

        CliException conflict = RunFailing(command, "report.test", "--to", "png", "--out", "page.svg");
        Assert.Equal(ErrorCodes.UsageError, conflict.Code);
        Assert.Contains("--to png", conflict.Message, StringComparison.Ordinal);
        Assert.Contains("--out 'page.svg'", conflict.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => Create(traits with { Output = null }, (_, _) => Result()));
        Assert.Throws<ArgumentException>(() => Create(
            traits with { Target = TargetFormat.Render("Image format.", formats, "doc") }, (_, _) => Result()));
    }

    [Fact]
    public void TargetFormat_ConvertsToARequiredFormatNamedByItsIdOrAlias()
    {
        FormatDescriptor[] formats =
        [
            FormatDescriptor.Declare("docx", FormatUse.Convert, null, 0, null, false, ".docx"),
            FormatDescriptor.Declare("md", FormatUse.Convert, null, 1, null, false, ".md") with { Aliases = ["markdown"] },
            FormatDescriptor.Declare("png", FormatUse.Render, null, null, 0, false, ".png"),
        ];
        Command command = Create(
            new CommandTraits
            {
                Input = Report,
                Output = OutputTarget.File("Output path."),
                Target = TargetFormat.Convert("Target format.", formats),
            },
            (_, standard) => Result(standard.TargetFormat()));

        Assert.Equal("md", Run(command, "report.test", "--to", "Markdown", "--out", "page.MD"));
        Assert.Equal("md", Run(command, "report.test", "--to", "md", "--out", "page.markdown"));
        Assert.Equal("docx", Run(command, "report.test", "--to", "DOCX"));
        Assert.NotEmpty(command.Parse(["report.test"]).Errors);
        Assert.NotEmpty(command.Parse(["report.test", "--to", "png"]).Errors);
        foreach (string extension in new[] { "docx", "png" })
        {
            CliException conflict = RunFailing(command, "report.test", "--to", "md", "--out", $"page.{extension}");
            Assert.Equal(ErrorCodes.UsageError, conflict.Code);
            Assert.Contains($"--to md contradicts --out 'page.{extension}'", conflict.Message, StringComparison.Ordinal);
            Assert.Contains("the .md extension", conflict.Message, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// A command that writes the format its output's extension names, and cannot write that one,
    /// says so for the parameter that named it and points to the product command that can.
    /// </summary>
    [Theory]
    [InlineData("new.pdf", "file 'new.pdf' asks for pdf, which aspose-cli test create does not write; it writes tst, tsx.",
        "Give file the .tst or .tsx extension, then run 'aspose-cli test convert <that file> --to pdf' for pdf.")]
    [InlineData("new.png", "file 'new.png' asks for png, which aspose-cli test create does not write; it writes tst, tsx.",
        "Give file the .tst or .tsx extension.")]
    [InlineData("new.foo", "Unsupported format 'foo'. Supported formats: tst, tsx", null)]
    // The command writes tst, so an error about tst concerns another file, such as one an operation reads.
    [InlineData("new.tst", "Unsupported format 'tst'. Supported formats: tst, tsx", null)]
    public void UnsupportedOutputFormat_NamesTheParameterAndTheCommandThatWritesIt(string file, string message, string? hint)
    {
        FormatDescriptor[] formats =
        [
            FormatDescriptor.Declare("tst", FormatUse.Input | FormatUse.Convert, 0, 0, null, false, ".tst"),
            FormatDescriptor.Declare("tsx", FormatUse.Input, 1, null, null, false, ".tsx"),
            FormatDescriptor.Declare("pdf", FormatUse.Convert, null, 1, null, false, ".pdf"),
            FormatDescriptor.Declare("png", FormatUse.Render, null, null, 0, false, ".png"),
        ];
        Command create = StandardCommand.Create(
            _host, "create", "Creates.", new CommandTraits { Output = OutputTarget.CreatedFile("File to create.") }, [],
            (_, standard) => throw CliErrors.FormatUnsupported(Path.GetExtension(standard.CreatedPath)[1..], ["tst", "tsx"]));
        Command convert = StandardCommand.Create(
            _host, "convert", "Converts.",
            new CommandTraits { Input = Report, Output = OutputTarget.File("Output path."), Target = TargetFormat.Convert("Target format.", formats) },
            [], (_, _) => Result());
        var root = new RootCommand { new Command("test") { create, convert } };

        _host.Error = null;
        root.Parse(["test", "create", file]).Invoke();
        CliException error = Assert.IsType<CliException>(_host.Error);

        Assert.Equal(ErrorCodes.FormatUnsupported, error.Code);
        Assert.Equal(message, error.Message);
        if (hint is not null)
        {
            Assert.Equal(hint, error.Hint);
            Assert.Equal("file", error.Details!["option"]!.GetValue<string>());
        }
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
    public void Continuation_RepeatsThePathInputAndPasswordVariableButNeverThePassword()
    {
        Command command = StandardCommand.Create(
            _host,
            "run",
            "Runs.",
            new CommandTraits { Input = Report },
            [],
            (_, standard) => Result(standard.Continuation().ToString()));
        var root = new RootCommand { command };

        string fromVariable = Run(root, "run", "report.test", "--password-env", "LEFT")!;
        string literal = Run(root, "run", "report.test", "--password", "secret")!;

        Assert.StartsWith("aspose-cli run ", fromVariable, StringComparison.Ordinal);
        Assert.EndsWith("report.test\" --password-env LEFT --output json", fromVariable, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", literal, StringComparison.Ordinal);
        Assert.DoesNotContain("--password", literal, StringComparison.Ordinal);
    }

    [Fact]
    public void OpenEngine_ResolvesTheInputAndAppliesTheFontsUntilTheInvocationEnds()
    {
        string fonts = Directory.CreateDirectory(_temp.File("fonts")).FullName;
        Command command = Create(
            new CommandTraits { Input = Report, UsesFonts = true },
            (_, standard) => Result(standard.OpenEngine().ActiveDirectories()));

        Assert.Equal(fonts, Run(command, "report.test", "--font-dir", "fonts"));
        Assert.Equal(1, _host.Engine.ScopesEntered);
        Assert.Equal(0, _host.Engine.ScopesOpen);

        CliException missing = RunFailing(command, "missing.test", "--font-dir", "fonts");
        Assert.Equal(ErrorCodes.FileNotFound, missing.Code);
        Assert.Equal(1, _host.Engine.ScopesEntered);
    }

    [Fact]
    public void InputFile_ResolvesOnlyTheCommandsOwnExistingInputFiles()
    {
        var template = new Option<string?>("--template").WithInput(InputKind.File);
        var images = new Option<string[]>("--image").WithInput(InputKind.File);
        var title = new Option<string?>("--title").WithInput(InputKind.None);
        Command command = StandardCommand.Create(
            _host,
            "create",
            "Creates.",
            new CommandTraits { Output = OutputTarget.CreatedFile("File to create.") },
            [template, images, title],
            (_, standard) => Result(standard.InputFile(template) + "|" + string.Join(';', standard.InputFiles(images))));
        Command undeclared = StandardCommand.Create(
            _host, "create", "Creates.", new CommandTraits { Output = OutputTarget.CreatedFile("File to create.") }, [title],
            (_, standard) => Result(standard.InputFile(title)));

        Assert.Equal(
            _temp.File("report.test") + "|" + _temp.File("other.test"),
            Run(command, "new.test", "--template", "report.test", "--image", "other.test"));
        Assert.Equal("|", Run(command, "new.test"));
        var certificate = new Option<string>("--certificate") { Required = true }.WithInput(InputKind.File);
        Command required = StandardCommand.Create(
            _host, "sign", "Signs.", new CommandTraits { Input = Report }, [certificate],
            (_, standard) => Result(standard.RequiredInputFile(certificate)));
        Assert.Equal(_temp.File("other.test"), Run(required, "report.test", "--certificate", "other.test"));
        Assert.Equal(ErrorCodes.FileNotFound, RunFailing(command, "new.test", "--template", "missing.test").Code);
        _host.Error = null;
        undeclared.Parse(["new.test", "--title", "x"]).Invoke();
        Assert.IsType<InvalidOperationException>(_host.Error);
    }

    [Fact]
    public void Bind_ReadsTheCommonValuesOfACommandOutsideTheProductPipeline()
    {
        var standard = new StandardOptions(new CommandTraits { Input = Report, UsesFonts = true });
        Command command = standard.CreateCommand("check", "Checks.", [Mode()]);
        string fonts = Directory.CreateDirectory(_temp.File("fonts")).FullName;

        StandardInvocation invocation = standard.Bind(
            command.Parse(["report.test", "--password-env", "LEFT", "--font-dir", "fonts"]),
            new PathResolver(_temp.Path),
            TestBudgets.Create().Inputs,
            static name => name == "LEFT" ? "a" : null);

        Assert.Equal(["--mode", "--password", "--password-env", "--password-stdin", "--font-dir"],
            command.Options.Select(static option => option.Name));
        Assert.Equal(_temp.File("report.test"), invocation.Input);
        Assert.Equal("a", invocation.InputPassword);
        Assert.Equal(fonts, Assert.Single(invocation.FontDirectories.Directories));
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

#pragma warning disable APCLI003 // A test result, not a product JSON root.
    private sealed record TestResult(string? Value) : ResultEnvelope("test/result", 1);
#pragma warning restore APCLI003

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
                Binding = ProductBinding.CreateLicenseFree<ITestPort>("test", _ => engine, _ => engine),
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
