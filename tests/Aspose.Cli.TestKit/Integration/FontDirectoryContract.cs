using System.Text.Json.Nodes;
using Xunit;

namespace Aspose.Cli.TestKit;

/// <summary>
/// Proves through the real CLI that a relative <c>--font-dir</c> makes the
/// fixture family available to the commands that lay out and save documents,
/// and that the family is missing again as soon as the option is omitted.
/// </summary>
public static class FontDirectoryContract
{
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
        JsonNode result = Succeed(workspace.Run(["pdf", "read", pdf, "--kind", "font", "--max-objects", "10000", "--output-format", "json"]));
        return result["objects"]!.AsArray().Any(static font =>
            IsFixture(font!["props"]!["fontName"]!.GetValue<string>()) && font["props"]!["isEmbedded"]!.GetValue<bool>());
    }

    /// <summary>A result on stdout: complete (0), or partial (8).</summary>
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
