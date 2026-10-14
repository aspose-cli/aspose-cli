using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Invariants.Tests;

/// <summary>
/// The committed list of every generated invariant case id, so a rule, branch or generator that
/// stops producing cases fails instead of checking less in silence. A case that is no longer
/// generated is removed from the list, which, like a new known violation, takes the owner's
/// <c>quality-exception</c> label (the pull request check compares the list with its base). A new
/// case is added by rerunning with <c>ASPOSE_CLI_TEST_UPDATE_SNAPSHOTS=1</c>.
/// </summary>
public sealed class GeneratedCaseListTests
{
    /// <summary>The list, one id per line in ordinal order.</summary>
    public const string FileName = "generated-cases.txt";

    private const string UpdateVariable = Aspose.Cli.Sdk.DistributionInfo.EnvironmentVariablePrefix + "TEST_UPDATE_SNAPSHOTS";

    private static string ListPath => Path.Combine(RepositoryPaths.Root, "tests", "Aspose.Cli.Invariants.Tests", FileName);

    [Fact]
    public void GeneratedCases_MatchTheCommittedList()
    {
        string[] generated = [.. InvariantCases.All.Keys.Order(StringComparer.Ordinal)];
        if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
        {
            File.WriteAllText(ListPath, string.Join('\n', generated) + "\n");
            return;
        }

        HashSet<string> listed = [.. File.ReadAllText(ListPath).ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries)];
        string[] vanished = [.. listed.Where(id => !InvariantCases.All.ContainsKey(id)).Order(StringComparer.Ordinal)];
        string[] added = [.. generated.Where(id => !listed.Contains(id))];
        Assert.True(vanished.Length == 0,
            $"{vanished.Length} listed cases are no longer generated, so their checks stopped running. Restore what generated them; "
            + $"only if dropping them is intended, remove them from {FileName}, which takes the owner's 'quality-exception' label:"
            + Environment.NewLine + string.Join(Environment.NewLine, vanished));
        Assert.True(added.Length == 0,
            $"{added.Length} generated cases are not in {FileName}; rerun with {UpdateVariable}=1 to list them:"
            + Environment.NewLine + string.Join(Environment.NewLine, added));
    }
}
