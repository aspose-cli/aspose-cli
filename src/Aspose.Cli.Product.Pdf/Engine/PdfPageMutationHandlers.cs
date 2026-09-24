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
using Aspose.Pdf.Devices;
using Aspose.Pdf.Forms;
using Aspose.Pdf.Optimization;
using Aspose.Pdf.Text;
using static Aspose.Cli.Product.Pdf.Engine.PdfEngineSupport;
using static Aspose.Cli.Product.Pdf.Engine.PdfMutationSupport;
using PdfColor = Aspose.Pdf.Color;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>Owns page structure and page geometry mutations.</summary>
internal static class PdfPageMutationHandlers
{
    internal static long Rotate(Document document, RotatePagesOp op, ISet<int> touched)
    {
        IReadOnlyList<int> pages = Resolve(document, op.Pages);
        Rotation rotation = op.Angle switch
        {
            90 => Rotation.on90,
            180 => Rotation.on180,
            270 => Rotation.on270,
            _ => throw new OperationInvalidException("Rotation must be 90, 180 or 270 degrees."),
        };
        foreach (int number in pages)
        {
            document.Pages[number].Rotate = rotation;
            touched.Add(number);
        }

        return pages.Count;
    }

    internal static long DeletePages(Document document, DeletePagesOp op)
    {
        int[] pages = Resolve(document, op.Pages).ToArray();
        if (pages.Length == document.Pages.Count)
        {
            throw new OperationInvalidException("A PDF must retain at least one page.");
        }

        document.Pages.Delete(pages);
        return pages.Length;
    }

    internal static long MovePages(Document document, MovePagesOp op, ISet<int> touched)
    {
        int[] pages = Resolve(document, op.Pages).ToArray();
        if (op.To > document.Pages.Count + 1)
        {
            throw PageNotFound(op.To, document.Pages.Count + 1);
        }

        using Document selected = Select(document, pages);
        int before = pages.Count(page => page < op.To);
        document.Pages.Delete(pages);
        int at = Math.Clamp(op.To - before, 1, document.Pages.Count + 1);
        document.Pages.Insert(at, selected.Pages.ToArray());
        for (int index = 0; index < pages.Length; index++)
        {
            touched.Add(at + index);
        }

        return pages.Length;
    }

    internal static long InsertPages(
        PdfDocumentLoader loader,
        Document document,
        InsertPagesFromOp op,
        IReadOnlyDictionary<string, string>? secrets,
        ISet<int> touched)
    {
        if (op.At > document.Pages.Count + 1)
        {
            throw PageNotFound(op.At, document.Pages.Count + 1);
        }

        string? password = Secret(secrets, op.PasswordEnv);
        using LoadedPdf source = loader.Open(op.Path, password);
        IReadOnlyList<int> pages = op.Pages is null
            ? Enumerable.Range(1, source.Document.Pages.Count).ToArray()
            : Resolve(source.Document, op.Pages);
        using Document selected = Select(source.Document, pages);
        document.Pages.Insert(op.At, selected.Pages.ToArray());
        for (int index = 0; index < pages.Count; index++)
        {
            touched.Add(op.At + index);
        }

        return pages.Count;
    }

    internal static long InsertBlank(Document document, InsertBlankPageOp op, ISet<int> touched)
    {
        if (op.At > document.Pages.Count + 1)
        {
            throw PageNotFound(op.At, document.Pages.Count + 1);
        }

        Page page = document.Pages.Insert(op.At);
        (double width, double height) = PdfPageSizes.Dimensions(op.Size);
        page.SetPageSize(width, height);
        touched.Add(op.At);
        return 1;
    }

    internal static long Crop(Document document, CropPagesOp op, ISet<int> touched)
    {
        IReadOnlyList<int> pages = Resolve(document, op.Pages);
        foreach (int number in pages)
        {
            Page page = document.Pages[number];
            Rectangle rectangle = ToPdfRect(page, op.Rect);
            if (op.Box == "media")
            {
                page.MediaBox = rectangle;
            }
            else
            {
                page.CropBox = rectangle;
            }

            touched.Add(number);
        }

        return pages.Count;
    }

    internal static long SetPageSize(Document document, SetPageSizeOp op, ISet<int> touched)
    {
        (double width, double height) = PdfPageSizes.Dimensions(op.Size);
        IReadOnlyList<int> pages = Resolve(document, op.Pages);
        foreach (int number in pages)
        {
            Page page = document.Pages[number];
            if (op.ScaleContent)
            {
                page.Resize(new PageSize((float)width, (float)height));
            }
            else
            {
                page.SetPageSize(width, height);
            }

            touched.Add(number);
        }

        return pages.Count;
    }
}
