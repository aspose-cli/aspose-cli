using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Integration;

/// <summary>
/// Decision D8: the SDK owns load results, so a product refuses an input it recognizes but cannot
/// load with the same code as every other product. The decision does not name the code; the
/// platform Skill allows <c>FILE_CORRUPT</c> or <c>FORMAT_MISMATCH</c> for a legacy binary file
/// renamed to another product's extension, so the test requires one of them, the same everywhere.
/// </summary>
public sealed class InputLoadingAlikeTests
{
    /// <summary>A legacy binary document of another product, by the product that is given it.</summary>
    private static readonly (string Product, string Fixture)[] LegacyInputs =
    [
        ("cells", "words.doc"), ("cells", "slides.ppt"),
        ("pdf", "words.doc"), ("pdf", "cells.xls"), ("pdf", "slides.ppt"),
        ("slides", "words.doc"), ("slides", "cells.xls"),
        ("words", "cells.xls"), ("words", "slides.ppt"),
    ];

    [Fact]
    public void ALegacyDocumentOfAnotherProduct_IsRefusedWithOneCodeInEveryProduct()
    {
        using var workspace = new TempWorkspace();
        ScenarioLicense.Project(workspace.Path);
        // Every input has its own file, so the inspections run at the same time.
        string[] codes = new string[LegacyInputs.Length];
        Parallel.For(0, LegacyInputs.Length, index =>
        {
            (string product, string fixture) = LegacyInputs[index];
            string input = $"{fixture.Replace('.', '-')}.{ScenarioFixtures.PrimaryFormat(product)}";
            File.WriteAllBytes(workspace.File(input), ScenarioFixtures.Read(fixture));
            CliResult result = workspace.Run(product, "inspect", input, "--output", "json");
            string code = result.ExitCode == 0
                ? "(opened)"
                : JsonNode.Parse(result.StdErr)?["error"]?["code"]?.GetValue<string>() ?? $"(exit {result.ExitCode})";
            codes[index] = $"{product} inspect {input}: {code}";
        });

        string report = string.Join(Environment.NewLine, codes);
        string[] distinct = [.. codes.Select(static line => line[(line.LastIndexOf(": ", StringComparison.Ordinal) + 2)..]).Distinct(StringComparer.Ordinal)];
        Assert.True(distinct.Length == 1 && distinct[0] is "FILE_CORRUPT" or "FORMAT_MISMATCH",
            "A recognized document that a product cannot load is refused with one code, FILE_CORRUPT or FORMAT_MISMATCH, in every product:"
            + Environment.NewLine + report);
    }
}
