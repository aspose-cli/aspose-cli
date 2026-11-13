using System.Globalization;
using Aspose.Cli.Sdk.Operations;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Sdk;

/// <summary>
/// The objects a batch changed (<see cref="AppliedBatch.Changed"/>) are listed once each, unit by
/// unit in the order the units first appear and by number within a unit, an object kept by
/// instance first, with whether its last change left it in the output; and listing them takes time
/// in proportion to how many there are, so an operation that changes hundreds of thousands of
/// cells is not held up by the list.
/// </summary>
public sealed class BoundedOperationBatchOrderTests
{
    private static readonly OperationCatalog<TestOp> Catalog = TestOp.Catalog;

    private static IReadOnlyList<TestOp> Batch(params TestOp[] ops) => Catalog.Prepare(new TestBatch { Ops = ops }).Ops;

    [Fact]
    public void Changed_ListsEachObjectOnce_UnitByUnitAndByNumber()
    {
        var model = new Model(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["value/z"] = "1", ["page/10"] = "1", ["note/b"] = "1", ["page/2"] = "1", ["value/a"] = "1", ["page/1"] = "1",
        });
        model.Edits[1] = static document =>
        {
            foreach (string address in new[] { "value/z", "page/10", "page/2", "value/a" })
            {
                document.Values[address] = "2";
            }
        };
        model.Edits[2] = static document =>
        {
            document.Values["page/2"] = "3";
            document.Values["note/b"] = "2";
            document.Blocks[1].Text = "changed";
        };
        model.Edits[3] = static document =>
        {
            document.Values["value/z"] = "3";
            document.Values["page/1"] = "2";
            document.Values.Remove("value/a");
        };

        AppliedBatch applied = BoundedOperationRunner.Run(
            Catalog, Batch(new SetOp { Value = 1 }, new SetOp { Value = 2 }, new SetOp { Value = 3 }), bestEffort: false, deadline: null, model);

        Assert.Equal(
            [("block/2", true), ("value/z", true), ("value/a", false), ("page/1", true), ("page/2", true), ("page/10", true), ("note/b", true)],
            applied.Changed.Select(static item => (item.Address, item.InOutput)));
    }

    [Fact]
    public async Task Changed_OfTwoHundredThousandObjects_IsListedInLinearTime()
    {
        const int Count = 200_000;
        var values = new Dictionary<string, string>(Count, StringComparer.Ordinal);
        for (int number = Count; number >= 1; number--)
        {
            values[string.Create(CultureInfo.InvariantCulture, $"row/{number}")] = "before";
        }

        var model = new Model(values, blocks: 0);
        model.Edits[1] = static document =>
        {
            foreach (string address in document.Values.Keys.ToArray())
            {
                document.Values[address] = "after";
            }
        };

        // Run apart, so a list that takes time in proportion to the square of its length, minutes
        // here where a linear one takes a fraction of a second, fails at the limit instead.
        Task<AppliedBatch> run = Task.Run(() => BoundedOperationRunner.Run(Catalog, Batch(new SetOp { Value = 1 }), bestEffort: false, deadline: null, model));

        Assert.True(await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(10))) == run, "Listing 200,000 changed objects took more than 10 seconds.");
        AppliedBatch applied = await run;
        Assert.Equal(Count, applied.Changed.Count);
        Assert.Equal(("row/1", "row/200000"), (applied.Changed[0].Address, applied.Changed[^1].Address));
    }

    /// <summary>A document of blocks kept by instance and of named values; each operation runs the edit its value names.</summary>
    private sealed class Model(Dictionary<string, string> values, int blocks = 3) : IOperationModel<TestOp>
    {
        public Dictionary<string, string> Values { get; } = values;

        public List<Block> Blocks { get; } = [.. Enumerable.Range(1, blocks).Select(static number => new Block(number.ToString(CultureInfo.InvariantCulture)))];

        public Dictionary<int, Action<Model>> Edits { get; } = [];

        public OperationSnapshot Snapshot(TestOp operation)
        {
            var snapshot = new OperationSnapshot().AddObjects("block", Blocks.Select(static block => ((object)block, block.Text)));
            foreach ((string address, string state) in Values)
            {
                snapshot.Add(address, state);
            }

            return snapshot;
        }

        public void Apply(TestOp operation) => Edits[((SetOp)operation).Value](this);

        public IReadOnlyList<string> AttemptedTargets(TestOp operation) => ["value/all"];

        public IReadOnlyDictionary<string, IReadOnlyList<object>> Positions() =>
            new Dictionary<string, IReadOnlyList<object>> { ["block"] = [.. Blocks] };
    }

    private sealed class Block(string text)
    {
        public string Text { get; set; } = text;
    }
}
