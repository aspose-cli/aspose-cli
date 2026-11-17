using System.Text;
using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO.Paths;
using Aspose.Pdf;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>
/// The Markdown of a document as one file. The export writes each picture it links as a file
/// beside its output, which is published alone: it exports into a directory of its own, and each
/// link to a file it wrote there is taken out of the text.
/// </summary>
internal static class PdfMarkdownText
{
    private const string Exported = "document.md";

    /// <summary>
    /// Writes the Markdown of <paramref name="document"/> to <paramref name="path"/> and returns how
    /// many picture links it took out.
    /// </summary>
    internal static int Save(Document document, string path)
    {
        string directory = UserStorage.CreateTemporaryDirectory("pdf-markdown");
        try
        {
            string exported = Path.Combine(directory, Exported);
            document.Save(exported, SaveFormat.Markdown);
            byte[] bytes = File.ReadAllBytes(exported);
            string[] pictures = [.. Directory.EnumerateFileSystemEntries(directory).Select(Path.GetFileName).OfType<string>().Where(static name => name != Exported)];
            if (pictures.Length == 0)
            {
                File.WriteAllBytes(path, bytes);
                return 0;
            }

            bool marked = bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble);
            (string text, int removed) = TakeOutPictures(Encoding.UTF8.GetString(bytes.AsSpan(marked ? Encoding.UTF8.Preamble.Length : 0)), pictures);
            File.WriteAllText(path, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: marked));
            return removed;
        }
        finally
        {
            UserStorage.TryDeleteTree(directory);
        }
    }

    /// <summary>
    /// Takes each link <c>![](name)</c> to one of <paramref name="pictures"/> out of the text, with
    /// a line that holds only such links and the blank line after it, or before it at the end of
    /// the text, so the text reads line for line as the export writes it without them. A picture without a link, or a reference to one left in the text,
    /// is one the conversion cannot take out: nothing is written.
    /// </summary>
    internal static (string Text, int Removed) TakeOutPictures(string markdown, IReadOnlyList<string> pictures)
    {
        string links = string.Join("|", pictures.Select(static name => Regex.Escape($"![]({name})")));
        int removed = 0;
        foreach (string name in pictures)
        {
            int count = Regex.Count(markdown, Regex.Escape($"![]({name})"));
            removed += count > 0 ? count : throw Unlinked(name);
        }

        // Lines of links that end the text go with the blank lines before them; any other with the blank line after it.
        string line = $@"[ \t]*(?:(?:{links})[ \t]*)+";
        string text = Regex.Replace(markdown, $@"(\r?\n)(?:[ \t]*\r?\n)*(?:{line}(?:\r?\n|\z)(?:[ \t]*\r?\n)*)+\z", "$1");
        text = Regex.Replace(text, $@"(?m)^{line}\r?\n(?:[ \t]*\r?\n)?", string.Empty);
        text = Regex.Replace(text, links, string.Empty);
        return pictures.FirstOrDefault(name => Regex.IsMatch(text, $"""[("']{Regex.Escape(name)}[)"' ]""")) is { } left
            ? throw Unlinked(left)
            : (text, removed);
    }

    private static CliException Unlinked(string picture) => CliErrors.ContentUnsupported(
        $"The md output cannot hold a picture of the converted pages: its export wrote '{picture}' beside it and links it in a way the conversion does not take out, so nothing was written.");
}
