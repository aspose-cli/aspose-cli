using Aspose.Cli.Sdk.IO.Input;
using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Pdf;
using Aspose.Pdf.Facades;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>
/// <c>pdf merge</c>: PdfFileEditor.Concatenate(inputs, merged) into a new document, with the
/// editor's defaults: it copies the inputs' bookmarks, page labels and attachments, and renames a
/// field whose name an earlier input took. The output is encrypted only with --encrypt.
/// </summary>
internal static class PdfMerge
{
    internal static PdfMergeResult Run(PdfSession session, PdfMergeRequest request)
    {
        LicenseState state = session.Outputs.License;
        var opened = new List<LoadedPdf>(request.InputPaths.Count);
        try
        {
            foreach (string path in request.InputPaths)
            {
                opened.Add(session.Loader.Open(path, request.Password));
            }

            using var merged = new Document();
            bool concatenated = new PdfFileEditor().Concatenate([.. opened.Select(static loaded => loaded.Document)], merged);
            OutputProtection protection = OutputProtection.For(request.Output.Format, request.EncryptPassword, new InputProtection(Encrypted: false), inputPassword: null);
            var documents = new PdfDocuments(session.Loader);
            PublishedFile written = session.Outputs.Write(request.Output.Path, request.Output.Format, request.Output.Overwrite,
                path => documents.Save(merged, path, protection), encrypted: protection.Encrypted);
            return new PdfMergeResult
            {
                Output = FileObjects.Output(written, request.Output.Format, protection.Encrypted),
                Inputs = [.. request.InputPaths.Zip(opened, static (path, loaded) => FileObjects.Input(path, loaded.FormatId, loaded.Protection.Encrypted))],
                Concatenated = concatenated,
                License = EnvelopeParts.License(state),
            };
        }
        finally
        {
            foreach (LoadedPdf loaded in opened)
            {
                loaded.Dispose();
            }
        }
    }
}
