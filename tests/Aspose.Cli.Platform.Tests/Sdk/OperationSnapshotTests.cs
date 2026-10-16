using Aspose.Cli.Sdk.Operations;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Sdk;

public sealed class OperationSnapshotTests
{
    private static IReadOnlyList<ObjectChange> Compare(OperationSnapshot before, OperationSnapshot after) =>
        OperationSnapshot.Compare(before, after);

    private static OperationSnapshot Pages(params string[] states) => new OperationSnapshot().AddSequence("page", states);

    [Fact]
    public void Compare_NamedObjectsChangeWhenTheirStateDiffersOrTheyComeOrGo()
    {
        OperationSnapshot before = new OperationSnapshot().Add("metadata/title", "Old").Add("metadata/author", "Ann").Add("metadata/subject", "S");
        OperationSnapshot after = new OperationSnapshot().Add("metadata/title", "New").Add("metadata/author", "Ann").Add("metadata/keywords", "K");

        Assert.Equal(
            [new ObjectChange("metadata/title", true), new ObjectChange("metadata/keywords", true), new ObjectChange("metadata/subject", false)],
            Compare(before, after));
    }

    [Fact]
    public void Compare_AnInsertedPageCountsOnceAtItsNewAddress()
    {
        Assert.Equal([new ObjectChange("page/2", true)], Compare(Pages("a", "b", "c"), Pages("a", "x", "b", "c")));
    }

    [Fact]
    public void Compare_ADeletedPageCountsOnceAtTheAddressItHad()
    {
        Assert.Equal([new ObjectChange("page/2", false)], Compare(Pages("a", "b", "c"), Pages("a", "c")));
    }

    [Fact]
    public void Compare_AMovedPageCountsOnceAtItsNewAddress()
    {
        Assert.Equal([new ObjectChange("page/1", true)], Compare(Pages("a", "b", "c"), Pages("c", "a", "b")));
    }

    [Fact]
    public void Compare_ANamedMovedPageCountsAtItsNewAddressWhenItSwapsWithANeighbor()
    {
        OperationSnapshot before = new OperationSnapshot().AddSequence("page", ["a", "b", "c", "d", "e"], moved: ["d"]);

        Assert.Equal([new ObjectChange("page/3", true)], Compare(before, Pages("a", "b", "d", "c", "e")));
    }

    [Fact]
    public void Compare_NamedMovedPagesCountEachAtItsNewAddress()
    {
        OperationSnapshot before = new OperationSnapshot().AddSequence("page", ["a", "b", "c", "d", "e"], moved: ["d", "e"]);

        Assert.Equal(
            [new ObjectChange("page/3", true), new ObjectChange("page/4", true)],
            Compare(before, Pages("a", "b", "d", "e", "c")));
    }

    [Fact]
    public void Compare_ANamedMovedPageThatKeptItsPlaceChangesNothing()
    {
        OperationSnapshot before = new OperationSnapshot().AddSequence("page", ["a", "b", "c"], moved: ["b"]);

        Assert.Empty(Compare(before, Pages("a", "b", "c")));
    }

    [Fact]
    public void Compare_OnlyTheSnapshotBeforeTheOperationNamesTheMovedPages()
    {
        OperationSnapshot after = new OperationSnapshot().AddSequence("page", ["a", "c", "b"], moved: ["c"]);

        Assert.Equal([new ObjectChange("page/3", true)], Compare(Pages("a", "b", "c"), after));
    }

    [Fact]
    public void Compare_ReusedInstancesWithTheSameStatesChangeNothing()
    {
        object first = new(), second = new(), third = new(), fresh = new();
        OperationSnapshot before = new OperationSnapshot().AddObjects("block", [(first, "a"), (second, "b"), (third, "c")], reused: true);
        OperationSnapshot after = new OperationSnapshot().AddObjects("block", [(third, "a"), (fresh, "b"), (second, "c")], reused: true);

        Assert.Empty(Compare(before, after));
    }

    [Fact]
    public void Compare_AReusedInstanceWhoseStateChangedCountsOnceWithTheInstanceTheOutputHolds()
    {
        object first = new(), second = new(), third = new();
        OperationSnapshot before = new OperationSnapshot().AddObjects("block", [(first, "a"), (second, "b"), (third, "c")], reused: true);
        OperationSnapshot after = new OperationSnapshot().AddObjects("block", [(third, "a"), (first, "B"), (second, "c")], reused: true);

        Assert.Equal([new ObjectChange("block/2", true, first)], Compare(before, after));
    }

    [Fact]
    public void Compare_APageChangedInPlaceCountsOnce()
    {
        Assert.Equal([new ObjectChange("page/2", true)], Compare(Pages("a", "b", "c"), Pages("a", "B", "c")));
    }

    [Fact]
    public void Compare_EqualSnapshotsChangeNothing()
    {
        Assert.Empty(Compare(Pages("a", "a", "b"), Pages("a", "a", "b")));
        Assert.Empty(Compare(new OperationSnapshot(), new OperationSnapshot()));
    }

    [Fact]
    public void Compare_SequencesWithTheirOwnAddressesKeepThem()
    {
        OperationSnapshot before = new OperationSnapshot().AddSequence("bookmark", [("bookmark/1", "A"), ("bookmark/1/1", "A1"), ("bookmark/2", "B")]);
        OperationSnapshot after = new OperationSnapshot().AddSequence("bookmark", [("bookmark/1", "B")]);

        Assert.Equal([new ObjectChange("bookmark/1", false), new ObjectChange("bookmark/1/1", false)], Compare(before, after));
    }

    [Theory]
    [InlineData("page")]
    [InlineData("pdf page/1")]
    [InlineData("Page/1")]
    [InlineData("page/ 1")]
    [InlineData("/1")]
    public void Add_RefusesAnAddressThatIsNotUnitAndAddress(string address)
    {
        Assert.Throws<ArgumentException>(() => new OperationSnapshot().Add(address, "state"));
        Assert.Throws<ArgumentException>(() => new OperationSnapshot().AddSequence("items", [(address, "state")]));
    }

    [Fact]
    public void Add_RefusesTheSameAddressTwice()
    {
        Assert.Throws<ArgumentException>(() => new OperationSnapshot().Add("field/a", "1").Add("field/a", "2"));
    }

    [Fact]
    public void Cover_ListsOneRangePerUnitOrAllForNamedObjects()
    {
        string[] addresses = [.. Enumerable.Range(1, 150).Select(static page => $"page/{page}"), "page/152/image/1", "field/a", "field/b"];

        Assert.Equal(["page/1-150,152", "field/all"], OperationSnapshot.Cover(addresses));
    }

    /// <summary>The edit script is a shortest one: its length is what a longest common subsequence leaves.</summary>
    [Fact]
    public void Edits_AreAShortestScript()
    {
        var random = new Random(20261011);
        for (int round = 0; round < 500; round++)
        {
            string[] before = [.. Enumerable.Range(0, random.Next(0, 12)).Select(_ => ((char)('a' + random.Next(4))).ToString())];
            string[] after = [.. Enumerable.Range(0, random.Next(0, 12)).Select(_ => ((char)('a' + random.Next(4))).ToString())];

            (List<int> deleted, List<int> inserted) = SequenceDiff.Edits(before, after);

            int common = Lcs(before, after);
            Assert.Equal(before.Length - common, deleted.Count);
            Assert.Equal(after.Length - common, inserted.Count);
            string[] kept = [.. before.Where((_, index) => !deleted.Contains(index))];
            Assert.Equal(kept, after.Where((_, index) => !inserted.Contains(index)));
        }
    }

    private static int Lcs(string[] a, string[] b)
    {
        int[,] table = new int[a.Length + 1, b.Length + 1];
        for (int i = a.Length - 1; i >= 0; i--)
        {
            for (int j = b.Length - 1; j >= 0; j--)
            {
                table[i, j] = a[i] == b[j] ? table[i + 1, j + 1] + 1 : Math.Max(table[i + 1, j], table[i, j + 1]);
            }
        }

        return table[0, 0];
    }
}
