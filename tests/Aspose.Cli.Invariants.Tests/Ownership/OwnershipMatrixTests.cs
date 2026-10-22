using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Invariants.Tests.Ownership;

/// <summary>
/// No command writes over its own input unless asked to edit in place, through any alias of the
/// input, and none removes or replaces a file it did not write (SECURITY.md, Files), for every
/// command that writes (<see cref="OwnershipCases"/>). <see cref="Holds"/> is the unmarked sample
/// every run checks, CI included; <see cref="HoldsExhaustively"/> is the rest of the matrix.
/// </summary>
public sealed class OwnershipMatrixTests
{
    [Theory]
    [MemberData(nameof(OwnershipCases.Sample), MemberType = typeof(OwnershipCases))]
    public void Holds(string id) => OwnershipCases.Verify(id);

    [Theory]
    [Category(TestCategory.Slow)]
    [MemberData(nameof(OwnershipCases.Exhaustive), MemberType = typeof(OwnershipCases))]
    public void HoldsExhaustively(string id) => OwnershipCases.Verify(id);

    /// <summary>The sample names cases the matrix still generates, so a renamed command or check cannot empty it.</summary>
    [Fact]
    public void Sample_IsGenerated() => Assert.Equal(6, OwnershipCases.Sample.Count);
}
