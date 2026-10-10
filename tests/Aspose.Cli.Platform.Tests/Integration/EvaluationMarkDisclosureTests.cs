using System.Text.Json.Nodes;
using Aspose.Cli.Architecture.Tests;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Integration;

/// <summary>
/// Decision D6: with a license, an input that an earlier evaluation-mode save marked (a watermark,
/// a banner, an evaluation sheet) is disclosed the same way by every product. The SDK pipeline
/// compares the evaluation marks of the input and of the staged output, so the disclosure is one
/// SDK warning code, whatever the product. The test does not name the code: it is what a licensed
/// edit of the marked document warns beyond the same edit of the clean document.
/// </summary>
public sealed class EvaluationMarkDisclosureTests
{
    private const string EvaluationModeWarning = "EVAL_MODE";

    [LicensedFact]
    public async Task EveryProduct_DisclosesAnEvaluationMarkedInputWithTheSameSdkCode()
    {
        // The products work in separate workspaces, so they run at the same time.
        Task<string[]>[] runs = [.. ScenarioFixtures.Products.Select(static product => Task.Run(() => Disclosure(product)))];
        string[][] found = await Task.WhenAll(runs);
        var disclosures = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach ((string product, string[] codes) in ScenarioFixtures.Products.Zip(found))
        {
            disclosures[product] = codes;
        }

        string report = string.Join(Environment.NewLine, disclosures.Select(static pair => $"  {pair.Key}: [{string.Join(", ", pair.Value)}]"));
        Assert.True(disclosures.Values.All(static codes => codes.Length > 0),
            "Every product discloses that its licensed output keeps the evaluation marks of its input; the warnings added by a marked input:"
            + Environment.NewLine + report);
        Assert.True(disclosures.Values.Select(static codes => string.Join(",", codes)).Distinct(StringComparer.Ordinal).Count() == 1,
            "Every product discloses an evaluation-marked input with the same codes:" + Environment.NewLine + report);
        HashSet<string> sdkLiterals = [.. OwnershipSourceFiles.Sdk.SelectMany(static file => file.Root.DescendantTokens()
            .Where(static token => token.IsKind(SyntaxKind.StringLiteralToken))
            .Select(static token => token.ValueText))];
        string[] foreign = [.. disclosures.Values.SelectMany(static codes => codes).Distinct(StringComparer.Ordinal).Where(code => !sdkLiterals.Contains(code))];
        Assert.True(foreign.Length == 0, "The disclosure is an SDK warning code, not a product's: " + string.Join(", ", foreign));
    }

    /// <summary>The warning codes a licensed edit of the product's evaluation-marked document adds to the same edit of its clean document.</summary>
    private static string[] Disclosure(string product)
    {
        using var workspace = new TempWorkspace();
        ScenarioLicense.Project(workspace.Path);
        string extension = ScenarioFixtures.PrimaryFormat(product);
        File.WriteAllBytes(workspace.File("clean." + extension), ScenarioFixtures.Read($"{product}.{extension}"));
        File.WriteAllBytes(workspace.File("ops.json"), ScenarioFixtures.Read($"{product}.ops"));

        // The licensed edit of the clean document reads only the clean input, so it runs at the
        // same time as the evaluation-mode edit that makes the marked document.
        Task<JsonNode> markedRun = Task.Run(() => Edit(workspace, product, "clean." + extension, "marked." + extension, "--license-mode", "evaluation"));
        Task<JsonNode> fromCleanRun = Task.Run(() => Edit(workspace, product, "clean." + extension, "licensed-clean." + extension));
        Task.WaitAll(markedRun, fromCleanRun);
        JsonNode marked = markedRun.Result;
        JsonNode fromClean = fromCleanRun.Result;
        Assert.Contains(EvaluationModeWarning, Codes(marked));

        JsonNode fromMarked = Edit(workspace, product, "marked." + extension, "licensed-marked." + extension);
        Assert.Equal("licensed", fromMarked["license"]?["mode"]?.GetValue<string>());
        Assert.DoesNotContain(EvaluationModeWarning, Codes(fromMarked));
        return [.. Codes(fromMarked).Except(Codes(fromClean), StringComparer.Ordinal).Order(StringComparer.Ordinal)];
    }

    private static JsonNode Edit(TempWorkspace workspace, string product, string input, string output, params string[] extra) =>
        workspace.Run([product, "edit", input, "--ops", "ops.json", "--out", output, "--output", "json", .. extra]).Json();

    private static string[] Codes(JsonNode result) =>
        [.. (result["warnings"] as JsonArray ?? []).Select(static warning => warning?["code"]?.GetValue<string>()).OfType<string>()];
}
