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
            90 => Rotation.on90,
            180 => Rotation.on180,
            270 => Rotation.on270,
            _ => throw new OperationInvalidException("Rotation must be 90, 180 or 270 degrees."),
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

        using Document selected = Select(_document, pages);
        int before = pages.Count(page => page < operation.To);
        _document.Pages.Delete(pages);
        int at = Math.Clamp(operation.To - before, 1, _document.Pages.Count + 1);
        _document.Pages.Insert(at, selected.Pages.ToArray());
        for (int index = 0; index < pages.Length; index++)
        {
            _touched.Add(at + index);
        }

        return pages.Length;
    }

    public long Apply(InsertPagesFromOp operation)
    {
        EnsureInsertionPosition(_document, operation.At);

        string? password = OperationSecrets.Resolve(_secrets, operation.PasswordEnv);
        using LoadedPdf source = _loader.Open(operation.Path, password);
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
