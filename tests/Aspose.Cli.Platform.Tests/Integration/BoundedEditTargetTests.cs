using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.Architecture.Tests;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>
/// The <c>targets</c> of an edit outcome are bounded once, by the SDK, for every product: an
/// operation that changed at most 100 parts lists each of them, and one that changed more lists
/// the product's degenerate form instead, one or a few document-level or range addresses (such as
/// <c>pdf</c>, <c>presentation</c>, <c>document</c> or <c>blocks/1-101</c>), never a truncated
/// prefix of the parts. PDF pages, slides and Word blocks are the parts a
/// single operation can change by the hundred; a Cells target is one range address, so Cells has
/// no case here.
/// </summary>
public sealed partial class BoundedEditTargetTests
{
    private const int Bound = 100;

    [LicensedFact]
    public void EveryProduct_BoundsItsTargetsTheSameWay()
    {
        foreach ((string product, Func<TempWorkspace, (string Input, JsonArray Ops)> prepare) in Cases())
        {
            using var workspace = new TempWorkspace();
            ScenarioLicense.Project(workspace.Path);
            (string input, JsonArray ops) = prepare(workspace);
            File.WriteAllText(workspace.File("ops.json"), new JsonObject { ["ops"] = ops }.ToJsonString());
            CliResult result = workspace.Run(product, "edit", input, "--ops", "ops.json", "--dry-run", "--output", "json");
            Assert.True(result.ExitCode == 0, $"{product} edit: {result.StdErr}");
            JsonArray applied = JsonNode.Parse(result.StdOut)!["applied"]!.AsArray();
            JsonNode atBound = applied[^2]!;
            JsonNode beyond = applied[^1]!;

            Assert.True(atBound["itemsAffected"]!.GetValue<long>() == Bound,
                $"{product}: the setup changes {Bound} parts in one operation; it changed {atBound["itemsAffected"]}.");
            Assert.True(beyond["itemsAffected"]!.GetValue<long>() > Bound,
                $"{product}: the setup changes more than {Bound} parts in one operation; it changed {beyond["itemsAffected"]}.");
            string[] listed = [.. atBound["targets"]!.AsArray().Select(static target => target!.GetValue<string>())];
            Assert.True(listed.Length == Bound && listed.Distinct(StringComparer.Ordinal).Count() == Bound,
                $"{product}: an operation that changed {Bound} parts lists each of them; it lists {listed.Length}.");
            string[] targets = [.. beyond["targets"]!.AsArray().Select(static target => target!.GetValue<string>())];
            Assert.True(targets.Length <= Bound, $"{product}: an outcome lists at most {Bound} targets; it lists {targets.Length}.");
            string[] parts = [.. targets.Where(listed.Contains)];
            Assert.True(targets.Length is > 0 and <= DegenerateMaximum && parts.Length == 0,
                $"{product}: an operation that changed more than {Bound} parts lists the product's degenerate form, one or a few "
                + $"document-level or range addresses, not a prefix of its parts; it lists {targets.Length}: [{string.Join(", ", targets.Take(5))}]");
        }
    }

    /// <summary>The most addresses a degenerate form holds: a document, or a few ranges and stories.</summary>
    private const int DegenerateMaximum = 10;

    [Fact]
    public void Products_LeaveTheBoundToTheSdk()
    {
        IEnumerable<string> bounds = OwnershipSourceFiles.Products.SelectMany(static file => file.Root.DescendantNodes()
            .OfType<ExpressionSyntax>()
            .Where(static expression => expression is BinaryExpressionSyntax or IsPatternExpressionSyntax
                && expression.Parent is not (BinaryExpressionSyntax or IsPatternExpressionSyntax)
                && CountAgainstBound().IsMatch(expression.ToString()))
            .Select(expression => file.At(expression, expression.ToString())));

        OwnershipSourceFiles.AssertNone(bounds,
            $"A product hands its edit targets to the SDK whole; only the SDK bounds them at {Bound}. These product lines compare a count with {Bound}:");
    }

    /// <summary>Per product: a document and two operations, the first changing exactly 100 parts and the second more.</summary>
    private static IEnumerable<(string Product, Func<TempWorkspace, (string Input, JsonArray Ops)> Prepare)> Cases()
    {
        yield return ("pdf", static workspace =>
        {
            File.WriteAllBytes(workspace.File("input.pdf"), ScenarioFixtures.Read("pdf.pdf"));
            var ops = new JsonArray();
            for (int page = 0; page < Bound; page++)
            {
                ops.Add(new JsonObject { ["op"] = "insert_blank_page", ["at"] = 1 });
            }
            ops.Add(new JsonObject { ["op"] = "rotate_pages", ["pages"] = $"1-{Bound}", ["angle"] = 90 });
            ops.Add(new JsonObject { ["op"] = "rotate_pages", ["pages"] = "1-", ["angle"] = 180 });
            return ("input.pdf", ops);
        });
        yield return ("slides", workspace => (Create(workspace, "slides", "input.pptx", "--from-markdown",
            string.Join("\n\n", Enumerable.Range(1, Bound + 1).Select(static slide =>
                $"# Slide {slide}\n\n- Hello {slide}{(slide <= Bound ? " Mark" : string.Empty)}"))), Replacements()));
        yield return ("words", workspace => (Create(workspace, "words", "input.docx", "--markdown",
            string.Join("\n\n", Enumerable.Range(1, Bound + 1).Select(static block =>
                $"Hello {block}{(block <= Bound ? " Mark" : string.Empty)}"))), Replacements()));
    }

    /// <summary>Replaces the word only the first 100 parts hold, then the word every part holds.</summary>
    private static JsonArray Replacements() =>
    [
        new JsonObject { ["op"] = "replace_text", ["find"] = "Mark", ["replace"] = "Sign" },
        new JsonObject { ["op"] = "replace_text", ["find"] = "Hello", ["replace"] = "Hi" },
    ];

    private static string Create(TempWorkspace workspace, string product, string input, string option, string markdown)
    {
        File.WriteAllText(workspace.File("source.md"), markdown);
        CliResult created = workspace.Run(product, "create", input, option, "source.md", "--output", "json");
        Assert.True(created.ExitCode == 0, $"{product} create: {created.StdErr}");
        return input;
    }

    /// <summary>A count or length compared with 100, such as <c>pages.Count is &gt; 0 and &lt;= 100</c>.</summary>
    [GeneratedRegex(@"\.(Count|Length)\b.*[<>]=?\s*100\b")]
    private static partial Regex CountAgainstBound();
}
