using System.Text;
using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility.Output;
using Aspose.Cli.Sdk.Text;
using Aspose.Pdf;
using Aspose.Pdf.Text;
using static Aspose.Cli.Product.Pdf.Engine.PdfPages;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>The text search, redact_text and its verification match on a page.</summary>
internal static class PdfTextMatch
{
    /// <summary>
    /// The expression that search, redact_text and its verification match. Literal text also
    /// matches with up to two spaces, never a line break, wherever an East Asian character meets
    /// a character that is not East Asian, such as a digit or Latin letter: the engine reads the gap that automatic spacing leaves there, such as
    /// Word's between Chinese and digits, as a space (known issue PDF-TEXT-GAP-SPACE in
    /// KNOWN-ISSUES.md).
    /// </summary>
    internal static Regex TextPattern(string pattern, bool regex, bool caseSensitive)
    {
        if (regex)
        {
            return SafeRegex.Create(pattern, caseSensitive);
        }

        var expression = new StringBuilder();
        Rune? previous = null;
        foreach (Rune rune in pattern.EnumerateRunes())
        {
            if (previous is { } before && !Rune.IsWhiteSpace(before) && !Rune.IsWhiteSpace(rune)
                && IsEastAsian(before) != IsEastAsian(rune))
            {
                expression.Append(@"[^\S\r\n]{0,2}");
            }

            expression.Append(Regex.Escape(rune.ToString()));
            previous = rune;
        }

        return SafeRegex.Create(expression.ToString(), caseSensitive);

        // East Asian Wide and Fullwidth characters: ideographs, kana, Hangul and fullwidth forms.
        static bool IsEastAsian(Rune rune) => TextWidth.Of(rune) == 2;
    }

    internal static TextFragmentCollection MatchText(
        Page page, string pattern, bool regex, bool caseSensitive, Func<string, Exception> invalidPattern)
    {
        Regex expression = TextPattern(pattern, regex, caseSensitive);
        try
        {
            // The SDK overflows on contextual zero-width matches. Reject them before
            // handing it the original expression, whose timeout and context it preserves.
            if (regex && expression.Matches(ExtractText(page, PdfReadContents.Text))
                .Any(static match => match.Length == 0))
            {
                throw invalidPattern("the regular expression must not match an empty string");
            }
            var absorber = new TextFragmentAbsorber(expression, new TextSearchOptions(true));
            page.Accept(absorber);
            return absorber.TextFragments;
        }
        catch (RegexMatchTimeoutException exception)
        {
            throw CliErrors.RegexTimeout(exception);
        }
    }
}
