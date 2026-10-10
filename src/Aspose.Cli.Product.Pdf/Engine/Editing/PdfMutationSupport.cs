using System.Globalization;
using Aspose.Cli.Sdk.Errors;
using Aspose.Pdf;
using Aspose.Pdf.Forms;
using PdfColor = Aspose.Pdf.Color;

namespace Aspose.Cli.Product.Pdf.Engine.Editing;

/// <summary>Provides stateless helpers shared by the PDF mutation handlers.</summary>
internal static class PdfMutationSupport
{
    internal static IReadOnlyList<int> Resolve(Document document, string text) =>
        Aspose.Cli.Sdk.Addressing.PageRange.Parse(text).Resolve(document.Pages.Count);

    internal static IReadOnlyList<int> ResolveOptional(Document document, string? text) =>
        text is null ? Enumerable.Range(1, document.Pages.Count).ToArray() : Resolve(document, text);

    /// <summary>Converts a <c>#RRGGBB</c> color, which the operation's <c>[HexColor]</c> member has already checked.</summary>
    internal static PdfColor ParseColor(string value) =>
        PdfColor.FromRgb(Channel(value, 1), Channel(value, 3), Channel(value, 5));

    private static double Channel(string color, int start) =>
        int.Parse(color.AsSpan(start, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255d;

    /// <summary>
    /// Aligns a margin stamp 24 pt inside an edge of the displayed page. The engine aligns the
    /// text it draws, so the width is that of the font it substitutes for characters the
    /// stamp's font lacks, such as CJK text in the default font.
    /// </summary>
    internal static void ApplyPosition(TextStamp stamp, string position)
    {
        const double edgeMargin = 24;
        stamp.HorizontalAlignment = position.EndsWith("left", StringComparison.Ordinal)
            ? HorizontalAlignment.Left
            : position.EndsWith("right", StringComparison.Ordinal)
                ? HorizontalAlignment.Right
                : HorizontalAlignment.Center;
        stamp.VerticalAlignment = position.StartsWith("top", StringComparison.Ordinal)
            ? VerticalAlignment.Top
            : VerticalAlignment.Bottom;
        stamp.LeftMargin = edgeMargin;
        stamp.RightMargin = edgeMargin;
        stamp.TopMargin = edgeMargin;
        stamp.BottomMargin = edgeMargin;
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

    /// <summary>The first AcroForm field with a full name, or <c>FIELD_NOT_FOUND</c> listing every full name.</summary>
    internal static Field FormField(Document document, string name) => FormFields(document, name)[0];

    /// <summary>
    /// Every AcroForm field with a full name, such as each button of a radio group, or
    /// <c>FIELD_NOT_FOUND</c> listing every full name once, so a radio group counts as one field.
    /// </summary>
    internal static Field[] FormFields(Document document, string name)
    {
        Field[] fields = [.. document.Form.Fields.Where(field => string.Equals(field.FullName, name, StringComparison.Ordinal))];
        return fields.Length > 0
            ? fields
            : throw CliErrors.NotFound(
                PdfDiagnostics.FieldNotFound,
                "form field",
                name,
                document.Form.Fields
                    .Select(static field => field.FullName)
                    .Where(static fullName => !string.IsNullOrEmpty(fullName))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray());
    }

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
        field is CheckboxField || PdfForms.RadioGroup(field) is not null ? "Off" : string.Empty;

    /// <summary>
    /// Explains why a form field cannot display a value, or null when it can.
    /// A check box renders only the states its appearance dictionary defines, so any
    /// other value is stored and read back while the box itself stays empty. A radio group
    /// selects only one of its buttons' values; any other value selects none.
    /// </summary>
    internal static string? RejectedFieldValue(Field field, string value)
    {
        if (field is CheckboxField checkbox
            && PdfForms.CheckboxStates(checkbox) is { Count: > 0 } states
            && !states.Contains(value, StringComparer.Ordinal))
        {
            return $"check box '{field.FullName}' has no state '{value}'; use one of: " + string.Join(", ", states);
        }

        if (PdfForms.RadioGroup(field) is { } group
            && PdfForms.ChoiceValues(group) is { Count: > 0 } options
            && !options.Contains(value, StringComparer.Ordinal))
        {
            return $"radio group '{field.FullName}' has no option '{value}'; use one of: " + string.Join(", ", options);
        }

        return null;
    }
}
