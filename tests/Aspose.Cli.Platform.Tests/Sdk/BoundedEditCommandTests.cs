using System.CommandLine;
using System.Text.Json.Serialization.Metadata;
using Aspose.Cli.Sdk;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Operations;
using Aspose.Cli.Sdk.Serialization;
using Aspose.Cli.Sdk.Tests;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Sdk;

public sealed class BoundedEditCommandTests : IDisposable
{
    private static readonly OperationCatalog<TestOp> Catalog =
        new OperationCatalog<TestOp>(DistributionInfo.SchemaBaseUri + "test/ops.schema.json", maximumOperations: 10)
            .Add<SetOp>("set", static op => OperationInvalidException.Require(op.Value >= 0, "value must not be negative"))
            .Add<LinkOp>("link")
            .Add<SecretOp>("secret");

    private static readonly ProductJsonDefinition Contracts =
        new("test", new DefaultJsonTypeInfoResolver(), [new TestOpConverter()]);

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
        MutationTarget inPlace = Read(Plain(), "--ops", Document, "--in-place").Target;
        MutationTarget backedUp = Read(Plain(), "--ops", Document, "--in-place", "--backup").Target;

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
        Assert.Equal(_input, Read(Plain(), "--ops", Document, "--in-place").Target.OutputPath);
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
    [InlineData("""{"ops":[{"op":"set","value":1},{"op":"set","value":1,"extra":true}]}""", 1, "set", "unknown field 'extra'")]
    [InlineData("""{"ops":[{"op":"set","value":"one"}]}""", 0, "set", "'value' must be a whole number")]
    [InlineData("""{"ops":[{"op":"set","value":1,"value":2}]}""", 0, "set", "'value' is duplicated")]
    [InlineData("""{"ops":[{"op":"link","path":null}]}""", 0, "link", "'path' must not be null")]
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
    [InlineData("""{"ops":{"op":"set","value":1}}""", "'ops' must be an array")]
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
        var command = new BoundedEditCommand<TestOp, TestBatch>(Definition() with
        {
            NormalizePaths = static (op, paths) => op is LinkOp link
                ? link with { Path = paths.ResolveInput(link.Path) }
                : op,
        });
        File.WriteAllText(_temp.File("image.png"), "image");

        LinkOp link = Assert.IsType<LinkOp>(Assert.Single(
            Read(command, "--ops", """{"ops":[{"op":"link","path":"image.png"}]}""").Batch.Ops));

        Assert.Equal(_temp.File("image.png"), link.Path);
    }

    [Fact]
    public void Read_NeverPublishesToTheOperationDocumentOrAFileAnOperationReads()
    {
        var linking = new BoundedEditCommand<TestOp, TestBatch>(Definition() with
        {
            NormalizePaths = static (op, paths) => op is LinkOp link
                ? link with { Path = paths.ResolveInput(link.Path) }
                : op,
        });
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
        Assert.Equal(_input, Read(linking, "--ops", image, "--in-place").Target.OutputPath);
        Assert.Equal(_temp.File("copy.test"), Read(linking, "--ops", image, "--out", "copy.test").Target.OutputPath);
    }

    [Fact]
    public void Read_ResolvesEachOperationSecretOnceByVariableName()
    {
        var command = new BoundedEditCommand<TestOp, TestBatch>(Definition() with
        {
            SecretVariables = static op => op is SecretOp secret ? [secret.PasswordEnv] : [],
        });
        const string document = """{"ops":[{"op":"secret","passwordEnv":"OWNER"},{"op":"secret","passwordEnv":"OWNER"},{"op":"secret"},{"op":"secret","passwordEnv":"ABSENT"},{"op":"secret","passwordEnv":"ABSENT"}]}""";
        var reads = new List<string>();

        IReadOnlyDictionary<string, string> secrets = ReadWithEnvironment(command, name =>
        {
            reads.Add(name);
            return name == "OWNER" ? "owner-secret" : null;
        }, "--ops", document).Secrets;

        Assert.Equal("owner-secret", Assert.Single(secrets).Value);
        Assert.Equal(["OWNER", "ABSENT"], reads);
        Assert.Empty(Read(Plain(), "--ops", document).Secrets);
    }

    [Fact]
    public void OperationSecrets_FailOnlyTheOperationThatNamesAMissingVariable()
    {
        var secrets = new Dictionary<string, string> { ["OWNER"] = "owner-secret" };

        OperationInvalidException missing = Assert.Throws<OperationInvalidException>(
            () => OperationSecrets.Resolve(secrets, "ABSENT"));

        Assert.Equal("owner-secret", OperationSecrets.Resolve(secrets, "OWNER"));
        Assert.Null(OperationSecrets.Resolve(secrets, null));
        Assert.Contains("'ABSENT'", missing.Message, StringComparison.Ordinal);
        Assert.Throws<OperationInvalidException>(() => OperationSecrets.Resolve(null, "OWNER"));
    }

    [Fact]
    public void Create_ChecksProductUsageBeforeReadingAnyInput()
    {
        var fast = new Option<bool>("--fast");
        var host = new TestHost(_temp.Path);
        Command command = Plain().Create<object>(
            host,
            "edit",
            "Edits.",
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

        command.Parse(["missing.test", "--ops", "-", "--fast"]).Invoke();
        CliException refused = Assert.IsType<CliException>(host.Error);
        command.Parse(["missing.test", "--ops", "-"]).Invoke();
        CliException missing = Assert.IsType<CliException>(host.Error);

        Assert.Equal("--fast", refused.Details!["option"]!.GetValue<string>());
        Assert.Equal(ErrorCodes.FileNotFound, missing.Code);
    }

    [Fact]
    public void Read_NeverPublishesToAFileAProductOptionReads()
    {
        var edit = Plain();
        var source = new Option<string?>("--source").WithInput(InputKind.File);
        var host = new TestHost(_temp.Path, static _ => null);
        Command command = edit.Create<object>(
            host, "edit", "Edits.", new CommandTraits { Input = Book }, [source],
            static (_, _, _) => throw new InvalidOperationException("The output is refused first."));
        File.WriteAllText(_temp.File("source.test"), "source");

        command.Parse([Path.GetFileName(_input), "--ops", Document, "--source", "source.test", "--out", "SOURCE.test"]).Invoke();

        CliException error = Assert.IsType<CliException>(host.Error);
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
            Encrypt = new EncryptedOutput("the output book", ["test"]),
            UsesFonts = true,
        };

        Command command = edit.Create<object>(
            new TestHost(_temp.Path), "edit", "Edits.", traits, [new Option<bool>("--fast")], static (_, _, _) => throw new InvalidOperationException());

        Assert.Equal(
            [
                "--ops", "--if-match", "--dry-run", "--best-effort", "--verify", "--fast",
                "--out", "--overwrite", "--in-place", "--backup",
                "--password", "--password-env", "--password-stdin", "--encrypt", "--encrypt-env", "--font-dir",
            ],
            command.Options.Select(static option => option.Name));
        Assert.Throws<ArgumentException>(() => edit.Create<object>(
            new TestHost(_temp.Path), "edit", "Edits.", traits with { Output = OutputTarget.File("Out.") }, [],
            static (_, _, _) => throw new InvalidOperationException()));
    }

    [Fact]
    public void TheTemplateReservesExactlyTheOptionNamesItDeclares()
    {
        var host = new TestHost(_temp.Path);
        var edit = new BoundedEditCommand<TestOp, TestBatch>(Definition() with
        {
            VerifyDescription = "Verify.",
            SetDirectives = new("Set a value.", static _ => new SetOp(0), static ops => new TestBatch { Ops = ops }),
        });
        Command edited = edit.Create<object>(
            host, "edit", "Edits.",
            new CommandTraits { Input = Book, Encrypt = new EncryptedOutput("the output book", ["test"]), UsesFonts = true },
            [], static (_, _, _) => throw new InvalidOperationException());
        Command published = StandardCommand.Create<object>(
            host, "split", "Splits.",
            new CommandTraits { Input = Book, Output = OutputTarget.FileOrDirectory("Form file.", "Parts.") },
            [], static (_, _) => throw new InvalidOperationException());

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

        ArgumentException error = Assert.Throws<ArgumentException>(() => StandardCommand.Create<object>(
            new TestHost(_temp.Path), "compare", "Compares.", traits,
            [new Option<string>("--right-password").WithInput(InputKind.None)],
            static (_, _) => throw new InvalidOperationException()));

        Assert.Contains("--right-password", error.Message, StringComparison.Ordinal);
    }

    private const string Document = """{"ops":[{"op":"set","value":1}]}""";

    private static readonly InputDocument Book = new("Book to edit.", "the book");

    private static BoundedEditDefinition<TestOp, TestBatch> Definition() => new()
    {
        Catalog = Catalog,
        Contracts = Contracts,
    };

    private static BoundedEditCommand<TestOp, TestBatch> Plain() => new(Definition());

    private static BoundedEditCommand<TestOp, TestBatch> WithDirectives() => new(Definition() with
    {
        SetDirectives = new(
            "Set a value.",
            static text => int.TryParse(text, out int value)
                ? new SetOp(value)
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
        var host = new TestHost(_temp.Path, readEnvironment);
        ParseResult parse = command.Create<object>(
            host, "edit", "Edits.", new CommandTraits { Input = Book }, [],
            (_, edit, _) =>
            {
                read = edit;
                return new TestResult();
            })
            .Parse([Path.GetFileName(_input), .. arguments]);
        Assert.Empty(parse.Errors);
        parse.Invoke();
        return host.Error is { } error ? throw error : read!;
    }

    private ParseResult Parse(BoundedEditCommand<TestOp, TestBatch> edit, params string[] arguments) =>
        edit.Create<object>(
            new TestHost(_temp.Path), "edit", "Edits.", new CommandTraits { Input = Book }, [],
            static (_, _, _) => throw new InvalidOperationException("Only parsed."))
            .Parse([Path.GetFileName(_input), .. arguments]);

    public abstract record TestOp : BoundedOperation;

    public sealed record SetOp(int Value) : TestOp;

    public sealed record LinkOp(string Path) : TestOp;

    public sealed record SecretOp(string? PasswordEnv = null) : TestOp;

    public sealed record TestBatch : BoundedOperationEnvelope<TestOp>;

    private sealed record TestResult() : ResultEnvelope("test/result", 1);

    private sealed class TestOpConverter() : OperationJsonConverter<TestOp>(Catalog);

    private sealed class TestHost(string workDirectory, Func<string, string?>? readEnvironment = null) : IProductCommandHost<object>
    {
        public Exception? Error { get; private set; }

        public int Run(ParseResult parseResult, Func<ProductCommandContext<object>, ResultEnvelope> handler)
        {
            Error = null;
            try
            {
                handler(new ProductCommandContext<object>
                {
                    Binding = ProductBinding.CreateLicenseFree<object>("test", static _ => new object()),
                    Paths = new PathResolver(workDirectory),
                    Inputs = TestBudgets.Create().Inputs,
                    ReadEnvironment = readEnvironment ?? (static _ => null),
                });
            }
            catch (Exception exception)
            {
                Error = exception;
            }

            return 0;
        }
    }
}
