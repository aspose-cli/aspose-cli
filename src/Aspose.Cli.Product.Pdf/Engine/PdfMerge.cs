using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Pdf;
using static Aspose.Cli.Product.Pdf.Engine.PdfEngineSupport;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>Merges PDF inputs into one document: <c>pdf merge</c>.</summary>
internal static class PdfMerge
{
    /// <summary>
    /// Merges the inputs. Merging reads every page, so in evaluation mode it names that cause
    /// and its own remedy for reading past the page limit, which the product guard keeps.
    /// </summary>
    internal static PdfWriteResult Run(PdfSession session, PdfMergeRequest request) =>
        PdfEvaluation.Run(
            session.Outputs,
            () => Merge(session, request),
            cause: "merging reads every page of the inputs, and together they have more",
            remedy: $"merge inputs that have at most {PdfEvaluation.VisiblePages} pages together");

    private static PdfWriteResult Merge(PdfSession session, PdfMergeRequest request)
    {
        if (request.InputPaths.Count < 2)
        {
            throw CliErrors.Usage(["Pass at least two PDF inputs to merge."]);
        }

        LicenseState state = session.Outputs.License;
        var inputs = new List<SourceInfo>(request.InputPaths.Count);
        using var merged = new Document();
        int bookmarks = 0;
        int brokenInputLinks = 0;
        int namedDestinations = 0;
        foreach (string path in request.InputPaths)
        {
            using LoadedPdf loaded = session.Loader.Open(path, request.Password);
            Document source = loaded.Document;
            brokenInputLinks += PdfNavigationCensus.Unresolved(source).Links;
            // The merged document carries no named destinations, so every working one is lost.
            namedDestinations += PdfNavigationCensus.NamedDestinationNames(source).Count(name =>
                PdfNavigationCensus.Resolves(source, source.NamedDestinations[name]));
            int offset = merged.Pages.Count;
            foreach (Page page in source.Pages)
            {
                merged.Pages.Add(page);
            }

            if (request.PreserveBookmarks)
            {
                bookmarks += CopyOutline(source, source.Outlines, merged.Outlines, merged, page => offset + page);
            }

            inputs.Add(PdfInfoProjection.Source(path));
        }

        var navigation = new PdfNavigationCensus(
            bookmarks,
            Math.Max(0, PdfNavigationCensus.Unresolved(merged).Links - brokenInputLinks),
            namedDestinations);
        long size = session.Outputs.Write(request.Output.Path, request.Output.Overwrite, merged, merged.Save);
        List<Warning> warnings = [];
        if (navigation.ToWarning(
                "lost their exact target: merged bookmarks open their page at Fit zoom, and named destinations are not carried into the merged document",
                "Re-create location-sensitive bookmarks (add_bookmark) and links (add_link) on the merged PDF.")
            is { } degraded)
        {
            warnings.Add(degraded);
        }

        return new PdfWriteResult
        {
            Action = "merge",
            Output = BuildOutput(request.Output.Path, "pdf", size),
            Inputs = inputs,
            License = EnvelopeParts.License(state),
            Warnings = warnings.Count == 0 ? null : warnings,
        };
    }
}
