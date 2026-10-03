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
    /// The bookmark at an index: 1-based positions from the top level down, joined by '/'.
    /// Positions count the bookmarks in enumeration order, as <c>pdf inspect</c> lists them.
    /// A position past its level is <c>BOOKMARK_NOT_FOUND</c> naming that level's count.
    /// </summary>
    internal static OutlineItemCollection Outline(OutlineCollection outlines, string index)
    {
        IEnumerable<OutlineItemCollection> current = outlines;
        OutlineItemCollection? found = null;
        string? parentIndex = null;
        foreach (string segment in index.Split('/'))
        {
            OutlineItemCollection[] siblings = current.ToArray();
            if (!int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out int position)
                || position < 1 || position > siblings.Length)
            {
                throw CliErrors.NotFoundAt(
                    ErrorCodes.BookmarkNotFound, "bookmark", index, siblings.Length, OutlineHint(parentIndex, siblings.Length));
            }

            found = siblings[position - 1];
            current = found;
            parentIndex = OutlineIndex(parentIndex, position);
        }

        return found!;
    }

    /// <summary>
    /// Removes exactly <paramref name="item"/> with its children. Known issue
    /// PDF-OUTLINE-DELETE-TITLE (KNOWN-ISSUES.md): <see cref="OutlineItemCollection.Delete()"/>
    /// removes bookmarks by title, so the item first takes a title no other bookmark has.
    /// </summary>
    internal static void DeleteOutline(OutlineItemCollection item)
    {
        item.Title = $"__delete_{Guid.NewGuid():N}";
        item.Delete();
    }

    /// <summary>How many bookmarks <paramref name="items"/> hold, their descendants included.</summary>
    internal static int CountOutline(IEnumerable<OutlineItemCollection> items) =>
        items.Sum(static item => 1 + CountOutline(item));

    /// <summary>
    /// The index <see cref="Outline"/> resolves for the bookmark at 1-based
    /// <paramref name="position"/> below the bookmark at <paramref name="parentIndex"/>, or at
    /// the top level when it is null.
    /// </summary>
    internal static string OutlineIndex(string? parentIndex, int position) =>
        parentIndex is null
            ? position.ToString(CultureInfo.InvariantCulture)
            : $"{parentIndex}/{position.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>The index of the bookmark add_bookmark just appended below <paramref name="parent"/>.</summary>
    internal static string NewBookmarkIndex(Document document, string? parent) =>
        OutlineIndex(parent, parent is null ? document.Outlines.Count : Outline(document.Outlines, parent).Count);

    private static string OutlineHint(string? parentIndex, int count) => (parentIndex, count) switch
    {
        (null, 0) => "The document has no bookmarks.",
        (null, 1) => "The document has one top-level bookmark; use 1.",
        (null, _) => $"Use a top-level bookmark from 1 through {count}.",
        (_, 0) => $"Bookmark {parentIndex} has no child bookmarks.",
        (_, 1) => $"Bookmark {parentIndex} has one child bookmark; use {parentIndex}/1.",
        _ => $"Bookmark {parentIndex} has {count} child bookmarks; use {parentIndex}/1 through {parentIndex}/{count}.",
    };

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
    /// The value that clears a form field: <c>Off</c> selects no button of a radio group and
    /// unchecks a check box; any other field is emptied.
    /// </summary>
    internal static string ClearedFieldValue(Field field) =>
        field is CheckboxField || PdfFormService.RadioGroup(field) is not null ? "Off" : string.Empty;

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
