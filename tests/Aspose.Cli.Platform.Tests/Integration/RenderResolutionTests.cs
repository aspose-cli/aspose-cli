using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>
/// A pixel size belongs to a raster image. Every product's <c>render</c> refuses an explicit
/// <c>--dpi</c>, or a pixel-size option such as Slides' <c>--width</c>, for a vector output
/// such as svg with <c>OPTION_INVALID</c> before it writes anything, rather than ignoring it,
/// and keeps honoring it for png.
/// </summary>
public sealed class RenderResolutionTests
{
    private static readonly string[] Products = ["cells", "pdf", "slides", "words"];

    public static TheoryData<string, string, string> VectorPixelSizes { get; } = new()
    {
        { "cells", "--dpi", "150" },
        { "pdf", "--dpi", "150" },
        { "slides", "--dpi", "150" },
        { "words", "--dpi", "150" },
        { "slides", "--width", "640" },
    };

    [Theory]
    [MemberData(nameof(VectorPixelSizes))]
    public void Render_RefusesAPixelSizeForSvg(string product, string option, string value)
    {
        using TempWorkspace workspace = Workspace(product, out string input);

        CliResult refused = workspace.Run(
            product, "render", input, "--to", "svg", option, value, "--out", "page.svg", "--output-format", "json");

        string call = $"{product} render --to svg {option} {value}";
        Assert.True(refused.ExitCode == 2, $"{call} exits 2 for an invalid option; it exited {refused.ExitCode}: {refused.StdErr}{refused.StdOut}");
        JsonNode error = JsonNode.Parse(refused.StdErr)!["error"]!;
        Assert.Equal("OPTION_INVALID", error["code"]!.GetValue<string>());
        Assert.Equal(option, error["details"]!["option"]!.GetValue<string>());
        Assert.Contains("svg", error["message"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"Drop {option}", error["hint"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.True(
            !Directory.EnumerateFiles(workspace.Path, "page*").Any(),
            $"{call} writes nothing: [{string.Join(", ", Directory.EnumerateFiles(workspace.Path).Select(Path.GetFileName))}]");
    }

    [Fact]
    public void Render_HonorsDpiForPng()
    {
        var runs = new (string Product, CliResult Result, string Output)[Products.Length];
        var workspaces = new TempWorkspace[Products.Length];
        try
        {
            Parallel.For(0, Products.Length, index =>
            {
                string product = Products[index];
                workspaces[index] = Workspace(product, out string input);
                string[] first = product switch { "slides" => ["--slides", "1"], "cells" => ["--sheets", "1"], _ => ["--pages", "1"] };
                runs[index] = (product, workspaces[index].Run(
                    [product, "render", input, .. first, "--to", "png", "--dpi", "150", "--out", "page.png", "--output-format", "json"]),
                    workspaces[index].File("page.png"));
            });

            foreach ((string product, CliResult result, string output) in runs)
            {
                Assert.True(result.ExitCode == 0, $"{product} render --to png --dpi 150: {result.StdErr}{result.StdOut}");
                Assert.Equal(150, JsonNode.Parse(result.StdOut)!["dpi"]!.GetValue<int>());
                Assert.True(File.Exists(output), $"{product} render --to png --dpi 150 writes page.png.");
            }
        }
        finally
        {
            foreach (TempWorkspace? workspace in workspaces)
            {
                workspace?.Dispose();
            }
        }
    }

    private static TempWorkspace Workspace(string product, out string input)
    {
        var workspace = new TempWorkspace();
        ScenarioLicense.Project(workspace.Path);
        string format = ScenarioFixtures.PrimaryFormat(product);
        input = $"input.{format}";
        File.WriteAllBytes(workspace.File(input), ScenarioFixtures.Read($"{product}.{format}"));
        return workspace;
    }
}
