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
using Aspose.Pdf.Devices;
using Aspose.Pdf.Forms;
using Aspose.Pdf.Optimization;
using Aspose.Pdf.Text;
using static Aspose.Cli.Product.Pdf.Engine.PdfEngineSupport;
using static Aspose.Cli.Product.Pdf.Engine.PdfMutationSupport;
using PdfColor = Aspose.Pdf.Color;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>Routes a validated PDF operation to its handler.</summary>
internal static class PdfMutationHandlers
{
    internal static long ApplyOp(
        PdfDocumentLoader loader,
        InputResourceScope inputs,
        Document document,
        PdfOp op,
        IReadOnlyDictionary<string, string>? secrets,
        ISet<int> touched)
    {
        try
        {
            return op switch
            {
                RotatePagesOp value => PdfPageMutationHandlers.Rotate(document, value, touched),
                DeletePagesOp value => PdfPageMutationHandlers.DeletePages(document, value),
                MovePagesOp value => PdfPageMutationHandlers.MovePages(document, value, touched),
                InsertPagesFromOp value => PdfPageMutationHandlers.InsertPages(loader, document, value, secrets, touched),
                InsertBlankPageOp value => PdfPageMutationHandlers.InsertBlank(document, value, touched),
                CropPagesOp value => PdfPageMutationHandlers.Crop(document, value, touched),
                SetPageSizeOp value => PdfPageMutationHandlers.SetPageSize(document, value, touched),
                AddWatermarkTextOp value => PdfContentMutationHandlers.WatermarkText(document, value, touched),
                AddWatermarkImageOp value => PdfContentMutationHandlers.WatermarkImage(document, value, touched, inputs),
                AddPageNumbersOp value => PdfContentMutationHandlers.PageNumbers(document, value, touched),
                AddHeaderTextOp value => PdfContentMutationHandlers.HeaderFooter(document, value.Text, value.Pages, value.Position, value.Font, touched),
                AddFooterTextOp value => PdfContentMutationHandlers.HeaderFooter(document, value.Text, value.Pages, value.Position, value.Font, touched),
                AddStampImageOp value => PdfContentMutationHandlers.StampImage(document, value, touched, inputs),
                AddLinkOp value => PdfContentMutationHandlers.AddLink(document, value, touched),
                RedactTextOp value => PdfContentMutationHandlers.RedactText(document, value, touched),
                RedactAreaOp value => PdfContentMutationHandlers.RedactArea(document, value, touched),
                SetMetadataOp value => PdfDocumentMutationHandlers.SetMetadata(document, value),
                RemoveMetadataOp value => PdfDocumentMutationHandlers.RemoveMetadata(document, value),
                AddBookmarkOp value => PdfDocumentMutationHandlers.AddBookmark(document, value),
                DeleteBookmarksOp value => PdfDocumentMutationHandlers.DeleteBookmarks(document, value),
                AddAttachmentOp value => PdfDocumentMutationHandlers.AddAttachment(document, value, inputs),
                RemoveAttachmentOp value => PdfDocumentMutationHandlers.RemoveAttachment(document, value),
                SetPageLabelsOp value => PdfDocumentMutationHandlers.SetPageLabels(document, value),
                SetFormFieldOp value => PdfDocumentMutationHandlers.SetFormField(document, value),
                FlattenFormsOp value => PdfDocumentMutationHandlers.FlattenForms(document, value),
                EncryptPdfOp value => PdfDocumentMutationHandlers.Encrypt(document, value, secrets),
                DecryptPdfOp => PdfDocumentMutationHandlers.Decrypt(document),
                OptimizePdfOp value => PdfDocumentMutationHandlers.Optimize(document, value),
                _ => throw new InvalidOperationException($"No PDF handler for {op.GetType().Name}."),
            };
        }
        catch (Exception exception) when (
            exception.GetType().Assembly.GetName().Name == "Aspose.PDF"
            || exception is IOException or UnauthorizedAccessException)
        {
            throw new EngineOpException(exception.Message, exception);
        }
        finally { inputs.ThrowIfFailed(); }
    }
}
