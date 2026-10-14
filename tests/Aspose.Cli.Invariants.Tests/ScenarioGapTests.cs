using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.Invariants.Tests;

/// <summary>
/// The scenario and secret checks catch what they exist for: each test runs a generated case or a
/// scenario that breaks its expectation in a way the check once let through.
/// </summary>
public sealed class ScenarioGapTests
{
    /// <summary>
    /// An operation secret case accepts only what its operation may do on an unprotected fixture:
    /// one that sets a password succeeds, and one that removes a protection succeeds or refuses
    /// the password. Without its input either edit fails with <c>FILE_NOT_FOUND</c>, which must
    /// be reported, although it shares the password refusal's exit code.
    /// </summary>
    [Theory]
    [InlineData("secret-hidden | pdf edit encrypt_document", ScenarioRunner.ExitCodeCheck, "exit 3 FILE_NOT_FOUND")]
    [InlineData("secret-hidden | words edit unprotect_document", ScenarioRunner.ErrorCheck, "FILE_NOT_FOUND")]
    public void OperationSecretCase_RefusesAFailureOtherThanThePasswords(string id, string check, string start)
    {
        Scenario scenario = InvariantCases.All[id].Scenario!;
        Scenario broken = scenario with
        {
            Files = scenario.Files.Where(static file => !file.Key.StartsWith("input.", StringComparison.Ordinal)).ToDictionary(),
        };

        ScenarioOutcome outcome = ScenarioRunner.Run(broken);

        Assert.Contains(outcome.Problems, problem => problem.Check == check && problem.Message.StartsWith(start, StringComparison.Ordinal));
    }

    /// <summary>The verbs that remove a protection are derived from the catalog's own verb pairs.</summary>
    [Fact]
    public void RemovalVerbs_AreTheVerbsThatUndoAnother()
    {
        HashSet<string> removals = InvariantCases.RemovalVerbs(CliCatalog.Current);

        Assert.Contains("unprotect", removals);
        Assert.DoesNotContain("protect", removals);
        Assert.DoesNotContain("encrypt", removals);
        Assert.DoesNotContain("delete", removals);
    }
}
