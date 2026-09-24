using System.CommandLine;
using System.Text.Json.Serialization.Metadata;
using Aspose.Cli.Sdk;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
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
            .Add<LinkOp>("link");

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
        Assert.False(invocation.OpsFromStandardInput);
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
    public void OutputFileOptions_NeverResolveToTheInput()
    {
        var output = new OutputFileOptions("Output path.");
        var command = new Command("convert");
        output.AddTo(command);

        CliException error = Assert.Throws<CliException>(() => output.ResolvePath(
            command.Parse(["--out", "BOOK.TEST"]), new PathResolver(_temp.Path), _input, ".test"));

        Assert.Equal(ErrorCodes.OptionInvalid, error.Code);
        Assert.DoesNotContain("--in-place", error.Hint, StringComparison.Ordinal);
        Assert.Equal(_temp.File("book.out.test"), output.ResolvePath(
            command.Parse([]), new PathResolver(_temp.Path), _input, ".test"));
    }

    [Fact]
    public void OutputFileOptions_NeverResolveToAnyInput()
    {
        var output = new OutputFileOptions("Output path.");
        var command = new Command("merge");
        output.AddTo(command);
        var paths = new PathResolver(_temp.Path);
        string other = _temp.File("other.test");
        File.WriteAllText(other, "other");

        CliException error = Assert.Throws<CliException>(() => output.Resolve(
            command.Parse(["--out", "OTHER.test"]), paths, _input, null, other));
        CliException argument = Assert.Throws<CliException>(() => OutputFileOptions.ResolveExplicit(
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
                ? link with { Path = paths.ResolveOutput(link.Path) }
                : op,
        });

        LinkOp link = Assert.IsType<LinkOp>(Assert.Single(
            Read(command, "--ops", """{"ops":[{"op":"link","path":"image.png"}]}""").Batch.Ops));

        Assert.Equal(_temp.File("image.png"), link.Path);
    }

    private const string Document = """{"ops":[{"op":"set","value":1}]}""";

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
        command.Read(Parse(command, arguments), new PathResolver(_temp.Path), TestBudgets.Create().Inputs, _input);

    private static ParseResult Parse(BoundedEditCommand<TestOp, TestBatch> edit, params string[] arguments)
    {
        var command = new Command("edit");
        edit.AddTo(command);
        return command.Parse(arguments);
    }

    public abstract record TestOp : BoundedOperation;

    public sealed record SetOp(int Value) : TestOp;

    public sealed record LinkOp(string Path) : TestOp;

    public sealed record TestBatch : BoundedOperationEnvelope<TestOp>;

    private sealed class TestOpConverter() : OperationJsonConverter<TestOp>(Catalog);
}
