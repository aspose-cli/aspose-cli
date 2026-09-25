using Aspose.Cli.Sdk;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Operations;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Sdk;

public sealed class BoundedOperationPipelineTests
{
    private static readonly OperationCatalog<TestOp> Catalog = TestOp.Catalog;

    private static SetOp Set(int value) => new() { Value = value };

    private static NoteOp Note() => new() { Text = "note" };

    [Fact]
    public void Prepare_AssignsIdsAndReportsTheFailingOperationPosition()
    {
        var batch = new TestBatch { Ops = [Set(1), Note(), Set(-1)] };

        CliException error = Assert.Throws<CliException>(() => Catalog.Prepare(batch));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Equal(2, error.Details!["index"]!.GetValue<int>());
        Assert.Equal("set", error.Details["op"]!.GetValue<string>());
        Assert.Equal("value must be at least 0", error.Details["reason"]!.GetValue<string>());
        Assert.Contains("aspose-cli schema v2/test/ops", error.Hint, StringComparison.Ordinal);
        Assert.Equal(["op-0001", "op-0002"], Catalog.Prepare(new TestBatch { Ops = [Set(1), Note()] })
            .Ops.Select(static op => op.Id));
    }

    [Fact]
    public void Prepare_EnforcesTheDeclaredOperationLimit()
    {
        CliException error = Assert.Throws<CliException>(() => Catalog.Prepare(
            new TestBatch { Ops = [.. Enumerable.Range(0, 9).Select(static _ => Note())] }));

        Assert.Contains("1-8 operations", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_BestEffortRecordsRejectionsAndContinues()
    {
        TestBatch batch = Catalog.Prepare(new TestBatch { Ops = [Set(1), Note(), Set(2)] });

        IReadOnlyList<BoundedOperationOutcome> outcomes = Run(batch, bestEffort: true, (op, _) => op is NoteOp
            ? throw new OperationInvalidException("the note has no anchor")
            : new AppliedOperation(1, ["test/value"]));

        Assert.Equal([OpStatuses.Ok, OpStatuses.Failed, OpStatuses.Ok], outcomes.Select(static item => item.Status));
        Assert.Equal(ErrorCodes.OpsInvalid.Name, outcomes[1].Error!.Code);
        Assert.Equal(["test/attempted"], outcomes[1].Targets);
    }

    [Fact]
    public void Run_KeepsADomainFailureCodeAndAddsItsPosition()
    {
        TestBatch batch = Catalog.Prepare(new TestBatch { Ops = [Note()] });

        CliException error = Assert.Throws<CliException>(() => Run(batch, bestEffort: false,
            (_, _) => throw CliErrors.FileNotFound("missing.png")));

        Assert.Equal(ErrorCodes.FileNotFound, error.Code);
        Assert.Equal(0, error.Details!["index"]!.GetValue<int>());
        Assert.Equal("note", error.Details["op"]!.GetValue<string>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Run_StopsTheBatchWhenTheEngineFailsMidChange(bool bestEffort)
    {
        TestBatch batch = Catalog.Prepare(new TestBatch { Ops = [Set(1), Note()] });
        int applied = 0;

        CliException error = Assert.Throws<CliException>(() => Run(batch, bestEffort, (op, _) =>
        {
            applied++;
            return op is NoteOp
                ? throw new EngineOpException("engine failed", new InvalidOperationException())
                : new AppliedOperation(1, []);
        }));

        Assert.Equal(ErrorCodes.FeatureUnsupported, error.Code);
        Assert.Equal(1, error.Details!["index"]!.GetValue<int>());
        Assert.Equal(2, applied);
        Assert.Contains("the batch stopped and nothing was written", error.Message, StringComparison.Ordinal);
        Assert.Equal(CliErrors.EngineFailed("any", new InvalidOperationException()).Hint, error.Hint);
    }

    private static IReadOnlyList<BoundedOperationOutcome> Run(
        TestBatch batch, bool bestEffort, Func<TestOp, int, AppliedOperation> apply) =>
        BoundedOperationRunner.Run(Catalog, batch.Ops, bestEffort, deadline: null, apply, static (_, _) => ["test/attempted"]);
}
