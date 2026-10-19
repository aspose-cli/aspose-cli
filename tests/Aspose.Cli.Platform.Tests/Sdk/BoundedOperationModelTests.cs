using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Diagnostics;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Operations;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Sdk;

/// <summary>
/// The SDK runner, applying a batch through a product's object model, derives every outcome
/// from the snapshots: the product counts nothing.
/// </summary>
public sealed class BoundedOperationModelTests
{
    private static readonly OperationCatalog<TestOp> Catalog = TestOp.Catalog;

    private static IReadOnlyList<TestOp> Batch(params TestOp[] ops) => Catalog.Prepare(new TestBatch { Ops = ops }).Ops;

    /// <summary>A document of pages and named values; each operation edits it as the test says.</summary>
    private sealed class Model(Action<TestOp, Model> apply) : IOperationModel<TestOp>
    {
        public List<string> Pages { get; } = ["a", "b", "c"];

        public Dictionary<string, string> Values { get; } = new(StringComparer.Ordinal) { ["value/x"] = "1" };

        public Func<TestOp, NoEffect?>? Explanation { get; init; }

        /// <summary>The call of <see cref="Snapshot"/>, counted from 1, that fails inside the engine.</summary>
        public int? FailingSnapshot { get; init; }

        private int _snapshots;

        public OperationSnapshot Snapshot(TestOp operation)
        {
            if (++_snapshots == FailingSnapshot)
            {
                throw new EngineOpException("the page cannot be read", new InvalidDataException("damaged"));
            }

            var snapshot = new OperationSnapshot().AddSequence("page", Pages);
            foreach ((string address, string state) in Values)
            {
                snapshot.Add(address, state);
            }

            return snapshot;
        }

        public void Apply(TestOp operation) => apply(operation, this);

        public IReadOnlyList<string> AttemptedTargets(TestOp operation) => ["page/all"];

        public NoEffect? Explain(TestOp operation) => Explanation?.Invoke(operation);
    }

    [Fact]
    public void Run_CountsWhatTheSnapshotsSeeChangeAndThePagesInTheOutput()
    {
        var model = new Model(static (op, document) =>
        {
            switch (((SetOp)op).Value)
            {
                case 1:
                    document.Values["value/x"] = "2";
                    document.Pages[1] = "B";
                    break;
                case 2:
                    document.Pages.Insert(0, "new");
                    break;
                case 3:
                    document.Pages.RemoveAt(3);
                    break;
            }
        });

        AppliedBatch applied = BoundedOperationRunner.Run(
            Catalog, Batch(new SetOp { Value = 1 }, new SetOp { Value = 2 }, new SetOp { Value = 3 }), bestEffort: false, deadline: null, model, pageUnit: "page");

        Assert.Equal([2L, 1L, 1L], applied.Outcomes.Select(static outcome => outcome.ItemsAffected));
        Assert.Equal(["value/x", "page/2"], applied.Outcomes[0].Targets);
        Assert.Equal(["page/1"], applied.Outcomes[1].Targets);
        Assert.Equal(["page/4"], applied.Outcomes[2].Targets);
        // The deleted page is not in the output.
        Assert.Equal([1, 2], applied.Pages);
        Assert.Empty(applied.Warnings);
    }

    [Fact]
    public void Run_WarnsThatAnOperationThatChangedNothingHadNoEffect()
    {
        var model = new Model(static (_, document) => document.Values["value/x"] = "1");

        AppliedBatch applied = BoundedOperationRunner.Run(Catalog, Batch(new SetOp { Value = 1 }), bestEffort: false, deadline: null, model);

        BoundedOperationOutcome outcome = Assert.Single(applied.Outcomes);
        Assert.Equal(OpStatuses.Ok, outcome.Status);
        Assert.Equal(0, outcome.ItemsAffected);
        Assert.Empty(outcome.Targets);
        Warning warning = Assert.Single(applied.Warnings);
        Assert.Equal("OP_NO_EFFECT", warning.Code.Name);
        Assert.Equal("op-0001", warning.Details!["id"]!.GetValue<string>());
        Assert.Equal("set", warning.Details["op"]!.GetValue<string>());
        Assert.Equal(NoEffectReasons.NoChange, warning.Details["reason"]!.GetValue<string>());
        Assert.Equal("Operation op-0001 (set) changed nothing: what it addresses already had the state it sets.", warning.Message);
    }

    [Fact]
    public void Run_TakesTheProductsExplanationOfNoEffect()
    {
        var model = new Model(static (_, _) => { })
        {
            Explanation = static op => new NoEffect(NoEffectReasons.NoMatch, $"Operation {op.Id} (note) found no anchor.") { Hint = "Add an anchor." },
        };

        AppliedBatch applied = BoundedOperationRunner.Run(Catalog, Batch(new NoteOp { Text = "n" }), bestEffort: false, deadline: null, model);

        Warning warning = Assert.Single(applied.Warnings);
        Assert.Equal(NoEffectReasons.NoMatch, warning.Details!["reason"]!.GetValue<string>());
        Assert.Equal("Operation op-0001 (note) found no anchor.", warning.Message);
        Assert.Equal("Add an anchor.", warning.Hint);
    }

    [Fact]
    public void Run_ReportsTheAttemptedTargetsOfAFailedOperation()
    {
        var model = new Model(static (op, document) =>
        {
            if (op is NoteOp)
            {
                throw new OperationInvalidException("the note has no anchor");
            }

            document.Pages[0] = "A";
        });

        AppliedBatch applied = BoundedOperationRunner.Run(
            Catalog, Batch(new NoteOp { Text = "n" }, new SetOp { Value = 1 }), bestEffort: true, deadline: null, model);

        Assert.Equal([OpStatuses.Failed, OpStatuses.Ok], applied.Outcomes.Select(static outcome => outcome.Status));
        Assert.Equal(["page/all"], applied.Outcomes[0].Targets);
        Assert.Equal(0, applied.Outcomes[0].ItemsAffected);
        Assert.Empty(applied.Warnings);
    }

    /// <summary>
    /// An operation that applied but whose result cannot be read is neither reported failed, which
    /// would deny a change the document holds, nor counted: the batch stops and nothing is written.
    /// </summary>
    [Fact]
    public void Run_StopsTheBatchWhenWhatAnAppliedOperationChangedCannotBeRead()
    {
        var model = new Model(static (_, document) => document.Pages[0] = "A") { FailingSnapshot = 2 };

        CliException error = Assert.Throws<CliException>(() => BoundedOperationRunner.Run(
            Catalog, Batch(new SetOp { Value = 1 }, new SetOp { Value = 2 }), bestEffort: true, deadline: null, model));

        Assert.Equal(ErrorCodes.FeatureUnsupported, error.Code);
        Assert.Equal("op-0001", error.Details!["id"]!.GetValue<string>());
        Assert.Equal("set", error.Details["op"]!.GetValue<string>());
        Assert.Contains("nothing was written: it applied, but reading what it changed failed: the page cannot be read", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_CoversMoreThanTheMaximumTargetsWithOneRangePerUnit()
    {
        var model = new Model(static (_, document) =>
        {
            for (int index = 0; index < document.Pages.Count; index++)
            {
                document.Pages[index] += "'";
            }

            for (int index = 0; index < 101; index++)
            {
                document.Values[$"value/n{index}"] = "new";
            }
        });
        model.Pages.AddRange(Enumerable.Range(4, 147).Select(static page => $"p{page}"));

        AppliedBatch applied = BoundedOperationRunner.Run(Catalog, Batch(new SetOp { Value = 1 }), bestEffort: false, deadline: null, model, pageUnit: "page");

        BoundedOperationOutcome outcome = Assert.Single(applied.Outcomes);
        Assert.Equal(251, outcome.ItemsAffected);
        Assert.Equal(["value/all", "page/1-150"], outcome.Targets);
        Assert.Equal(150, applied.Pages.Count);
    }

    [Fact]
    public void Verified_ChecksTheRecordedOperationsInBatchOrderAndLeavesOneWhoseRecordFailedUnchecked()
    {
        var model = new Model(static (op, document) => document.Pages[0] = ((SetOp)op).Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var verifier = new Verifier();

        AppliedBatch applied = BoundedOperationRunner.Run(
            Catalog, Batch(new SetOp { Value = 1 }, new SetOp { Value = 2 }, new SetOp { Value = 3 }), bestEffort: false, deadline: null, model, verifier);
        VerifiedBatch verified = applied.Verified(new VerificationFacts([], ["op-0003", "op-0002", "op-0001"]));

        Assert.Equal([1L, 1L, 1L], verifier.Affected);
        Assert.True(verified.Ok);
        Assert.Equal(["op-0001", "op-0003"], verified.CheckedOps);
    }

    [Fact]
    public void Verified_FailsWithTheIssuesTheReadBackFound()
    {
        var model = new Model(static (_, document) => document.Pages[0] = "A");
        VerificationIssue issue = VerificationIssues.Of(
            DiagnosticDescriptor.Verification("TEST_MISMATCH", "test"), "Operation op-0001 (set): page 1 reads 'a'.", "page/1", "Inspect the page.");

        AppliedBatch applied = BoundedOperationRunner.Run(Catalog, Batch(new SetOp { Value = 1 }), bestEffort: false, deadline: null, model, new Verifier());
        VerifiedBatch verified = applied.Verified(new VerificationFacts([issue], ["op-0001"]));

        Assert.False(verified.Ok);
        Assert.Equal([issue], verified.Issues);
        Assert.Equal(["op-0001"], verified.CheckedOps);
    }

    /// <summary>A document of blocks the engine keeps by instance, each with its text.</summary>
    private sealed class BlockModel(Action<TestOp, List<Block>> apply) : IOperationModel<TestOp>
    {
        public List<Block> Blocks { get; } = [new("a"), new("b"), new("c"), new("d")];

        public OperationSnapshot Snapshot(TestOp operation) =>
            new OperationSnapshot().AddObjects("block", Blocks.Select(static block => ((object)block, block.Text)));

        public void Apply(TestOp operation) => apply(operation, Blocks);

        public IReadOnlyList<string> AttemptedTargets(TestOp operation) => ["block/all"];

        public IReadOnlyDictionary<string, IReadOnlyList<object>> Positions() =>
            new Dictionary<string, IReadOnlyList<object>> { ["block"] = [.. Blocks] };
    }

    private sealed class Block(string text)
    {
        public string Text { get; set; } = text;
    }

    [Fact]
    public void Run_NamesObjectsKeptByInstanceWhereTheOutputHoldsThem_InOrder()
    {
        var model = new BlockModel(static (op, blocks) =>
        {
            switch (((SetOp)op).Value)
            {
                case 1:
                    // Changes c and removes b, which moves c and d up without changing them.
                    blocks[2].Text = "C";
                    blocks.RemoveAt(1);
                    break;
                case 2:
                    blocks.Insert(0, new Block("new"));
                    break;
            }
        });

        AppliedBatch applied = BoundedOperationRunner.Run(
            Catalog, Batch(new SetOp { Value = 1 }, new SetOp { Value = 2 }), bestEffort: false, deadline: null, model);

        // c is block 3 of the output, after the later insertion; b, removed, keeps its number in the input.
        Assert.Equal(2L, applied.Outcomes[0].ItemsAffected);
        Assert.Equal(["block/2", "block/3"], applied.Outcomes[0].Targets);
        Assert.Equal(["block/1"], applied.Outcomes[1].Targets);
        Assert.Equal(
            [("block/1", true), ("block/2", false), ("block/3", true)],
            applied.Changed.Select(static item => (item.Address, item.InOutput)));
    }

    /// <summary>
    /// A document of pages the engine keeps by instance but compares by state: an operation that
    /// edits pages snapshots the text on each (<c>page/2/text</c>), one that moves them the page
    /// order naming the moved page, and one that deletes them the page order.
    /// </summary>
    private sealed class PageModel(Action<TestOp, List<Page>> apply) : IOperationModel<TestOp>
    {
        public List<Page> Pages { get; } = [new("a"), new("b"), new("c"), new("d")];

        public OperationSnapshot Snapshot(TestOp operation)
        {
            var snapshot = new OperationSnapshot();
            switch (((SetOp)operation).Value)
            {
                case 1:
                    for (int index = 0; index < Pages.Count; index++)
                    {
                        snapshot.Add($"page/{index + 1}/text", Pages[index].Text, owner: Pages[index]);
                    }

                    return snapshot;
                case 2:
                    return snapshot.AddSequence("page", Pages.Select(static page => page.Id), moved: [Pages[0].Id], owners: Pages);
                default:
                    return snapshot.AddSequence("page", Pages.Select(static page => page.Id), owners: Pages);
            }
        }

        public void Apply(TestOp operation) => apply(operation, Pages);

        public IReadOnlyList<string> AttemptedTargets(TestOp operation) => ["page/all"];

        public IReadOnlyDictionary<string, IReadOnlyList<object>> Positions() =>
            new Dictionary<string, IReadOnlyList<object>> { ["page"] = [.. Pages] };
    }

    private sealed class Page(string id)
    {
        public string Id { get; } = id;

        public string Text { get; set; } = id;
    }

    [Fact]
    public void Run_AnObjectAnEarlierOperationChanged_IsReportedWhereALaterMoveLeavesIt()
    {
        var model = new PageModel(static (op, pages) =>
        {
            switch (((SetOp)op).Value)
            {
                case 1:
                    pages[0].Text = "A";
                    break;
                case 2:
                    // a moves to the end: b, c, d, a.
                    Page first = pages[0];
                    pages.RemoveAt(0);
                    pages.Add(first);
                    break;
                case 3:
                    // b is deleted: c, d, a.
                    pages.RemoveAt(0);
                    break;
            }
        });

        AppliedBatch applied = BoundedOperationRunner.Run(
            Catalog, Batch(new SetOp { Value = 1 }, new SetOp { Value = 2 }, new SetOp { Value = 3 }), bestEffort: false, deadline: null, model, pageUnit: "page");

        // A page that only moved still counts: the order is compared by state.
        Assert.Equal([1L, 1L, 1L], applied.Outcomes.Select(static outcome => outcome.ItemsAffected));
        Assert.Equal(["page/3/text"], applied.Outcomes[0].Targets);
        Assert.Equal(["page/3"], applied.Outcomes[1].Targets);
        // b is gone, so it keeps its number in the input.
        Assert.Equal(["page/2"], applied.Outcomes[2].Targets);
        Assert.Equal([3], applied.Pages);
    }

    /// <summary>A workbook of named sheets, each a scope whose name begins the addresses of its cells.</summary>
    private sealed class SheetModel(Action<TestOp, List<Sheet>> apply) : IOperationModel<TestOp>
    {
        public List<Sheet> Sheets { get; } = [new("Old"), new("Other")];

        public OperationSnapshot Snapshot(TestOp operation)
        {
            var snapshot = new OperationSnapshot();
            foreach (Sheet sheet in Sheets)
            {
                snapshot.Add($"sheet/{sheet.Name}", "sheet");
                snapshot.Add($"cell/{sheet.Name}!A1", sheet.A1);
            }

            return snapshot;
        }

        public void Apply(TestOp operation) => apply(operation, Sheets);

        public IReadOnlyList<string> AttemptedTargets(TestOp operation) => ["sheet/all"];

        public NamedScopes Scopes() => new(["sheet", "cell"], [.. Sheets.Select(static sheet => ((object)sheet, sheet.Name))]);
    }

    private sealed class Sheet(string name)
    {
        public string Name { get; set; } = name;

        public string A1 { get; set; } = "";
    }

    /// <summary>
    /// An address an operation reported under a sheet name that a later operation changed is
    /// reported under the output's name; a later sheet that takes the old name keeps its own.
    /// </summary>
    [Fact]
    public void Run_NamesTheScopeOfAnAddressAsTheOutputNamesIt()
    {
        var model = new SheetModel(static (op, sheets) =>
        {
            switch (((SetOp)op).Value)
            {
                case 1:
                    sheets[0].A1 = "x";
                    break;
                case 2:
                    sheets[0].Name = "New";
                    break;
                case 3:
                    sheets.Add(new Sheet("Old") { A1 = "y" });
                    break;
                case 4:
                    sheets.RemoveAt(0);
                    break;
            }
        });

        AppliedBatch applied = BoundedOperationRunner.Run(
            Catalog, Batch(new SetOp { Value = 1 }, new SetOp { Value = 2 }, new SetOp { Value = 3 }), bestEffort: false, deadline: null, model);

        Assert.Equal(["cell/New!A1"], applied.Outcomes[0].Targets);
        Assert.Contains("sheet/New", applied.Outcomes[1].Targets);
        Assert.Equal(["sheet/Old", "cell/Old!A1"], applied.Outcomes[2].Targets);
    }

    /// <summary>A sheet the batch renames and then removes is named as the input names it, in every target.</summary>
    [Fact]
    public void Run_NamesARenamedScopeTheBatchRemovedAsTheInputNamesIt()
    {
        var model = new SheetModel(static (op, sheets) =>
        {
            switch (((SetOp)op).Value)
            {
                case 2:
                    sheets[0].Name = "New";
                    break;
                case 4:
                    sheets.RemoveAt(0);
                    break;
            }
        });

        AppliedBatch applied = BoundedOperationRunner.Run(
            Catalog, Batch(new SetOp { Value = 2 }, new SetOp { Value = 4 }), bestEffort: false, deadline: null, model);

        Assert.All(applied.Outcomes.SelectMany(static outcome => outcome.Targets), target => Assert.DoesNotContain("New", target, StringComparison.Ordinal));
        Assert.Equal(["sheet/Old", "cell/Old!A1"], applied.Outcomes[1].Targets);
    }

    [Fact]
    public void Run_AnObjectWhoseInstanceTheEngineReplacedWithTheSameState_ChangedNothing()
    {
        var model = new BlockModel(static (_, blocks) => blocks[1] = new Block(blocks[1].Text));

        AppliedBatch applied = BoundedOperationRunner.Run(Catalog, Batch(new SetOp { Value = 1 }), bestEffort: false, deadline: null, model);

        BoundedOperationOutcome outcome = Assert.Single(applied.Outcomes);
        Assert.Equal(0L, outcome.ItemsAffected);
        Assert.Empty(outcome.Targets);
        Assert.Equal("OP_NO_EFFECT", Assert.Single(applied.Warnings).Code.Name);
        Assert.Empty(applied.Changed);
    }

    /// <summary>
    /// Only the SDK says what an operation changed: a product cannot construct, default or
    /// rewrite an <see cref="AppliedOperation"/>, so every count and target it reports comes from
    /// the snapshots of its <see cref="IOperationModel{TOp}"/>.
    /// </summary>
    [Fact]
    public void AppliedOperation_IsConstructedOnlyByTheSdk()
    {
        Type type = typeof(AppliedOperation);
        const System.Reflection.BindingFlags Everything = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static;

        Assert.False(type.IsValueType, "A struct can be constructed as default by anyone.");
        Assert.True(type.IsSealed);
        Assert.All(type.GetConstructors(Everything), static constructor => Assert.True(constructor.IsAssembly || constructor.IsPrivate, constructor.ToString()));
        Assert.DoesNotContain(type.GetMethods(Everything), static method =>
            (method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly)
            && (method.ReturnType == typeof(AppliedOperation) || method.Name.StartsWith("set_", StringComparison.Ordinal)));
    }

    [Fact]
    public void Run_ListsUpToTheMaximumTargetsAsTheyAre()
    {
        var model = new Model(static (_, document) =>
        {
            for (int index = 0; index < document.Pages.Count; index++)
            {
                document.Pages[index] += "'";
            }
        });
        model.Pages.AddRange(Enumerable.Range(4, BoundedOperationOutcome.MaximumTargets - 3).Select(static page => $"p{page}"));

        AppliedBatch applied = BoundedOperationRunner.Run(Catalog, Batch(new SetOp { Value = 1 }), bestEffort: false, deadline: null, model);

        Assert.Equal(100, BoundedOperationOutcome.MaximumTargets);
        Assert.Equal(Enumerable.Range(1, 100).Select(static page => $"page/{page}"), Assert.Single(applied.Outcomes).Targets);
    }

    /// <summary>Records each operation; the second one's record throws.</summary>
    private sealed class Verifier : IEditVerifier<TestOp>
    {
        public List<long> Affected { get; } = [];

        public void Record(TestOp operation, AppliedOperation applied)
        {
            Affected.Add(applied.ItemsAffected);
            if (operation.Id == "op-0002")
            {
                throw new InvalidOperationException("The effect cannot be read.");
            }
        }
    }
}
