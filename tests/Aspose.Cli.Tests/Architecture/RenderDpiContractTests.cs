using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Aspose.Cli.Tests;

/// <summary>
/// The <c>--dpi</c> range of every product's <c>render</c> is stated once and agrees everywhere
/// an agent reads it: the CLI accepts the range its help and capabilities state and refuses the
/// values just outside it, and the render result schema's <c>dpi</c> has the same bounds. The
/// Cells result schema allowed 24 while the CLI refuses anything below 36.
/// </summary>
public sealed partial class RenderDpiContractTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    [Category(TestCategory.Slow)]
    [Fact]
    public void RenderDpi_AcceptedRangeAgreesWithCapabilitiesAndResultSchema()
    {
        File.WriteAllText(_workspace.File("table.csv"), "a,b\n1,2\n");
        File.WriteAllText(_workspace.File("text.txt"), "Dpi range.\n");
        File.WriteAllText(_workspace.File("outline.md"), "# Dpi range\n");
        Succeeds(_workspace.Run("pdf", "create", "document.pdf", "--from-text", "text.txt", "--output", "json"));
        Succeeds(_workspace.Run("words", "create", "document.docx", "--markdown", "outline.md", "--output", "json"));
        Succeeds(_workspace.Run("slides", "create", "deck.pptx", "--output", "json"));
        var inputs = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["cells"] = "table.csv",
            ["pdf"] = "document.pdf",
            ["words"] = "document.docx",
            ["slides"] = "deck.pptx",
        };

        CliResult capabilities = _workspace.Run("capabilities", "--output", "json");
        Succeeds(capabilities);
        var problems = new List<string>();
        foreach ((string product, string input) in inputs)
        {
            JsonNode option = JsonNode.Parse(capabilities.StdOut)!["products"]!.AsArray()
                .Single(entry => entry!["id"]!.GetValue<string>() == product)!["commands"]!.AsArray()
                .Single(command => command!["path"]!.GetValue<string>() == $"{product} render")!["options"]!.AsArray()
                .Single(entry => entry!["name"]!.GetValue<string>() == "--dpi")!;
            Match stated = StatedRange().Match(option["description"]!.GetValue<string>());
            Assert.True(stated.Success, $"{product} render --dpi states no range: {option["description"]}");
            int minimum = int.Parse(stated.Groups["min"].Value, System.Globalization.CultureInfo.InvariantCulture);
            int maximum = int.Parse(stated.Groups["max"].Value, System.Globalization.CultureInfo.InvariantCulture);
            Assert.True(_workspace.Run(product, "render", "--help").StdOut.Contains($"{minimum}-{maximum}", StringComparison.Ordinal),
                $"{product} render --help does not state the --dpi range {minimum}-{maximum}");

            CliResult lowest = _workspace.Run(product, "render", input, "--out", $"{product}-low.png", "--dpi", Text(minimum), "--output", "json");
            if (lowest.ExitCode != 0)
            {
                problems.Add($"{product} render refuses --dpi {minimum}, the stated minimum: {lowest.StdErr}");
            }
            else if (JsonNode.Parse(lowest.StdOut)!["dpi"]?.GetValue<int>() != minimum)
            {
                problems.Add($"{product} render --dpi {minimum} reports another dpi: {lowest.StdOut}");
            }

            foreach (int outside in new[] { minimum - 1, maximum + 1 })
            {
                CliResult refused = _workspace.Run(product, "render", input, "--out", $"{product}-out.png", "--dpi", Text(outside), "--output", "json");
                if (refused.ExitCode != 2 || JsonNode.Parse(refused.StdErr)?["error"]?["code"]?.GetValue<string>() != "OPTION_INVALID")
                {
                    problems.Add($"{product} render accepts --dpi {outside}, outside the stated {minimum}-{maximum}: exit {refused.ExitCode}");
                }
            }

            JsonObject schema = PublishedSchemas.Document($"v2/{product}/render-result");
            JsonObject? dpi = Dereference(schema, schema["properties"]?["dpi"]);
            if (dpi is null)
            {
                problems.Add($"v2/{product}/render-result declares no dpi");
                continue;
            }

            string bounds = $"minimum {dpi["minimum"]?.ToJsonString() ?? "none"}, maximum {dpi["maximum"]?.ToJsonString() ?? "none"}";
            if (dpi["minimum"]?.GetValue<int>() != minimum || dpi["maximum"]?.GetValue<int>() != maximum)
            {
                problems.Add($"v2/{product}/render-result dpi has {bounds}; the CLI accepts {minimum}-{maximum}");
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    /// <summary>The schema a property states, through a local <c>$ref</c> when it has one.</summary>
    private static JsonObject? Dereference(JsonObject document, JsonNode? node)
    {
        for (int depth = 0; depth < 8 && node is JsonObject schema; depth++)
        {
            if (schema["$ref"] is not JsonValue reference || !reference.TryGetValue(out string? target) || !target.StartsWith("#/", StringComparison.Ordinal))
            {
                return schema;
            }

            node = target[2..].Split('/').Aggregate<string, JsonNode?>(document, static (current, segment) => current?[segment]);
        }

        return null;
    }

    private static string Text(int value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static void Succeeds(CliResult result) => Assert.True(result.ExitCode == 0, result.StdErr);

    [GeneratedRegex(@"\((?<min>\d+)-(?<max>\d+)")]
    private static partial Regex StatedRange();
}
