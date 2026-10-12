using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Pdf;
using static Aspose.Cli.Product.Pdf.Engine.PdfEngineSupport;

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
        var inputs = new List<InputInfo>(request.InputPaths.Count);
        using var merged = new Document();
        int bookmarks = 0;
        int brokenInputLinks = 0;
        int namedDestinations = 0;
        List<Warning> warnings = [];
        var security = new List<PdfInputSecurity>(request.InputPaths.Count);
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

        var navigation = new PdfNavigationCensus(
            bookmarks,
            Math.Max(0, PdfNavigationCensus.Unresolved(merged).Links - brokenInputLinks),
            namedDestinations);
        (PdfOutputPasswords? passwords, Warning? kept) = PdfOutputEncryption.Kept(request.EncryptPassword, request.Password, security);
        bool encrypted = PdfOutputEncryption.Encrypted(merged, passwords);
        PublishedFile written = session.Outputs.Write(request.Output.Path, request.Output.Overwrite, merged,
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
}
