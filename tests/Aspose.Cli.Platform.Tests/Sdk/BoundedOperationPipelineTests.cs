using Aspose.Cli.Sdk;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Operations;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Sdk;

public sealed class BoundedOperationPipelineTests
{
    private static readonly OperationCatalog<TestOp> Catalog =
        new OperationCatalog<TestOp>(DistributionInfo.SchemaBaseUri + "test/ops.schema.json", maximumOperations: 3)
            .Add<SetOp>("set", static op => OperationInvalidException.Require(op.Value >= 0, "value must not be negative"))
            .Add<NoteOp>("note");

    [Fact]
    public void Prepare_AssignsIdsAndReportsTheFailingOperationPosition()
    {
        var batch = new TestBatch { Ops = [new SetOp(1), new NoteOp(), new SetOp(-1)] };

        CliException error = Assert.Throws<CliException>(() => Catalog.Prepare(batch));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Equal(2, error.Details!["index"]!.GetValue<int>());
        Assert.Equal("set", error.Details["op"]!.GetValue<string>());
        Assert.Equal("value must not be negative", error.Details["reason"]!.GetValue<string>());
        Assert.Contains("aspose-cli schema v2/test/ops", error.Hint, StringComparison.Ordinal);
        Assert.Equal(["op-0001", "op-0002"], Catalog.Prepare(new TestBatch { Ops = [new SetOp(1), new NoteOp()] })
            .Ops.Select(static op => op.Id));
    }

    [Fact]
    public void Prepare_EnforcesTheDeclaredOperationLimit()
    {
        CliException error = Assert.Throws<CliException>(() => Catalog.Prepare(
            new TestBatch { Ops = [new NoteOp(), new NoteOp(), new NoteOp(), new NoteOp()] }));

        Assert.Contains("1-3 operations", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_BestEffortRecordsRejectionsAndContinues()
    {
        TestBatch batch = Catalog.Prepare(new TestBatch { Ops = [new SetOp(1), new NoteOp(), new SetOp(2)] });

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
        TestBatch batch = Catalog.Prepare(new TestBatch { Ops = [new NoteOp()] });

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
        TestBatch batch = Catalog.Prepare(new TestBatch { Ops = [new SetOp(1), new NoteOp()] });
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
    }

    [Fact]
    public void Catalog_RejectsDuplicateRegistrationsAndUnregisteredTypes()
    {
        var catalog = new OperationCatalog<TestOp>(DistributionInfo.SchemaBaseUri + "test/ops.schema.json", 1)
            .Add<SetOp>("set");

        Assert.Throws<ArgumentException>(() => catalog.Add<SetOp>("set-again"));
        Assert.Throws<ArgumentException>(() => catalog.Add<NoteOp>("set"));
        Assert.Throws<InvalidOperationException>(() => catalog.NameOf(new NoteOp()));
        Assert.Equal(["set"], catalog.Names);
        Assert.Equal(["set"], catalog.Registry.Keys);
    }

    private static IReadOnlyList<BoundedOperationOutcome> Run(
        TestBatch batch, bool bestEffort, Func<TestOp, int, AppliedOperation> apply) =>
        BoundedOperationRunner.Run(Catalog, batch.Ops, bestEffort, deadline: null, apply, static (_, _) => ["test/attempted"]);

    public abstract record TestOp : BoundedOperation;

    public sealed record SetOp(int Value) : TestOp;

    public sealed record NoteOp : TestOp;

    public sealed record TestBatch : BoundedOperationEnvelope<TestOp>;
}
