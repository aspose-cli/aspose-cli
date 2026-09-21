using System.Globalization;
using System.Text.RegularExpressions;
using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Product.Pdf.Engine.Mapping;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.Sdk.Text;
using Aspose.Pdf;
using Aspose.Pdf.Annotations;
using Aspose.Pdf.Forms;
using Aspose.Pdf.Optimization;
using Aspose.Pdf.Text;
using static Aspose.Cli.Product.Pdf.Engine.PdfArtifactSupport;
using static Aspose.Cli.Product.Pdf.Engine.PdfEngineSupport;
using PdfColor = Aspose.Pdf.Color;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>Provides stateless helpers shared by PDF mutation services.</summary>
internal static class PdfMutationSupport
{
    internal static IReadOnlyList<string> MatchPhrases(
        Page page,
        string pattern,
        bool regex,
        bool caseSensitive)
    {
        if (!regex)
        {
            return [pattern];
        }

        try
        {
            MatchCollection matches = SafeRegex.Create(pattern, caseSensitive)
                .Matches(ExtractText(page, PdfReadModes.Plain));
            string[] phrases = matches.Cast<Match>()
                .Select(static match => match.Value)
                .Where(static value => value.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (matches.Count > 0 && phrases.Length == 0)
            {
                throw new InvalidOperationException("A redaction or search regex must not match an empty string.");
            }

            return phrases;
        }
        catch (RegexMatchTimeoutException exception)
        {
            throw new CliException(
                ErrorCodes.OperationTimeout,
                "The PDF regular expression exceeded its one-second execution budget.",
                hint: "Simplify the expression or search a narrower page range.",
                innerException: exception);
        }
    }

    internal static IReadOnlyList<int> Resolve(Document document, string text) =>
        Aspose.Cli.Sdk.Addressing.PageRange.Parse(text).Resolve(document.Pages.Count);

    internal static IReadOnlyList<int> ResolveOptional(Document document, string? text) =>
        text is null ? Enumerable.Range(1, document.Pages.Count).ToArray() : Resolve(document, text);

    internal static Page PageAt(Document document, int page) =>
        page > 0 && page <= document.Pages.Count
            ? document.Pages[page]
            : throw PageNotFound(page, document.Pages.Count);

    internal static CliException PageNotFound(int requested, int available) => new(
        ErrorCodes.PageNotFound,
        $"Requested page {requested} exceeds the available count of {available}.",
        hint: $"Use a page from 1 through {available}.");

    internal static Rectangle ToPdfRect(Page page, PdfRectInput rect)
    {
        if (rect.X < 0 || rect.Y < 0 || rect.X + rect.Width > page.Rect.Width || rect.Y + rect.Height > page.Rect.Height)
        {
            throw new InvalidOperationException("Rectangle lies outside the page bounds.");
        }

        return new Rectangle(
            rect.X,
            page.Rect.Height - rect.Y - rect.Height,
            rect.X + rect.Width,
            page.Rect.Height - rect.Y,
            normalizeCoordinates: true);
    }

    internal static PdfColor ParseColor(string value)
    {
        if (value.Length != 7 || value[0] != '#'
            || !int.TryParse(value.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int red)
            || !int.TryParse(value.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int green)
            || !int.TryParse(value.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int blue))
        {
            throw new InvalidOperationException($"Color '{value}' must use #RRGGBB.");
        }

        return PdfColor.FromRgb(red / 255d, green / 255d, blue / 255d);
    }

    internal static void ApplyPosition(Page page, TextStamp stamp, string position)
    {
        const double edgeMargin = 24;
        float fontSize = stamp.TextState.FontSize > 0 ? stamp.TextState.FontSize : 12;
        double textWidth = stamp.TextState.Font.MeasureString(stamp.Value, fontSize);
        double textHeight = fontSize * 1.25;
        Rectangle box = page.Rect;
        stamp.HorizontalAlignment = HorizontalAlignment.None;
        stamp.VerticalAlignment = VerticalAlignment.None;
        stamp.XIndent = position.EndsWith("left", StringComparison.Ordinal)
            ? box.LLX + edgeMargin
            : position.EndsWith("right", StringComparison.Ordinal)
                ? box.URX - edgeMargin - textWidth
                : box.LLX + ((box.Width - textWidth) / 2);
        stamp.YIndent = position.StartsWith("top", StringComparison.Ordinal)
            ? box.URY - edgeMargin - textHeight
            : box.LLY + edgeMargin;
    }

    internal static OutlineItemCollection? FindOutline(
        IEnumerable<OutlineItemCollection> root,
        IReadOnlyList<string> path)
    {
        IEnumerable<OutlineItemCollection> current = root;
        OutlineItemCollection? found = null;
        foreach (string segment in path)
        {
            found = current.FirstOrDefault(item => string.Equals(item.Title, segment, StringComparison.Ordinal));
            if (found is null)
            {
                return null;
            }

            current = found;
        }

        return found;
    }

    internal static NumberingStyle NumberingStyleValue(string value) => value.ToLowerInvariant() switch
    {
        "arabic" => NumberingStyle.NumeralsArabic,
        "roman-upper" => NumberingStyle.NumeralsRomanUppercase,
        "roman-lower" => NumberingStyle.NumeralsRomanLowercase,
        "letters-upper" => NumberingStyle.LettersUppercase,
        "letters-lower" => NumberingStyle.LettersLowercase,
        "none" => NumberingStyle.None,
        _ => throw new InvalidOperationException($"Unknown page-label style '{value}'."),
    };

    internal static void EnsureAcroForm(Document document)
    {
        if (document.Form.HasXfa)
        {
            throw new CliException(
                PdfDiagnostics.FormXfaUnsupported,
                "XFA forms are read-only in the current PDF command surface.",
                hint: "Convert the XFA form to AcroForm before filling, flattening or exporting it.");
        }
    }

    internal static string? Secret(
        IReadOnlyDictionary<string, string>? secrets,
        string name,
        bool required)
    {
        if (secrets is not null && secrets.TryGetValue(name, out string? value) && !string.IsNullOrEmpty(value))
        {
            return value;
        }

        if (required)
        {
            throw new InvalidOperationException($"The required {name} environment variable is missing or empty.");
        }

        return null;
    }

    internal static void EnsureFile(string path)
    {
        if (!File.Exists(path))
        {
            throw CliErrors.FileNotFound(path);
        }
    }

    /// <summary>
    /// Explains why a form field cannot display a value, or null when it can.
    /// A check box renders only the states its appearance dictionary defines, so any
    /// other value is stored and read back while the box itself stays empty.
    /// </summary>
    internal static string? RejectedFieldValue(Field field, string value) =>
        field is CheckboxField { AllowedStates.Count: > 0 } checkbox
            && !checkbox.AllowedStates.Contains(value, StringComparer.Ordinal)
            ? $"check box '{field.FullName}' has no state '{value}'; use one of: "
                + string.Join(", ", checkbox.AllowedStates)
            : null;

    internal static CliException InvalidOp(int index, string op, string reason, Exception? inner = null) => new(
        ErrorCodes.OpsInvalid,
        $"PDF op {index} ({op}) failed: {reason}",
        hint: "Fix the operation and retry the atomic batch, or use --best-effort for an explicit partial result.",
        innerException: inner);
}
