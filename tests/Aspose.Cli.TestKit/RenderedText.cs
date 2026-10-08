using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Extensibility.Output;
using Xunit;

namespace Aspose.Cli.TestKit;

/// <summary>
/// Golden human-readable output: what a table renderer writes, with <c>\n</c> line breaks and
/// each trailing space shown as <c>·</c>, so an editor that trims trailing whitespace cannot
/// change a golden without failing it.
/// </summary>
public static partial class RenderedText
{
    /// <summary>Runs <paramref name="render"/> against a surface of <paramref name="format"/>.</summary>
    public static string Of(Action<TableSurface> render, TableFormat format = TableFormat.Plain)
    {
        ArgumentNullException.ThrowIfNull(render);
        using var writer = new StringWriter();
        render(new TableSurface(writer, format));
        string text = writer.ToString().ReplaceLineEndings("\n");
        return TrailingSpaces().Replace(text, static match => new string('·', match.Length));
    }

    /// <summary>
    /// Asserts that <paramref name="actual"/> is <paramref name="expected"/> followed by the final
    /// line break every rendering ends with; <paramref name="expected"/> may use any line breaks.
    /// </summary>
    public static void Equal(string expected, string actual)
    {
        ArgumentNullException.ThrowIfNull(expected);
        Assert.Equal(expected.ReplaceLineEndings("\n") + "\n", actual);
    }

    [GeneratedRegex(" +(?=\n)")]
    private static partial Regex TrailingSpaces();
}
