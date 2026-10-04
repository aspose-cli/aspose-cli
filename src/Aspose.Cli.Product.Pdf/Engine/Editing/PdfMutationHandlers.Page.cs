using Aspose.Cli.Sdk.Errors;
using Aspose.Pdf;
using static Aspose.Cli.Product.Pdf.Engine.PdfEngineSupport;
using static Aspose.Cli.Product.Pdf.Engine.Editing.PdfMutationSupport;

namespace Aspose.Cli.Product.Pdf.Engine.Editing;

// Page structure and page geometry.
internal sealed partial class PdfMutationHandlers
{
    public long Apply(RotatePagesOp operation)
    {
        IReadOnlyList<int> pages = Resolve(_document, operation.Pages);
        Rotation rotation = operation.Angle switch
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
            _touched.Add(number);
        }

        return pages.Count;
    }

    public long Apply(DeletePagesOp operation)
    {
        int[] pages = Resolve(_document, operation.Pages).ToArray();
        if (pages.Length == _document.Pages.Count)
        {
            throw new OperationInvalidException("A PDF must retain at least one page.");
        }

        _document.Pages.Delete(pages);
        return pages.Length;
    }

    public long Apply(MovePagesOp operation)
    {
        int[] pages = Resolve(_document, operation.Pages).ToArray();
        EnsureInsertionPosition(_document, operation.To);

        // The SDK has no page move: the pages are copied and the originals deleted, and the
        // navigation that targeted the originals is pointed at the copies.
        var navigation = PdfNavigationRetarget.Capture(_document, pages);
        using Document selected = Select(_document, pages);
        int before = pages.Count(page => page < operation.To);
        _document.Pages.Delete(pages);
        int at = Math.Clamp(operation.To - before, 1, _document.Pages.Count + 1);
        _document.Pages.Insert(at, selected.Pages.ToArray());
        navigation.Retarget(_document, page => Array.IndexOf(pages, page) is var moved and >= 0
            ? at + moved
            : Renumber(page, pages, at));
        for (int index = 0; index < pages.Length; index++)
        {
            _touched.Add(at + index);
        }

        return pages.Length;
    }

    /// <summary>The new number of a page that stayed while <paramref name="moved"/> moved to <paramref name="at"/>.</summary>
    private static int Renumber(int page, int[] moved, int at)
    {
        int remaining = page - moved.Count(number => number < page);
        return remaining >= at ? remaining + moved.Length : remaining;
    }

    public long Apply(InsertPagesFromOp operation)
    {
        EnsureInsertionPosition(_document, operation.At);

        string? password = OperationSecrets.Resolve(_secrets, operation.PasswordEnv);
        using LoadedPdf source = OpenSource(operation.Path, password);
        IReadOnlyList<int> pages = operation.Pages is null
            ? Enumerable.Range(1, source.Document.Pages.Count).ToArray()
            : Resolve(source.Document, operation.Pages);
        using Document selected = Select(source.Document, pages);
        _document.Pages.Insert(operation.At, selected.Pages.ToArray());
        for (int index = 0; index < pages.Count; index++)
        {
            _touched.Add(operation.At + index);
        }

        return pages.Count;
    }

    // The source's password comes from the operation's passwordEnv, not a command option.
    private LoadedPdf OpenSource(string path, string? password)
    {
        try
        {
            return _loader.Open(path, password);
        }
        catch (CliException error) when (CliErrors.IsPasswordError(error))
        {
            throw CliErrors.ForOperationSource(error, "passwordEnv");
        }
    }

    public long Apply(InsertBlankPageOp operation)
    {
        EnsureInsertionPosition(_document, operation.At);

        Page page = _document.Pages.Insert(operation.At);
        (double width, double height) = PdfPageSizes.Dimensions(operation.Size);
        page.SetPageSize(width, height);
        _touched.Add(operation.At);
        return 1;
    }

    public long Apply(CropPagesOp operation)
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

            _touched.Add(number);
        }

        return pages.Count;
    }

    public long Apply(SetPageSizeOp operation)
    {
        (double width, double height) = PdfPageSizes.Dimensions(operation.Size);
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
            }

            _touched.Add(number);
        }

        return pages.Count;
    }
}
