using Aspose.Cli.Sdk.IO.Input;
using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Pdf;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>
/// <c>pdf split</c>: each part is a new document, new Document(), and Pages.Add(pages) of the part's
/// pages; what the engine copies with a page is what the part holds. The parts are written all or
/// none, and encrypted only with --encrypt.
/// </summary>
internal static class PdfSplit
{
    internal static PdfSplitResult Run(PdfSession session, PdfSplitRequest request)
    {
        LicenseState state = session.Outputs.License;
        using LoadedPdf loaded = session.Loader.Open(request.Input, request.Password);
        Document source = loaded.Document;
        IReadOnlyList<IReadOnlyList<int>> groups = request.Plan.PageGroups(source.Pages.Count);
        IReadOnlyList<int> numbers = request.Plan.PartNumbers(groups);
        IReadOnlyList<string> paths = request.Output.Parts(Path.GetFileNameWithoutExtension(request.Input), "pdf", numbers);
        OutputProtection protection = OutputProtection.For(PdfFormats.Document[0], request.EncryptPassword, new InputProtection(Encrypted: false), inputPassword: null);
        var documents = new PdfDocuments(session.Loader);
        using OutputSet<Document> writer = session.Outputs.BeginSet([request.Output.Path], "pdf-split");
        var parts = new List<Document>();
        try
        {
            foreach ((IReadOnlyList<int> pages, string path) in groups.Zip(paths))
            {
                var part = new Document();
                parts.Add(part);
                part.Pages.Add([.. pages.Select(number => source.Pages[number])]);
                writer.Stage(path, PdfFormats.Document[0], request.Output.Overwrite, staged => documents.Save(part, staged, protection), encrypted: protection.Encrypted);
            }

            IReadOnlyList<PublishedFile> files = writer.Commit();
            return new PdfSplitResult
            {
                Input = FileObjects.Input(request.Input, loaded.FormatId, loaded.Protection.Encrypted),
                Outputs = [.. groups.Select((pages, index) => new PdfPartOutput(FileObjects.Output(files[index], PdfFormats.Document[0], protection.Encrypted))
                {
                    Pages = Aspose.Cli.Sdk.Addressing.PageRange.Describe(pages) ?? string.Empty,
                })],
                License = EnvelopeParts.License(state),
            };
        }
        finally
        {
            foreach (Document part in parts)
            {
                part.Dispose();
            }
        }
    }
}
