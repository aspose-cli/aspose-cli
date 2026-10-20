using System.Globalization;
using Aspose.Cli.Sdk.Errors;
using Aspose.Pdf;
using Aspose.Pdf.Forms;
using static Aspose.Cli.Product.Pdf.Engine.Mapping.PdfOutlineAddresses;
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
    /// Removes exactly <paramref name="item"/> with its children. Known issue
    /// PDF-OUTLINE-DELETE-TITLE (KNOWN-ISSUES.md): <see cref="OutlineItemCollection.Delete()"/>
    /// removes bookmarks by title, so the item first takes a title no other bookmark has.
    /// </summary>
    internal static void DeleteOutline(OutlineItemCollection item)
    {
        item.Title = $"__delete_{Guid.NewGuid():N}";
        item.Delete();
    }

    /// <summary>The index of the bookmark add_bookmark just appended below <paramref name="parent"/>.</summary>
    internal static string NewBookmarkIndex(Document document, string? parent) =>
        OutlineIndex(parent, parent is null ? document.Outlines.Count : Outline(document.Outlines, parent).Count);

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
    internal static void EnsureInsertionPosition(Document document, int position) =>
        EnsurePagePosition(position, document.Pages.Count + 1);

    /// <summary>Refuses a 1-based page position beyond <paramref name="last"/>.</summary>
    internal static void EnsurePagePosition(int position, int last)
    {
        if (position > last)
        {
            throw CliErrors.NotFoundAt(
                ErrorCodes.PageNotFound,
                "page position",
                position.ToString(CultureInfo.InvariantCulture),
                last);
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

    /// <summary>
    /// Refuses to set or clear a field that holds no value: the engine draws a push button's
    /// value as its caption, and a signature field is filled only by signing.
    /// </summary>
    internal static void RequireValueField(Field field)
    {
        string? kind = field switch
        {
            ButtonField => "push button",
            SignatureField => "signature field",
            _ => null,
        };
        if (kind is not null)
        {
            throw new OperationInvalidException(
                $"{kind} '{field.FullName}' holds no value",
                "To place an image on the form, use add_stamp_image with the field's page and rect from 'pdf inspect --detail forms'; a signature field is filled by 'pdf sign'.");
        }
    }
}
