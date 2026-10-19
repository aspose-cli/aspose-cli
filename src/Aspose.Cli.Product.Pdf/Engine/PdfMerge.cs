using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Pdf;
using static Aspose.Cli.Product.Pdf.Engine.PdfAssembly;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>Merges PDF inputs into one document: <c>pdf merge</c>.</summary>
internal static class PdfMerge
{
    internal static PdfMergeResult Run(PdfSession session, PdfMergeRequest request)
    {
        if (request.InputPaths.Count < 2)
        {
            throw CliErrors.Usage(["Pass at least two PDF inputs to merge."]);
        }

        LicenseState state = session.Outputs.License;
        if (state == LicenseState.Evaluation)
        {
            RefuseMoreThanEvaluationReads(session, request);
        }

        var inputs = new List<InputInfo>(request.InputPaths.Count);
        using var merged = new Document();
        int bookmarks = 0;
        int brokenInputLinks = 0;
        int namedDestinations = 0;
        List<Warning> warnings = [];
        var security = new List<PdfInputSecurity>(request.InputPaths.Count);
        var unlabelled = new List<int>();
        foreach (string path in request.InputPaths)
        {
            using LoadedPdf loaded = session.Loader.Open(path, request.Password);
            security.Add(PdfInputSecurity.Of(loaded));
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

            // The merged document is described as its first input is.
            if (offset == 0)
            {
                CopyInformation(source, merged);
            }

            if (!CopyPageLabels(source, merged, [.. Enumerable.Range(1, source.Pages.Count)], offset) && offset > 0)
            {
                unlabelled.Add(offset);
            }

            if (request.PreserveBookmarks)
            {
                bookmarks += CopyOutline(source, source.Outlines, merged.Outlines, merged, page => offset + page);
            }

            // An attachment keeps the name it has in the first input that holds that name.
            warnings.AddRange(CopyAttachments(source, merged).Select(name =>
                new Warning(WarningCodes.LossyConversion, $"Attachment '{name}' of '{path}' was left out: an earlier input has an attachment of that name, which the merged document keeps.")
                {
                    Location = $"{path} attachment {name}",
                    Hint = "Rename the attachment in that input (delete_attachment, then add_attachment with another name) and merge again, or deliver the file alongside the merged PDF.",
                    AffectsCompleteness = true,
                }));
            if (PortfolioNotKept(source, path) is { } portfolio)
            {
                warnings.Add(portfolio);
            }

            inputs.Add(PdfInfoProjection.Source(path, loaded));
        }

        // Once any input labels its pages, an input without labels numbers its pages from 1 as
        // it did on its own, rather than continuing the previous input's numbering.
        if (merged.PageLabels.GetPages().Length > 0)
        {
            foreach (int offset in unlabelled)
            {
                merged.PageLabels.UpdateLabel(offset, new PageLabel { NumberingStyle = NumberingStyle.NumeralsArabic, StartingValue = 1 });
            }
        }

        var navigation = new PdfNavigationCensus(
            bookmarks,
            Math.Max(0, PdfNavigationCensus.Unresolved(merged).Links - brokenInputLinks),
            namedDestinations);
        (PdfOutputPasswords? passwords, Warning? kept) = PdfOutputEncryption.Kept(request.EncryptPassword, request.Password, security);
        bool encrypted = PdfOutputEncryption.Encrypted(merged, passwords);
        PublishedFile written = session.Outputs.Write(request.Output.Path, request.Output.Format, request.Output.Overwrite, merged,
            path => PdfOutputEncryption.Save(merged, path, passwords), encrypted: encrypted);
        if (kept is not null)
        {
            warnings.Add(kept);
        }
        if (navigation.ToWarning(
                "lost their exact target: merged bookmarks open their page at Fit zoom, and named destinations are not carried into the merged document",
                "Re-create location-sensitive bookmarks (add_bookmark) and links (add_hyperlink) on the merged PDF.")
            is { } degraded)
        {
            warnings.Add(degraded);
        }

        return new PdfMergeResult
        {
            Output = FileObjects.Output(written, request.Output.Format, encrypted),
            Inputs = inputs,
            License = EnvelopeParts.License(state),
            Warnings = warnings.Count == 0 ? null : warnings,
        };
    }

    /// <summary>
    /// Refuses, before the engine reads past them, inputs that hold more pages in total than
    /// evaluation mode reads (<see cref="PdfEvaluation.VisiblePages"/>): the merged document
    /// would hold them all. The error states the pages the inputs hold and offers a merge of
    /// fewer; its details are those of the engine's own page limit.
    /// </summary>
    private static void RefuseMoreThanEvaluationReads(PdfSession session, PdfMergeRequest request)
    {
        int total = 0;
        foreach (string path in request.InputPaths)
        {
            using LoadedPdf loaded = session.Loader.Open(path, request.Password);
            total += loaded.Document.Pages.Count;
        }

        if (total <= PdfEvaluation.VisiblePages)
        {
            return;
        }

        const int limit = PdfEvaluation.VisiblePages;
        IFormatProvider invariant = System.Globalization.CultureInfo.InvariantCulture;
        throw CliErrors.EvaluationLimit(
            string.Create(invariant, $"Aspose.PDF evaluation mode reads only the first {limit} pages of a document, and the inputs hold {total} pages in total, so the merge stopped and wrote nothing."),
            "Aspose.PDF",
            string.Create(invariant, $"merge inputs of at most {limit} pages in total; these hold {total} pages"),
            new JsonObject { ["limit"] = PdfEvaluation.VisiblePages, ["unit"] = "pages" });
    }
}
