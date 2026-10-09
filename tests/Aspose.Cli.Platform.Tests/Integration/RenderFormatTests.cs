using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>
/// Every product's <c>render</c> takes its image format from <c>--to</c>, else from the
/// <c>--out</c> extension, else png. So <c>--to</c> has no parser default: help and capabilities
/// must not claim one (a declared <c>png</c> default reads as if <c>--out page.jpg</c> wrote png
/// or contradicted it).
/// </summary>
public sealed class RenderFormatTests
{
    private static readonly string[] Products = ["cells", "pdf", "slides", "words"];

    [Fact]
    public void RenderTo_DeclaresNoDefault()
    {
        string[] declared =
        [
            .. CliCatalog.Current.Document["commands"]!.AsArray()
                .Where(static command => command!["path"]!.GetValue<string>().EndsWith(" render", StringComparison.Ordinal))
                .SelectMany(static command => command!["options"]!.AsArray()
                    .Where(static option => option!["name"]!.GetValue<string>() == "--to"
                        && option["hasDefault"]?.GetValue<bool>() == true)
                    .Select(option => $"{command["path"]!.GetValue<string>()} --to (default {option!["default"]})")),
        ];

        Assert.True(declared.Length == 0,
            "render --to takes the --out extension's format, else png, so capabilities declares no default for it: "
            + string.Join(" | ", declared));
    }

    [Fact]
    public void RenderHelp_ClaimsNoParserDefaultForTo()
    {
        using var workspace = new TempWorkspace();
        foreach (string product in Products)
        {
            CliResult help = workspace.Run(product, "render", "--help");
            Assert.True(help.ExitCode == 0, $"{product} render --help: {help.StdErr}");
            string line = help.StdOut.ReplaceLineEndings("\n").Split('\n')
                .Single(static line => line.TrimStart().StartsWith("--to ", StringComparison.Ordinal));
            Assert.True(!line.Contains("[default:", StringComparison.Ordinal),
                $"{product} render --help shows a parser default for --to, though the --out extension decides first: {line.Trim()}");
        }
    }

    [LicensedFact]
    public void Render_TakesTheFormatFromTheOutExtensionElsePng()
    {
        foreach (string product in Products)
        {
            using var workspace = new TempWorkspace();
            ScenarioLicense.Project(workspace.Path);
            string format = ScenarioFixtures.PrimaryFormat(product);
            string input = $"input.{format}";
            File.WriteAllBytes(workspace.File(input), ScenarioFixtures.Read($"{product}.{format}"));

            foreach ((string? output, string expected) in new (string?, string)[] { ("page.jpg", "jpeg"), ("page.svg", "svg"), (null, "png") })
            {
                string[] arguments = output is null
                    ? [product, "render", input, "--output", "json"]
                    : [product, "render", input, "--out", output, "--output", "json"];
                CliResult result = workspace.Run(arguments);
                Assert.True(result.ExitCode == 0, $"{string.Join(' ', arguments)}: {result.StdErr}{result.StdOut}");
                JsonNode root = JsonNode.Parse(result.StdOut)!;
                string[] written = [.. OutputFormats(root, root["input"]).Distinct(StringComparer.Ordinal)];
                Assert.True(written is [var only] && only == expected,
                    $"{string.Join(' ', arguments)} writes {expected} without --to; it wrote [{string.Join(", ", written)}].");
            }
            Assert.True(File.Exists(workspace.File("input.png")),
                $"{product} render without --out or --to writes input.png beside the input.");
        }
    }

    /// <summary>The <c>format</c> of every object with a <c>path</c> in the result, the input's left out.</summary>
    private static IEnumerable<string> OutputFormats(JsonNode? node, JsonNode? input)
    {
        switch (node)
        {
            case JsonObject value when !ReferenceEquals(value, input):
                if (value["path"] is not null && value["format"] is JsonValue format)
                {
                    yield return format.GetValue<string>();
                }
                foreach (KeyValuePair<string, JsonNode?> property in value)
                {
                    foreach (string nested in OutputFormats(property.Value, input))
                    {
                        yield return nested;
                    }
                }
                break;
            case JsonArray items:
                foreach (JsonNode? item in items)
                {
                    foreach (string nested in OutputFormats(item, input))
                    {
                        yield return nested;
                    }
                }
                break;
        }
    }
}
