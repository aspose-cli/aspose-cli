using System.Globalization;
using Aspose.Cli.Sdk.Errors;
using Aspose.Pdf;

namespace Aspose.Cli.Product.Pdf.Engine.Mapping;

/// <summary>
/// The one bookmark address scheme, which <c>pdf inspect</c> reports and the bookmark
/// operations resolve: 1-based positions from the top level down, joined by '/'.
/// </summary>
internal static class PdfOutlineAddresses
{
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

    private static string OutlineHint(string? parentIndex, int count) => (parentIndex, count) switch
    {
        (null, 0) => "The document has no bookmarks.",
        (null, 1) => "The document has one top-level bookmark; use 1.",
        (null, _) => $"Use a top-level bookmark from 1 through {count}.",
        (_, 0) => $"Bookmark {parentIndex} has no child bookmarks.",
        (_, 1) => $"Bookmark {parentIndex} has one child bookmark; use {parentIndex}/1.",
        _ => $"Bookmark {parentIndex} has {count} child bookmarks; use {parentIndex}/1 through {parentIndex}/{count}.",
    };
}
