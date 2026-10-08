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
    private static readonly EncryptedOutput Encrypted = new("the output report");

    // The one format the commands below write without a --to.
    private static readonly FormatDescriptor[] Out = [FormatDescriptor.Declare("out", FormatUse.Convert, null, 0, null, false, ".out", ".test")];

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
                Output = OutputTarget.File("Output path.", Out),
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
                passwords = (standard.InputPassword?.Reveal(), standard.OtherPassword?.Reveal());
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
            (_, standard) => Result(standard.DirectoryOutput.Path + "|" + standard.DirectoryOutput.Overwrite));
        Command created = Create(
            new CommandTraits { Output = OutputTarget.CreatedFile("File to create.", Out) },
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
            new CommandTraits { Input = Report, Output = OutputTarget.CreatedFile("New.", Out) }, (_, _) => Result()));
        Assert.Throws<ArgumentException>(() => Create(
            new CommandTraits { Input = Report, Other = Report }, (_, _) => Result()));
        Assert.Throws<ArgumentException>(() => Create(
            new CommandTraits { Input = Report, Output = OutputTarget.FileOrDirectory("Form file.", "Parts.") }, (_, _) => Result()));
    }

    [Fact]
    public void Output_DerivesASiblingAndNeverNamesAnInput()
    {
        Command command = Create(
            new CommandTraits { Input = Report, Output = OutputTarget.File("Output path.", Out) },
            (_, standard) => Result(standard.Output.Path));

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
            new CommandTraits { Output = OutputTarget.CreatedFile("File to create.", Out) },
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
            new CommandTraits { PasswordSubject = "all inputs", Output = OutputTarget.File("Merged file.", Out, required: true) },
            [Mode(), files],
            (_, standard) => Result(standard.RequestedOutputPath() + "|" + standard.InputPassword?.Reveal()));

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
                Target = TargetFormat.Among("Form format.", [FormatDescriptor.Declare("form", FormatUse.Convert, null, 0, null, false, ".form", ".xfdf")]),
            },
            (_, standard) => Result(standard.RequestedOutputDirectory is { } directory
                ? directory
                : standard.Output.Path + "|" + standard.Output.Overwrite));

        Assert.Equal(["--to", "--mode", "--out-dir", "--out", "--overwrite", "--password", "--password-env", "--password-stdin"],
            command.Options.Select(static option => option.Name));
        Assert.Equal(_temp.File("parts"), Run(command, "report.test", "--out-dir", "parts"));
        Assert.Equal(_temp.File("report.form") + "|False", Run(command, "report.test"));
        Assert.Equal(_temp.File("data.xfdf") + "|True", Run(command, "report.test", "--out", "data.xfdf", "--overwrite"));
    }

    [Fact]
    public void Output_RefusesADerivedSiblingThatAProductOptionReads()
    {
        var template = new Option<string?>("--template").WithInput(InputKind.File);
        File.WriteAllText(_temp.File("report.out"), "template");
        Command command = StandardCommand.Create(
            _host,
            "convert",
            "Converts.",
            new CommandTraits { Input = Report, Output = OutputTarget.File("Output path.", Out) },
            [template],
            (_, standard) => Result(standard.Output.Path));

        CliException error = RunFailing(command, "report.test", "--template", "report.out");

        Assert.Equal(ErrorCodes.OptionInvalid, error.Code);
        Assert.Equal("--out", error.Details!["option"]!.GetValue<string>());
        Assert.Equal(_temp.File("copy.out"), Run(command, "report.test", "--template", "report.out", "--out", "copy.out"));
    }

    [Fact]
    public void Output_RendersTheExplicitToThenTheOutputExtensionThenTheDefault()
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
        Command command = Create(traits, (_, standard) => Result(standard.Output.Format.Id + "|" + Path.GetFileName(standard.Output.Path)));

        Assert.Equal(["--to", "--mode", "--out", "--overwrite", "--password", "--password-env", "--password-stdin"],
            command.Options.Select(static option => option.Name));
        Assert.Equal("png|report.png", Run(command, "report.test"));
        Assert.Equal("svg|page.SVG", Run(command, "report.test", "--out", "page.SVG"));
        Assert.Equal("svg|page.svg", Run(command, "report.test", "--to", "SVG", "--out", "page.svg"));
        Assert.Equal("jpeg|page.jpeg", Run(command, "report.test", "--to", "JPG", "--out", "page.jpeg"));
        Assert.Equal("jpeg|page.jpg", Run(command, "report.test", "--out", "page.jpg"));
        Assert.NotEmpty(command.Parse(["report.test", "--to", "doc"]).Errors);
        foreach (string[] arguments in new[] { new[] { "report.test", "--out", "page.doc" }, new[] { "report.test", "--out", "page" } })
        {
            CliException foreign = RunFailing(command, arguments);
            Assert.Equal(ErrorCodes.UsageError, foreign.Code);
            Assert.Equal("--out", foreign.Details!["option"]!.GetValue<string>());
            Assert.EndsWith("; use .png, .jpg, .jpeg, .svg.", foreign.Message, StringComparison.Ordinal);
        }

        CliException conflict = RunFailing(command, "report.test", "--to", "png", "--out", "page.svg");
        Assert.Equal(ErrorCodes.UsageError, conflict.Code);
        Assert.Equal("--to png contradicts --out 'page.svg': its .svg extension is not a png extension; use .png.", conflict.Message);
        Assert.Equal("Give --out the .png extension, as in page.png, or pass --to svg to write svg.", conflict.Hint);

        // An output without an extension often names a folder: the hint names both corrected paths.
        CliException folder = RunFailing(command, "report.test", "--to", "jpeg", "--out", Path.Combine("deliver", "png"));
        Assert.Equal(
            $"Give --out the .jpg or .jpeg extension, as in {Path.Combine("deliver", "png.jpg")}, or name a file inside the "
                + $"{Path.Combine("deliver", "png")} folder, as in {Path.Combine("deliver", "png", "page.jpg")}; "
                + "an output of several parts is written beside that file as numbered files.",
            folder.Hint);
        Assert.Throws<ArgumentException>(() => Create(traits with { Output = null }, (_, _) => Result()));
        Assert.Throws<ArgumentException>(() => Create(
            traits with { Target = TargetFormat.Render("Image format.", formats, "doc") }, (_, _) => Result()));
    }

    [Fact]
    public void Output_ConvertsToARequiredFormatNamedByItsIdOrAliasUnderOneOfItsExtensions()
    {
        FormatDescriptor[] formats =
        [
            FormatDescriptor.Declare("docx", FormatUse.Convert, null, 0, null, false, ".docx"),
            FormatDescriptor.Declare("md", FormatUse.Convert, null, 1, null, false, ".md", ".markdown") with { Aliases = ["markdown"] },
            FormatDescriptor.Declare("csv", FormatUse.Convert, null, 2, null, false, ".csv", ".txt"),
            FormatDescriptor.Declare("png", FormatUse.Render, null, null, 0, false, ".png"),
        ];
        Command command = Create(
            new CommandTraits
            {
                Input = Report,
                Output = OutputTarget.File("Output path."),
                Target = TargetFormat.Convert("Target format.", formats),
            },
            (_, standard) => Result(standard.Output.Format.Id + "|" + Path.GetFileName(standard.Output.Path)));

        Assert.Equal("md|page.MD", Run(command, "report.test", "--to", "Markdown", "--out", "page.MD"));
        Assert.Equal("docx|report.docx", Run(command, "report.test", "--to", "DOCX"));
        Assert.Equal("md|page.markdown", Run(command, "report.test", "--to", "md", "--out", "page.markdown"));
        Assert.Equal("csv|rows.txt", Run(command, "report.test", "--to", "csv", "--out", "rows.txt"));
        Assert.NotEmpty(command.Parse(["report.test"]).Errors);
        Assert.NotEmpty(command.Parse(["report.test", "--to", "png"]).Errors);
        foreach (string output in new[] { "page.docx", "page.png", "page.mdown", "page" })
        {
            CliException conflict = RunFailing(command, "report.test", "--to", "md", "--out", output);
            Assert.Equal(ErrorCodes.UsageError, conflict.Code);
            Assert.StartsWith($"--to md contradicts --out '{output}': ", conflict.Message, StringComparison.Ordinal);
            Assert.EndsWith("; use .md, .markdown.", conflict.Message, StringComparison.Ordinal);
            Assert.Equal("md", conflict.Details!["format"]!.GetValue<string>());
        }
    }

    [Fact]
    public void Output_OfACreatedFileIsTheFormatItsExtensionNames()
    {
        FormatDescriptor[] writes =
        [
            FormatDescriptor.Declare("tst", FormatUse.Input | FormatUse.Convert, 0, 0, null, false, ".tst", ".test") with { Protectable = true },
            FormatDescriptor.Declare("tsx", FormatUse.Input | FormatUse.Convert, 1, 1, null, false, ".tsx"),
        ];
        FormatDescriptor pdf = FormatDescriptor.Declare("pdf", FormatUse.Convert, null, 2, null, false, ".pdf");
        Command create = StandardCommand.Create(
            _host, "create", "Creates.",
            new CommandTraits { Output = OutputTarget.CreatedFile("File to create.", writes), Encrypt = new EncryptedOutput("the file") },
            [],
            (_, standard) => Result(standard.Output.Format.Id + "|" + standard.EncryptPassword()?.Reveal()));
        Command convert = StandardCommand.Create(
            _host, "convert", "Converts.",
            new CommandTraits { Input = Report, Output = OutputTarget.File("Output path."), Target = TargetFormat.Convert("Target format.", [.. writes, pdf]) },
            [], (_, _) => Result());
        var root = new RootCommand { new Command("test") { create, convert } };
        CliException Refused(string file)
        {
            _host.Error = null;
            root.Parse(["test", "create", file]).Invoke();
            return Assert.IsType<CliException>(_host.Error);
        }

        Assert.Equal("tsx|", Run(create, "new.TSX"));
        Assert.Equal("tst|secret", Run(create, "new.test", "--encrypt", "secret"));
        CliException unprotectable = RunFailing(create, "new.tsx", "--encrypt-env", "MISSING");
        Assert.Equal(ErrorCodes.OptionInvalid, unprotectable.Code);
        Assert.Equal("--encrypt-env", unprotectable.Details!["option"]!.GetValue<string>());
        Assert.Contains("Protect only tst outputs", unprotectable.Hint, StringComparison.Ordinal);

        CliException producer = Refused("new.pdf");
        Assert.Equal(ErrorCodes.UsageError, producer.Code);
        Assert.Equal("file 'new.pdf' asks for pdf, which aspose-cli test create does not write; use .tst, .test, .tsx.", producer.Message);
        Assert.Equal("Give file one of these extensions, then run 'aspose-cli test convert <that file> --to pdf' for pdf.", producer.Hint);
        Assert.Equal("file", producer.Details!["option"]!.GetValue<string>());
        foreach (string file in new[] { "new.foo", "new" })
        {
            CliException refused = Refused(file);
            Assert.Equal(ErrorCodes.UsageError, refused.Code);
            Assert.Equal("Give file one of these extensions.", refused.Hint);
        }
    }

    [Fact]
    public void Output_OfAnEditKeepsTheInputFormatUnlessItsOutputNamesAnother()
    {
        FormatDescriptor[] writes =
        [
            FormatDescriptor.Declare("tst", FormatUse.Input | FormatUse.Convert, 0, 0, null, false, ".test"),
            FormatDescriptor.Declare("old", FormatUse.Input | FormatUse.Convert, 1, 1, null, false, ".test"),
            FormatDescriptor.Declare("tsx", FormatUse.Input | FormatUse.Convert, 2, 2, null, false, ".tsx"),
        ];
        Command edit = StandardCommand.Create(
            _host, "edit", "Edits.",
            new CommandTraits { Input = Report, Output = OutputTarget.Mutation(writes) },
            [],
            (_, standard) => Result(string.Join('|',
                standard.Output.Format.Id, standard.Output.Keeping("old").Id, standard.Output.InPlace, Path.GetFileName(standard.Output.Path))));
        File.WriteAllText(_temp.File("report.foo"), "foo");

        Assert.Equal("tst|old|False|report.out.test", Run(edit, "report.test"));
        Assert.Equal("tst|old|True|report.test", Run(edit, "report.test", "--in-place"));
        Assert.Equal("tsx|tsx|False|copy.tsx", Run(edit, "report.test", "--out", "copy.tsx"));
        CliException input = RunFailing(edit, "report.foo");
        Assert.Equal(ErrorCodes.UsageError, input.Code);
        Assert.Equal("--out", input.Details!["option"]!.GetValue<string>());
        Assert.StartsWith("The input 'report.foo', whose name the output keeps without --out, has the .foo extension", input.Message, StringComparison.Ordinal);
        Assert.Equal("--out", RunFailing(edit, "report.test", "--out", "copy.foo").Details!["option"]!.GetValue<string>());
    }

    [Fact]
    public void Output_DerivesTheOnlyFormatWithTheCommandsMarker()
    {
        FormatDescriptor pdf = FormatDescriptor.Declare("pdf", FormatUse.Convert, null, 0, null, false, ".pdf");
        Command sign = Create(
            new CommandTraits { Input = Report, Output = OutputTarget.File("Signed file.", [pdf], derivedMarker: ".signed") },
            (_, standard) => Result(Path.GetFileName(standard.Output.Path)));

        Assert.Equal("report.signed.pdf", Run(sign, "report.test"));
        Assert.Equal("copy.pdf", Run(sign, "report.test", "--out", "copy.pdf"));
        Assert.Equal(ErrorCodes.UsageError, RunFailing(sign, "report.test", "--out", "copy.png").Code);
    }

    [Fact]
    public void ResolvedOutput_NamesItsPartsBesideIt()
    {
        var output = new ResolvedOutput(FormatDescriptor.Declare("png", FormatUse.Render, null, null, 0, false, ".png"), _temp.File("deck.png"));

        Assert.Equal(_temp.File("deck.png"), output.Part("s", 1, 1));
        Assert.Equal(_temp.File("deck.s3.png"), output.Part("s", 3, 4));
        Assert.Equal(_temp.File("deck.Summary.png"), output.Part("Summary"));
    }

    [Theory]
    [InlineData("--encrypt", "secret")]
    [InlineData("--encrypt-env", "MISSING")]
    public void EncryptPassword_RefusesAnUnprotectableFormatBeforeReadingTheSecret(string option, string value)
    {
        FormatDescriptor[] writes =
        [
            FormatDescriptor.Declare("secure", FormatUse.Convert, null, 0, null, false, ".secure") with { Protectable = true },
            FormatDescriptor.Declare("plain", FormatUse.Convert, null, 1, null, false, ".plain"),
        ];
        Command command = Create(
            new CommandTraits { Input = Report, Output = OutputTarget.File("Output path.", writes), Encrypt = Encrypted },
            (_, standard) => Result(standard.EncryptPassword()?.Reveal()));

        CliException refused = RunFailing(command, "report.test", "--out", "out.plain", option, value);

        Assert.Equal(ErrorCodes.OptionInvalid, refused.Code);
        Assert.Equal(option, refused.Details!["option"]!.GetValue<string>());
        Assert.Contains("'plain' format cannot be password-protected", refused.Message, StringComparison.Ordinal);
        Assert.Equal("secret", Run(command, "report.test", "--out", "out.secure", "--encrypt", "secret"));
        Assert.Equal("b", Run(command, "report.test", "--out", "out.secure", "--encrypt-env", "RIGHT"));
        Assert.Null(Run(command, "report.test", "--out", "out.plain"));
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
            (_, standard) => Result(standard.InputPassword?.Reveal()));

        CliException error = RunFailing(command, "report.test", "--password-stdin");

        Assert.Equal(ErrorCodes.OptionInvalid, error.Code);
        Assert.Equal("--password-stdin", error.Details!["option"]!.GetValue<string>());
        Assert.Equal("a", Run(command, "report.test", "--password-env", "LEFT"));
    }

    [Fact]
    public void RequiredEnvironmentPassword_IsNamedOnlyByItsVariable()
    {
        PasswordOptions key = PasswordOptions.RequiredEnvironment("--key-password", "the signing key");
        Command command = StandardCommand.Create(
            _host, "sign", "Signs.", new CommandTraits { Input = Report }, [.. key.Options],
            (_, standard) => Result(standard.Password(key).Reveal()));

        Option environment = Assert.Single(
            command.Options, static option => option.Name.StartsWith("--key-password", StringComparison.Ordinal));
        Assert.Equal("--key-password-env", environment.Name);
        Assert.True(environment.Required);
        Assert.Equal("a", Run(command, "report.test", "--key-password-env", "LEFT"));
        CliException missing = RunFailing(command, "report.test", "--key-password-env", "MISSING");
        Assert.Equal(ErrorCodes.OptionInvalid, missing.Code);
        Assert.Equal("--key-password-env", missing.Details!["option"]!.GetValue<string>());
        Assert.DoesNotContain("another source", missing.Hint, StringComparison.Ordinal);
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
            new CommandTraits { Output = OutputTarget.CreatedFile("File to create.", Out) },
            [template, images, title],
            (_, standard) => Result(standard.InputFile(template) + "|" + string.Join(';', standard.InputFiles(images))));
        Command undeclared = StandardCommand.Create(
            _host, "create", "Creates.", new CommandTraits { Output = OutputTarget.CreatedFile("File to create.", Out) }, [title],
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
        Assert.Equal("a", invocation.InputPassword?.Reveal());
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
