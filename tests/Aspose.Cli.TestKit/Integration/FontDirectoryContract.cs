using System.Text.Json.Nodes;
using Xunit;

namespace Aspose.Cli.TestKit;

/// <summary>
/// Proves through the real CLI that a relative <c>--font-dir</c> makes the
/// fixture family available to <c>fonts check</c> and <c>review</c>, and that
/// the family is missing again as soon as the option is omitted.
/// </summary>
public static class FontDirectoryContract
{
    private const string MissingFontsFinding = "FONTS_MISSING_OR_SUBSTITUTED";

    /// <param name="workspace">Workspace whose working directory holds <paramref name="fileName"/>.</param>
    /// <param name="fileName">Document that uses <see cref="FontFixtures.UniqueFamily"/>.</param>
    public static void Verify(TempWorkspace workspace, string fileName)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        FontFixtures.WriteUniqueFont(workspace.File("fonts"));

        Assert.False(FixtureAvailable(workspace, ["fonts", "check", fileName]));
        Assert.True(FixtureAvailable(workspace, ["fonts", "check", fileName, "--font-dir", "fonts"]));
        Assert.False(FixtureAvailable(workspace, ["fonts", "check", fileName]));

        Assert.Contains(MissingFontsFinding, ReviewFindings(workspace, fileName, "ambient.review"));
        Assert.DoesNotContain(
            MissingFontsFinding,
            ReviewFindings(workspace, fileName, "fonts.review", "--font-dir", "fonts"));
    }

    private static bool FixtureAvailable(TempWorkspace workspace, string[] args)
    {
        JsonNode result = Succeed(workspace.Run([.. args, "--output", "json"]));
        JsonNode font = Assert.Single(
            result["fonts"]!.AsArray(),
            font => IsFixture(font!["name"]!.GetValue<string>()))!;
        return font["available"]!.GetValue<bool>();
    }

    private static string[] ReviewFindings(
        TempWorkspace workspace,
        string fileName,
        string output,
        params string[] extra)
    {
        JsonNode result = Succeed(workspace.Run(
            ["review", fileName, "--out", output, "--output", "json", .. extra]));
        return result["findings"]!.AsArray()
            .Select(static finding => finding!["code"]!.GetValue<string>())
            .ToArray();
    }

    /// <summary>A result on stdout: complete (0), or partial (8) when review reports an error finding.</summary>
    private static JsonNode Succeed(CliResult result)
    {
        Assert.True(result.ExitCode is 0 or 8, $"exit {result.ExitCode}: {result.StdErr}");
        return JsonNode.Parse(result.StdOut)!;
    }

    private static bool IsFixture(string name) =>
        name.Replace(" ", string.Empty, StringComparison.Ordinal).StartsWith(
            FontFixtures.UniqueFamily.Replace(" ", string.Empty, StringComparison.Ordinal),
            StringComparison.OrdinalIgnoreCase);
}
