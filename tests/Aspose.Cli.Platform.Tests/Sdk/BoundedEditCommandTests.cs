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
