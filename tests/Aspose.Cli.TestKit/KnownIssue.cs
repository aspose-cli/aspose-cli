using Xunit;

namespace Aspose.Cli.TestKit;

/// <summary>
/// The reproduction of an SDK defect recorded in KNOWN-ISSUES.md. The test that calls
/// <see cref="Reproduces"/> passes while the pinned SDK still has the defect, and fails once an
/// SDK update fixes it, so the issue, its handling and its test are deleted together.
/// </summary>
public static class KnownIssue
{
    public static void Reproduces(string id, bool reproduced, string observation) =>
        Assert.True(
            reproduced,
            $"The SDK no longer reproduces known issue {id} ({observation}). Delete its section in "
            + $"KNOWN-ISSUES.md, the code that names {id}, and this test.");
}
