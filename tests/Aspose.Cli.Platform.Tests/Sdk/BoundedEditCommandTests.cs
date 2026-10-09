using System.CommandLine;
using System.Text.Json.Serialization.Metadata;
using Aspose.Cli.Sdk;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;
using Aspose.Cli.Sdk.Extensibility.Output;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Operations;
using Aspose.Cli.Sdk.Serialization;
using Aspose.Cli.Sdk.Tests;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Sdk;

public sealed class BoundedEditCommandTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly string _input;

    public BoundedEditCommandTests()
    {
        _input = _temp.File("book.test");
        File.WriteAllText(_input, "document");
    }

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Read_AppendsCompiledDirectivesToTheDocumentAndPreparesTheWholeBatch()
    {
        BoundedEditInvocation<TestBatch> invocation = Read(
            WithDirectives(),
            "--ops", """{"ops":[{"op":"set","value":1}]}""", "--set", "2", "--set", "3");

        Assert.Equal([1, 2, 3], invocation.Batch.Ops.Cast<SetOp>().Select(static op => op.Value));
        Assert.Equal(["op-0001", "op-0002", "op-0003"], invocation.Batch.Ops.Select(static op => op.Id));
    }

    [Fact]
    public void Read_ComposesADirectiveOnlyBatchThroughTheCatalog()
    {
        CliException invalid = Assert.Throws<CliException>(() => Read(WithDirectives(), "--set", "-1"));

        Assert.Equal(ErrorCodes.OpsInvalid, invalid.Code);
        Assert.Equal(["op-0001"], Read(WithDirectives(), "--set", "4").Batch.Ops.Select(static op => op.Id));
    }

    [Fact]
    public void Read_RejectsABadDirectiveBeforeReadingTheDocument()
    {
        CliException error = Assert.Throws<CliException>(() => Read(
            WithDirectives(), "--ops", "missing-ops.json", "--set", "not-a-number"));

        Assert.Equal(ErrorCodes.OptionInvalid, error.Code);
        Assert.Contains("--set", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_RequiresADocumentOrADirective()
    {
        CliException error = Assert.Throws<CliException>(() => Read(WithDirectives()));

        Assert.Equal(ErrorCodes.UsageError, error.Code);
        Assert.NotEmpty(Parse(Plain()).Errors);
    }

    [Fact]
    public void Read_MakesABackupOnlyWhenRequested()
    {
        ResolvedOutput inPlace = Read(Plain(), "--ops", Document, "--in-place").Output;
        ResolvedOutput backedUp = Read(Plain(), "--ops", Document, "--in-place", "--backup").Output;

        Assert.True(inPlace.InPlace);
        Assert.Null(inPlace.BackupPath);
        Assert.Equal(_temp.File("book.backup.test"), backedUp.BackupPath);
        Assert.Equal(ErrorCodes.OptionInvalid, Assert.Throws<CliException>(
            () => Read(Plain(), "--ops", Document, "--backup")).Code);
    }

    [Fact]
    public void Read_OffersVerifyOnlyWhenTheProductDeclaresItAndRejectsItWithDryRun()
    {
        var verifying = new BoundedEditCommand<TestOp, TestBatch>(Definition() with { VerifyDescription = "Verify." });

        Assert.True(Read(verifying, "--ops", Document, "--verify").Verify);
        Assert.Equal(ErrorCodes.OptionInvalid, Assert.Throws<CliException>(
            () => Read(verifying, "--ops", Document, "--verify", "--dry-run")).Code);
        Assert.NotEmpty(Parse(Plain(), "--ops", Document, "--verify").Errors);
    }

    [Fact]
    public void Read_CombinesExecutionFlagsAndRejectsAConflictingFingerprint()
    {
        EditCommandOptions options = Read(
            Plain(),
            "--ops", """{"ifMatch":"abcdef","ops":[{"op":"set","value":1}]}""",
            "--if-match", "ABCDEF", "--dry-run", "--best-effort").Options;

        Assert.Equal("ABCDEF", options.IfMatch);
        Assert.True(options.DryRun);
        Assert.True(options.BestEffort);
        Assert.Equal(ErrorCodes.OptionInvalid, Assert.Throws<CliException>(() => Read(
            Plain(),
            "--ops", """{"ifMatch":"stale","ops":[{"op":"set","value":1}]}""",
            "--if-match", "current")).Code);
    }

    [Fact]
    public void Read_ExplainsAnInlineDocumentWhoseQuotesTheShellRemoved()
    {
        CliException withDirectives = Assert.Throws<CliException>(
            () => Read(WithDirectives(), "--ops", "{ops:[{op:set,value:1}]}"));
        CliException plain = Assert.Throws<CliException>(
            () => Read(Plain(), "--ops", "{ops:[{op:set,value:1}]}"));

        Assert.Equal(ErrorCodes.OpsInvalid, withDirectives.Code);
        Assert.Contains("Windows PowerShell", withDirectives.Hint, StringComparison.Ordinal);
        Assert.Contains("--set", withDirectives.Hint, StringComparison.Ordinal);
        Assert.DoesNotContain("--set", plain.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_ReplacesTheInputOnlyInPlace()
    {
        CliException error = Assert.Throws<CliException>(() => Read(
            Plain(), "--ops", Document, "--out", Path.Combine("missing", "..", "BOOK.TEST"), "--overwrite"));

        Assert.Equal(ErrorCodes.OptionInvalid, error.Code);
        Assert.Equal("--out", error.Details!["option"]!.GetValue<string>());
        Assert.Contains("--in-place", error.Hint, StringComparison.Ordinal);
        Assert.Equal(_input, Read(Plain(), "--ops", Document, "--in-place").Output.Path);
    }

    [Fact]
    public void OutputFileOption_NeverResolvesToTheInput()
    {
        var output = new OutputFileOption("Output path.", required: false);
        var command = new Command("convert");
        output.AddTo(command);

        CliException error = Assert.Throws<CliException>(() => output.Resolve(
            command.Parse(["--out", "BOOK.TEST"]), new PathResolver(_temp.Path), _input));

        Assert.Equal(ErrorCodes.OptionInvalid, error.Code);
        Assert.DoesNotContain("--in-place", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void OutputFileOption_NeverResolvesToAnyInput()
    {
        var output = new OutputFileOption("Output path.", required: false);
        var command = new Command("merge");
        output.AddTo(command);
        var paths = new PathResolver(_temp.Path);
        string other = _temp.File("other.test");
        File.WriteAllText(other, "other");

        CliException error = Assert.Throws<CliException>(() => output.Resolve(
            command.Parse(["--out", "OTHER.test"]), paths, _input, null, other));
        CliException argument = Assert.Throws<CliException>(() => OutputFileOption.ResolveExplicit(
            paths, "BOOK.TEST", "file", null, _input));

        Assert.Equal(ErrorCodes.OptionInvalid, error.Code);
        Assert.Equal("--out", error.Details!["option"]!.GetValue<string>());
        Assert.Equal("file", argument.Details!["option"]!.GetValue<string>());
        Assert.Null(output.Resolve(command.Parse([]), paths, _input, other));
        Assert.Equal(_temp.File("merged.test"), output.Resolve(
            command.Parse(["--out", "merged.test"]), paths, _input, other));
    }

    [Theory]
    [InlineData("""{"ops":[{"op":"set","value":1},{"op":"set","value":1,"extra":true}]}""", 1, "set", "unknown field 'extra'; set accepts: op, id, value")]
    [InlineData("""{"ops":[{"op":"set","value":"one"}]}""", 0, "set", "value must be a whole number")]
    [InlineData("""{"ops":[{"op":"set","value":1,"value":2}]}""", 0, "set", "value is duplicated")]
    [InlineData("""{"ops":[{"op":"link","path":null}]}""", 0, "link", "path must not be null")]
    public void Read_ExplainsARejectedOperationInWireTerms(string document, int index, string op, string reason)
    {
        CliException error = Assert.Throws<CliException>(() => Read(Plain(), "--ops", document));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Equal(index, error.Details!["index"]!.GetValue<int>());
        Assert.Equal(op, error.Details["op"]!.GetValue<string>());
        Assert.Equal(reason, error.Details["reason"]!.GetValue<string>());
        Assert.DoesNotContain(nameof(BoundedEditCommandTests), error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{"ops":[{"op":"sett","value":1}]}""", "unknown op 'sett'")]
    [InlineData("""{"ops":[5]}""", "every op must be an object")]
    public void Read_NamesOnlyTheIndexOfAnEntryWithoutAKnownOperation(string document, string reason)
    {
        CliException error = Assert.Throws<CliException>(() => Read(Plain(), "--ops", document));

        Assert.Equal(0, error.Details!["index"]!.GetValue<int>());
        Assert.Null(error.Details["op"]);
        Assert.StartsWith(reason, error.Details["reason"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""[{"op":"set","value":1}]""", "the document must be an object")]
    [InlineData("""{"ops":{"op":"set","value":1}}""", "ops must be an array")]
    [InlineData("""{"schemaVersion":2}""", "the required field 'ops' is missing")]
    public void Read_ExplainsARejectedEnvelopeInWireTerms(string document, string reason)
    {
        CliException error = Assert.Throws<CliException>(() => Read(Plain(), "--ops", document));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Equal(reason, error.Details!["reason"]!.GetValue<string>());
        Assert.DoesNotContain(nameof(TestBatch), error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_NormalizesOperationPathsAgainstTheInvocationDirectory()
    {
        File.WriteAllText(_temp.File("image.png"), "image");

        LinkOp link = Assert.IsType<LinkOp>(Assert.Single(
            Read(Plain(), "--ops", """{"ops":[{"op":"link","path":"image.png"}]}""").Batch.Ops));

        Assert.Equal(_temp.File("image.png"), link.Path);
    }

    [Fact]
    public void Read_NeverPublishesToTheOperationDocumentOrAFileAnOperationReads()
    {
        BoundedEditCommand<TestOp, TestBatch> linking = Plain();
        File.WriteAllText(_temp.File("ops.json"), Document);
        File.WriteAllText(_temp.File("image.png"), "image");
        File.WriteAllText(_temp.File("book.out.test"), "earlier output");
        const string image = """{"ops":[{"op":"link","path":"image.png"}]}""";

        CliException[] errors =
        [
            Assert.Throws<CliException>(() => Read(Plain(), "--ops", "ops.json", "--out", "OPS.json", "--overwrite")),
            Assert.Throws<CliException>(() => Read(linking, "--ops", image, "--out", "IMAGE.png", "--overwrite")),
            Assert.Throws<CliException>(() => Read(linking, "--ops", """{"ops":[{"op":"link","path":"book.out.test"}]}""")),
        ];

        Assert.All(errors, static error =>
        {
            Assert.Equal(ErrorCodes.OptionInvalid, error.Code);
            Assert.Equal("--out", error.Details!["option"]!.GetValue<string>());
            Assert.Contains("--in-place", error.Hint, StringComparison.Ordinal);
        });
        Assert.Equal(_input, Read(linking, "--ops", image, "--in-place").Output.Path);
        Assert.Equal(_temp.File("copy.test"), Read(linking, "--ops", image, "--out", "copy.test").Output.Path);
    }

    [Fact]
    public void Read_ResolvesEachOperationSecretOnceByVariableName()
    {
        const string document = """{"ops":[{"op":"secret","passwordEnv":"OWNER"},{"op":"secret","passwordEnv":"OWNER"},{"op":"secret"},{"op":"secret","passwordEnv":"ABSENT"},{"op":"secret","passwordEnv":"ABSENT"}]}""";
        var reads = new List<string>();

        IReadOnlyDictionary<string, Secret> secrets = ReadWithEnvironment(Plain(), name =>
        {
            reads.Add(name);
            return name == "OWNER" ? "owner-secret" : null;
        }, "--ops", document).Secrets;

        Assert.Equal("owner-secret", Assert.Single(secrets).Value.Reveal());
        Assert.Equal(["OWNER", "ABSENT"], reads);
    }

    [Fact]
    public void OperationSecrets_FailOnlyTheOperationThatNamesAMissingVariable()
    {
        var secrets = new Dictionary<string, Secret> { ["OWNER"] = new("owner-secret") };

        OperationInvalidException missing = Assert.Throws<OperationInvalidException>(
            () => OperationSecrets.Resolve(secrets, "ABSENT"));

        Assert.Equal("owner-secret", OperationSecrets.Resolve(secrets, "OWNER")?.Reveal());
        Assert.Null(OperationSecrets.Resolve(secrets, null));
        Assert.Contains("'ABSENT'", missing.Message, StringComparison.Ordinal);
        Assert.Throws<OperationInvalidException>(() => OperationSecrets.Resolve(null, "OWNER"));
    }

    [Fact]
    public void Create_ChecksProductUsageBeforeReadingTheOperationDocument()
    {
        var fast = new Option<bool>("--fast");
        TestCommandRunner runner = Runner();
        Command command = Edit(
            Plain(),
            runner,
            new CommandTraits { Input = Book },
            [fast],
            static (_, _, _) => throw new InvalidOperationException("The handler is never reached."),
            checkUsage: parse =>
            {
                if (parse.GetValue(fast))
                {
                    throw CliErrors.OptionInvalid("--fast", "is refused", "Drop --fast.");
                }
            });

        command.Parse([Path.GetFileName(_input), "--ops", "-", "--fast"]).Invoke();
        CliException refused = Assert.IsType<CliException>(runner.Error);
        command.Parse(["missing.test", "--ops", "-"]).Invoke();
        CliException missing = Assert.IsType<CliException>(runner.Error);

        Assert.Equal("--fast", refused.Details!["option"]!.GetValue<string>());
        Assert.Equal(ErrorCodes.FileNotFound, missing.Code);
    }

    [Fact]
    public void Read_NeverPublishesToAFileAProductOptionReads()
    {
        var source = new Option<string?>("--source").WithInput(InputKind.File);
        TestCommandRunner runner = Runner();
        Command command = Edit(
            Plain(), runner, new CommandTraits { Input = Book }, [source],
            static (_, _, _) => throw new InvalidOperationException("The output is refused first."));
        File.WriteAllText(_temp.File("source.test"), "source");

        command.Parse([Path.GetFileName(_input), "--ops", Document, "--source", "source.test", "--out", "SOURCE.test"]).Invoke();

        CliException error = Assert.IsType<CliException>(runner.Error);
        Assert.Equal(ErrorCodes.OptionInvalid, error.Code);
        Assert.Equal("--out", error.Details!["option"]!.GetValue<string>());
    }

    [Fact]
    public void Create_PutsTheEditOptionsBeforeTheProductAndCommonOptions()
    {
        var edit = new BoundedEditCommand<TestOp, TestBatch>(Definition() with { VerifyDescription = "Verify." });
        var traits = new CommandTraits
        {
            Input = Book,
            Encrypt = new EncryptedOutput("the output book"),
            UsesFonts = true,
        };

        Command command = Edit(
            edit, Runner(), traits, [new Option<bool>("--fast")], static (_, _, _) => throw new InvalidOperationException());

        Assert.Equal(
            [
                "--ops", "--if-match", "--dry-run", "--best-effort", "--verify", "--fast",
                "--out", "--overwrite", "--in-place", "--backup",
                "--password", "--password-env", "--password-stdin", "--encrypt", "--encrypt-env", "--font-dir",
            ],
            command.Options.Select(static option => option.Name));
        Assert.Throws<ArgumentException>(() => Edit(
            edit, Runner(), traits with { Output = OutputTarget.File("Out.") }, [],
            static (_, _, _) => throw new InvalidOperationException()));
    }

    [Fact]
    public void TheTemplateReservesExactlyTheOptionNamesItDeclares()
    {
        var edit = new BoundedEditCommand<TestOp, TestBatch>(Definition() with
        {
            VerifyDescription = "Verify.",
            SetDirectives = new("Set a value.", static _ => new SetOp { Value = 0 }, static ops => new TestBatch { Ops = ops }),
        });
        Command edited = Edit(
            edit, Runner(),
            new CommandTraits { Input = Book, Encrypt = new EncryptedOutput("the output book"), UsesFonts = true },
            [], static (_, _, _) => throw new InvalidOperationException());
        Command published = new StandardOptions(new CommandTraits { Input = Book, Output = OutputTarget.Directory("Parts.") })
            .CreateCommand("split", "Splits.", []);

        string[] declared = edited.Options.Concat(published.Options)
            .SelectMany(static option => option.Aliases.Prepend(option.Name))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(StandardOptionNames.Reserved.Order(StringComparer.Ordinal), declared);
    }

    [Fact]
    public void Create_RejectsAProductOptionThatRepeatsACommonOption()
    {
        var traits = new CommandTraits { Input = Book, Other = new InputDocument("Other book.", "the other book", "right") };

        ArgumentException error = Assert.Throws<ArgumentException>(() => new StandardOptions(traits).CreateCommand(
            "compare", "Compares.", [new Option<string>("--right-password").WithInput(InputKind.None)]));

        Assert.Contains("--right-password", error.Message, StringComparison.Ordinal);
    }

    private const string Document = """{"ops":[{"op":"set","value":1}]}""";

    private static readonly InputDocument Book = new("Book to edit.", "the book");

    private static BoundedEditDefinition<TestOp, TestBatch> Definition() => new()
    {
        Contracts = TestContracts.Json,
        // The extensions of the files the tests name as outputs.
        Writes =
        [
            FormatDescriptor.Declare("tst", FormatUse.Input | FormatUse.Convert, 0, 0, null, false, ".test"),
            FormatDescriptor.Declare("json", FormatUse.Convert, null, 1, null, false, ".json"),
            FormatDescriptor.Declare("png", FormatUse.Convert, null, 2, null, false, ".png"),
        ],
    };

    private static BoundedEditCommand<TestOp, TestBatch> Plain() => new(Definition());

    private static BoundedEditCommand<TestOp, TestBatch> WithDirectives() => new(Definition() with
    {
        SetDirectives = new(
            "Set a value.",
            static text => int.TryParse(text, out int value)
                ? new SetOp { Value = value }
                : throw CliErrors.OptionInvalid("--set", $"'{text}' is not a number", "Pass an integer."),
            static ops => new TestBatch { Ops = ops }),
    });

    private BoundedEditInvocation<TestBatch> Read(
        BoundedEditCommand<TestOp, TestBatch> command,
        params string[] arguments) =>
        ReadWithEnvironment(command, static _ => null, arguments);

    private BoundedEditInvocation<TestBatch> ReadWithEnvironment(
        BoundedEditCommand<TestOp, TestBatch> command,
        Func<string, string?> readEnvironment,
        params string[] arguments)
    {
        BoundedEditInvocation<TestBatch>? read = null;
        TestCommandRunner runner = Runner(readEnvironment);
        ParseResult parse = Edit(
            command, runner, new CommandTraits { Input = Book }, [],
            (_, edit, _) =>
            {
                read = edit;
                return new TestResult();
            })
            .Parse([Path.GetFileName(_input), .. arguments]);
        Assert.Empty(parse.Errors);
        parse.Invoke();
        return runner.Error is { } error ? throw error : read!;
    }

    private ParseResult Parse(BoundedEditCommand<TestOp, TestBatch> edit, params string[] arguments) =>
        Edit(
            edit, Runner(), new CommandTraits { Input = Book }, [],
            static (_, _, _) => throw new InvalidOperationException("Only parsed."))
            .Parse([Path.GetFileName(_input), .. arguments]);

    // The edit command of a test product, whose handler returns what the binding made of the command line.
    private static Command Edit(
        BoundedEditCommand<TestOp, TestBatch> edit,
        TestCommandRunner runner,
        CommandTraits traits,
        IReadOnlyList<Symbol> parameters,
        Func<ParseResult, BoundedEditInvocation<TestBatch>, StandardInvocation, TestResult> bind,
        Action<ParseResult>? checkUsage = null) =>
        TestProducts.Command<object>(runner, product => product.Command(
                () => EditDefinition.Create<TestOp, TestBatch, TestResult, TestResult>(
                    edit, "Edits.", traits, parameters, bind, NoTable, checkUsage),
                static (_, request) => request))
            .Subcommands.Single();

    private TestCommandRunner Runner(Func<string, string?>? readEnvironment = null) =>
        new(
            _temp.Path,
            static context => TestBindings.Create(TestProducts.Manifest.Id, static () => new object(), context: context),
            readEnvironment ?? (static _ => null));

    private static void NoTable(TestResult result, TableSurface table)
    {
    }

#pragma warning disable APCLI003 // A test result, not a product JSON root.
    private sealed record TestResult() : ResultEnvelope("test/result", 1);
#pragma warning restore APCLI003
}
