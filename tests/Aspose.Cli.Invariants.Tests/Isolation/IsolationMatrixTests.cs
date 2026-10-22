using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.Invariants.Tests.Isolation;

/// <summary>
/// Opening a document never contacts the network and never reads files outside the input's
/// folder (SECURITY.md, Network), for every product, readable format and command that opens a
/// document (<see cref="IsolationCases"/>). <see cref="Holds"/> is the unmarked sample every run
/// checks, CI included; <see cref="HoldsExhaustively"/> is the rest of the matrix.
/// </summary>
public sealed class IsolationMatrixTests
{
    [Theory]
    [MemberData(nameof(IsolationCases.Sample), MemberType = typeof(IsolationCases))]
    public Task Holds(string id) => IsolationCases.Verify(id);

    [Theory]
    [Category(TestCategory.Slow)]
    [MemberData(nameof(IsolationCases.Exhaustive), MemberType = typeof(IsolationCases))]
    public Task HoldsExhaustively(string id) => IsolationCases.Verify(id);

    /// <summary>
    /// Every format a product opens has a sample or a stated reason it has none, and not both, so
    /// a new format cannot leave the matrix in silence.
    /// </summary>
    [Fact]
    public void EveryOpenedFormat_HasASampleOrAReason()
    {
        string[] unclassified = [.. IsolationCases.Formats(CliCatalog.Current)
            .Select(static pair => pair.Format)
            .Distinct(StringComparer.Ordinal)
            .Where(static format => IsolationDocuments.Families.ContainsKey(format) == IsolationDocuments.NotApplicable.ContainsKey(format))
            .Order(StringComparer.Ordinal)];
        Assert.True(unclassified.Length == 0,
            "Give each format a sample in IsolationDocuments.Families or a reason in IsolationDocuments.NotApplicable, not both: "
            + string.Join(", ", unclassified));
    }

    /// <summary>Every file option of a create command names the formats it reads, so its source joins the matrix.</summary>
    [Fact]
    public void EveryCreateSource_NamesTheFormatsItReads()
    {
        string[] unknown = [.. CliCatalog.Current.Commands
            .Where(static command => command.Verb == "create")
            .SelectMany(static command => command.Options.Where(static option => option.InputKind == "file").Select(option => $"{command} {option.Name}"))
            .Where(static source => !IsolationCases.SourceFormats.ContainsKey(source[(source.LastIndexOf(' ') + 1)..]))];
        Assert.True(unknown.Length == 0, "Add the formats these create sources read to IsolationCases.SourceFormats: " + string.Join(", ", unknown));
    }

    /// <summary>The matrix generates a case for every kind, so a generator change cannot drop one in silence.</summary>
    [Fact]
    public void Matrix_CoversEveryKindAndProduct()
    {
        IReadOnlyCollection<IsolationCase> cases = [.. IsolationCases.All.Values];
        Assert.All(Enum.GetValues<ReferenceKind>(), kind => Assert.Contains(cases, item => item.Kind == kind));
        Assert.All(CliCatalog.Current.Products, product => Assert.Contains(cases, item => item.Product == product.Id));
        Assert.NotEmpty(IsolationCases.Sample);
    }
}
