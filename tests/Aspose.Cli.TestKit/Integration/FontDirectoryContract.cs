using System.Text.Json.Nodes;
using Xunit;

namespace Aspose.Cli.TestKit;

/// <summary>
/// Proves through the real CLI that a relative <c>--font-dir</c> makes the
/// fixture family available to <c>fonts check</c>, <c>review</c> and the
/// commands that lay out and save documents, and that the family is missing
/// again as soon as the option is omitted.
/// </summary>
public static class FontDirectoryContract
{
    /// <summary>The end of the shared review check's code, which begins with the product's prefix.</summary>
    private const string MissingFontsFinding = "_FONTS_MISSING_OR_SUBSTITUTED";

    /// <param name="workspace">Workspace whose working directory holds <paramref name="fileName"/>.</param>
    /// <param name="fileName">Document that uses <see cref="FontFixtures.UniqueFamily"/>.</param>
    public static void Verify(TempWorkspace workspace, string fileName)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        FontFixtures.WriteUniqueFont(workspace.File("fonts"));

        Assert.False(FixtureAvailable(workspace, ["fonts", "check", fileName]));
        Assert.True(FixtureAvailable(workspace, ["fonts", "check", fileName, "--font-dir", "fonts"]));
        Assert.False(FixtureAvailable(workspace, ["fonts", "check", fileName]));

        Assert.Contains(ReviewFindings(workspace, fileName, "ambient.review"), MissingFonts);
        Assert.DoesNotContain(
            ReviewFindings(workspace, fileName, "fonts.review", "--font-dir", "fonts"),
            MissingFonts);
    }

    /// <summary>
    /// Proves through the real CLI that a command lays out and saves its PDF output with
    /// the fixture family from a relative <c>--font-dir</c>, and substitutes another font
    /// when the option is omitted.
    /// </summary>
    /// <param name="workspace">Workspace whose working directory holds the command's input.</param>
    /// <param name="label">Prefix of the two output names.</param>
    /// <param name="command">The command writing the PDF named by its argument.</param>
    public static void VerifyPdfOutput(TempWorkspace workspace, string label, Func<string, string[]> command)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(command);
        FontFixtures.WriteUniqueFont(workspace.File("fonts"));
        string ambient = label + "-ambient.pdf";
        string fonts = label + "-fonts.pdf";

        Succeed(workspace.Run([.. command(ambient), "--output-format", "json"]));
        Succeed(workspace.Run([.. command(fonts), "--font-dir", "fonts", "--output-format", "json"]));

        Assert.False(EmbedsFixture(workspace, ambient));
        Assert.True(EmbedsFixture(workspace, fonts));
    }

    /// <summary>Whether a PDF carries the fixture family embedded, so it renders as itself anywhere.</summary>
    public static bool EmbedsFixture(TempWorkspace workspace, string pdf)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        JsonNode result = Succeed(workspace.Run(["fonts", "check", pdf, "--output-format", "json"]));
        return result["fonts"]!.AsArray().Any(static font =>
            IsFixture(font!["name"]!.GetValue<string>()) && font["available"]!.GetValue<bool>());
    }

    private static bool FixtureAvailable(TempWorkspace workspace, string[] args)
    {
        JsonNode result = Succeed(workspace.Run([.. args, "--output-format", "json"]));
        JsonNode font = Assert.Single(
            result["fonts"]!.AsArray(),
            font => IsFixture(font!["name"]!.GetValue<string>()))!;
        return font["available"]!.GetValue<bool>();
    }

    private static bool MissingFonts(string code) => code.EndsWith(MissingFontsFinding, StringComparison.Ordinal);

    private static string[] ReviewFindings(
        TempWorkspace workspace,
        string fileName,
        string output,
        params string[] extra)
    {
        JsonNode result = Succeed(workspace.Run(
            ["review", fileName, "--out", output, "--output-format", "json", .. extra]));
        return result["findings"]!.AsArray()
            .Select(static finding => finding!["code"]!.GetValue<string>())
            .ToArray();
    }

    /// <summary>A result on stdout: complete (0), or partial (8) when review reports an error finding or a checked font is missing.</summary>
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
