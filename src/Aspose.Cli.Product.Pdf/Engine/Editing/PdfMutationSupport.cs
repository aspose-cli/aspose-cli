using System.Globalization;
using System.Text.RegularExpressions;
using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Product.Pdf.Engine.Mapping;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Operations;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.Sdk.Text;
using Aspose.Pdf;
using Aspose.Pdf.Annotations;
using Aspose.Pdf.Forms;
using Aspose.Pdf.Optimization;
using Aspose.Pdf.Text;
using static Aspose.Cli.Product.Pdf.Engine.PdfEngineSupport;
using PdfColor = Aspose.Pdf.Color;

namespace Aspose.Cli.Product.Pdf.Engine.Editing;

/// <summary>Provides stateless helpers shared by the PDF mutation handlers.</summary>
internal static class PdfMutationSupport
{
    internal static IReadOnlyList<int> Resolve(Document document, string text) =>
        Aspose.Cli.Sdk.Addressing.PageRange.Parse(text).Resolve(document.Pages.Count);

    internal static IReadOnlyList<int> ResolveOptional(Document document, string? text) =>
        text is null ? Enumerable.Range(1, document.Pages.Count).ToArray() : Resolve(document, text);

    internal static PdfColor ParseColor(string value)
    {
        if (value.Length != 7 || value[0] != '#'
            || !int.TryParse(value.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int red)
            || !int.TryParse(value.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int green)
            || !int.TryParse(value.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int blue))
        {
            throw new OperationInvalidException($"Color '{value}' must use #RRGGBB.");
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

    /// <summary>
    /// The bookmark at a slash-separated title path. A path that matches no bookmark is
    /// <c>BOOKMARK_NOT_FOUND</c> listing every title path; a path that several sibling
    /// bookmarks share is refused rather than resolved to one of them.
    /// </summary>
    internal static OutlineItemCollection Outline(OutlineCollection outlines, string path)
    {
        IEnumerable<OutlineItemCollection> current = outlines;
        OutlineItemCollection? found = null;
        foreach (string segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            OutlineItemCollection[] matches = current
                .Where(item => string.Equals(item.Title, segment, StringComparison.Ordinal))
                .Take(2)
                .ToArray();
            if (matches.Length == 0)
            {
                throw CliErrors.NotFound(
                    ErrorCodes.BookmarkNotFound, "bookmark", path, OutlinePaths(outlines, parentPath: null).Distinct().ToArray());
            }

            if (matches.Length > 1)
            {
                throw new OperationInvalidException(
                    $"Bookmark path '{path}' is ambiguous: several sibling bookmarks are titled '{segment}'.",
                    "A title path selects one bookmark only when its titles are unique among their siblings; "
                    + "use delete_bookmarks with all: true and add the bookmarks again to restructure them.");
            }

            found = matches[0];
            current = found;
        }

        return found ?? throw new OperationInvalidException($"Bookmark path '{path}' names no title.");
    }

    /// <summary>
    /// The title path <see cref="Outline"/> resolves for a bookmark titled <paramref name="title"/>
    /// below the bookmark at <paramref name="parentPath"/>, or at the top level when it is null.
    /// </summary>
    internal static string OutlinePath(string? parentPath, string? title) =>
        parentPath is null ? title ?? string.Empty : $"{parentPath}/{title}";

    private static IEnumerable<string> OutlinePaths(IEnumerable<OutlineItemCollection> items, string? parentPath)
    {
        foreach (OutlineItemCollection item in items)
        {
            string path = OutlinePath(parentPath, item.Title);
            yield return path;
            foreach (string child in OutlinePaths(item, path))
            {
                yield return child;
            }
        }
    }

    /// <summary>The AcroForm field with a full name, or <c>FIELD_NOT_FOUND</c> listing every full name.</summary>
    internal static Field FormField(Document document, string name) =>
        document.Form.Fields.FirstOrDefault(field => string.Equals(field.FullName, name, StringComparison.Ordinal))
        ?? throw CliErrors.NotFound(
            PdfDiagnostics.FieldNotFound,
            "form field",
            name,
            document.Form.Fields
                .Select(static field => field.FullName)
                .Where(static fullName => !string.IsNullOrEmpty(fullName))
                .ToArray());

    /// <summary>
    /// Refuses a position past the end of the document; pages insert before positions 1
    /// through the page count, and position count + 1 appends.
    /// </summary>
    internal static void EnsureInsertionPosition(Document document, int position)
    {
        if (position > document.Pages.Count + 1)
        {
            throw CliErrors.NotFoundAt(
                ErrorCodes.PageNotFound,
                "page position",
                position.ToString(CultureInfo.InvariantCulture),
                document.Pages.Count + 1);
        }
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
    /// other value is stored and read back while the box itself stays empty. A radio group
    /// selects only one of its buttons' values; any other value selects none.
    /// </summary>
    internal static string? RejectedFieldValue(Field field, string value)
    {
        if (field is CheckboxField checkbox
            && PdfFormService.CheckboxStates(checkbox) is { Count: > 0 } states
            && !states.Contains(value, StringComparer.Ordinal))
        {
            return $"check box '{field.FullName}' has no state '{value}'; use one of: " + string.Join(", ", states);
        }

        if (PdfFormService.RadioGroup(field) is { } group
            && PdfFormService.ChoiceValues(group) is { Count: > 0 } options
            && !options.Contains(value, StringComparer.Ordinal))
        {
            return $"radio group '{field.FullName}' has no option '{value}'; use one of: " + string.Join(", ", options);
        }

        return null;
    }
}
