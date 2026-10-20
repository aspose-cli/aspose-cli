using Aspose.Cli.Sdk.IO.Input;
using Aspose.Pdf;
using static Aspose.Cli.Product.Pdf.Engine.Editing.PdfMutationSupport;
using static Aspose.Cli.Product.Pdf.Engine.PdfAssembly;
using static Aspose.Cli.Product.Pdf.Engine.PdfGeometry;

namespace Aspose.Cli.Product.Pdf.Engine.Editing;

// Page structure and page geometry.
internal sealed partial class PdfMutationHandlers
{
    public ValueTuple Apply(RotatePagesOp operation)
    {
        IReadOnlyList<int> pages = Resolve(_document, operation.Pages);
        Rotation rotation = operation.Rotation switch
        {
            0 => Rotation.None,
            90 => Rotation.on90,
            180 => Rotation.on180,
            270 => Rotation.on270,
            _ => throw new OperationInvalidException("Rotation must be 0, 90, 180 or 270 degrees."),
        };
        foreach (int number in pages)
        {
            _document.Pages[number].Rotate = rotation;
        }

        return default;
    }

    public ValueTuple Apply(DeletePagesOp operation)
    {
        int[] pages = Resolve(_document, operation.Pages).ToArray();
        if (pages.Length == _document.Pages.Count)
        {
            throw new OperationInvalidException("A PDF must retain at least one page.");
        }

        _document.Pages.Delete(pages);
        return default;
    }

    public ValueTuple Apply(MovePagesOp operation)
    {
        int[] pages = Resolve(_document, operation.Pages).ToArray();
        // The moved pages must fit from the position to the end.
        EnsurePagePosition(operation.To, _document.Pages.Count - pages.Length + 1);

        // The SDK has no page move: the pages are copied and the originals deleted, and the
        // navigation that targeted the originals is pointed at the copies.
        var navigation = PdfNavigationRetarget.Capture(_document, pages);
        Page[] originals = [.. pages.Select(page => _document.Pages[page])];
        using Document selected = Select(_document, pages);
        int at = operation.To;
        _document.Pages.Delete(pages);
        _document.Pages.Insert(at, selected.Pages.ToArray());
        navigation.Retarget(_document, page => Array.IndexOf(pages, page) is var moved and >= 0
            ? at + moved
            : Renumber(page, pages, at));
        for (int index = 0; index < originals.Length; index++)
        {
            _pages.Carry(originals[index], _document.Pages[at + index]);
        }

        return default;
    }

    /// <summary>The new number of a page that stayed while <paramref name="moved"/> moved to <paramref name="at"/>.</summary>
    private static int Renumber(int page, int[] moved, int at)
    {
        int remaining = page - moved.Count(number => number < page);
        return remaining >= at ? remaining + moved.Length : remaining;
    }

    public ValueTuple Apply(ImportPagesOp operation)
    {
        EnsureInsertionPosition(_document, operation.At);

        using LoadedPdf source = _loader.Open(operation.Path, PasswordSource.OperationField(
            OperationSecrets.Resolve(_secrets, operation.SourcePasswordEnv), "sourcePasswordEnv"));
        IReadOnlyList<int> pages = operation.Pages is null
            ? Enumerable.Range(1, source.Document.Pages.Count).ToArray()
            : Resolve(source.Document, operation.Pages);
        using Document selected = Select(source.Document, pages);
        _document.Pages.Insert(operation.At, selected.Pages.ToArray());
        return default;
    }

    public ValueTuple Apply(InsertPageOp operation)
    {
        EnsureInsertionPosition(_document, operation.At);

        Page page = _document.Pages.Insert(operation.At);
        (double width, double height) = operation.PageSize?.Dimensions() ?? PageSizePresets.Dimensions(PageSizePresets.A4);
        page.SetPageSize(width, height);
        return default;
    }

    public ValueTuple Apply(CropPagesOp operation)
    {
        IReadOnlyList<int> pages = Resolve(_document, operation.Pages);
        foreach (int number in pages)
        {
            Page page = _document.Pages[number];
            Rectangle rectangle = ToPdfRect(page, operation.Rect);
            if (operation.Box == "media")
            {
                page.MediaBox = rectangle;
            }
            else
            {
                page.CropBox = rectangle;
            }

        }

        return default;
    }

    public ValueTuple Apply(SetPageSizeOp operation)
    {
        (double width, double height) = operation.PageSize.Dimensions();
        IReadOnlyList<int> pages = Resolve(_document, operation.Pages);
        foreach (int number in pages)
        {
            Page page = _document.Pages[number];
            if (operation.ScaleContent)
            {
                page.Resize(new PageSize((float)width, (float)height));
            }
            else
            {
                page.SetPageSize(width, height);
                // The engine sets the media box alone; a crop box the page had would keep showing the
                // old size, so the visible page becomes the whole new page.
                page.CropBox = page.MediaBox;
            }

        }

        return default;
    }
}
