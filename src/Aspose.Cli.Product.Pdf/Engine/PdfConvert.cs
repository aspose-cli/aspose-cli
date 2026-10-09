using System.Text;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Pdf;
using Aspose.Pdf.Devices;
using static Aspose.Cli.Product.Pdf.Engine.PdfEngineSupport;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>Converts selected PDF pages to a supported format: <c>pdf convert</c>.</summary>
internal static class PdfConvert
{
    /// <summary>The resolution of the page images convert writes.</summary>
    private const int ImageDpi = 192;

    internal static PdfConvertResult Run(PdfSession session, PdfConvertRequest request)
    {
        string filePath = request.Input;
        LicenseState state = session.Outputs.License;
        using LoadedPdf loaded = session.Loader.Open(filePath, request.Password);
        IReadOnlyList<int> pages = request.Pages?.Resolve(loaded.Document.Pages.Count)
            ?? Enumerable.Range(1, loaded.Document.Pages.Count).ToArray();
        List<Warning> warnings = [];
        PdfEngineFormat target = PdfEngineFormats.Of(request.Output.Format.Id);
        IReadOnlyList<OutputInfo> outputs = target.Write switch
        {
            PdfEngineWrite.PageImage => ConvertPages(session, loaded.Document, pages, request),
            PdfEngineWrite.Tiff => [ConvertTiff(session, loaded.Document, pages, request)],
            PdfEngineWrite.Text => [ConvertText(session, loaded.Document, pages, request)],
            PdfEngineWrite.Archive => [ConvertPdfa(session, loaded.Document, pages, request, state, warnings)],
            PdfEngineWrite.Document => [ConvertDocument(session, loaded.Document, pages, request, state, warnings)],
            _ => throw new InvalidOperationException($"'{target.Id}' is not a PDF convert format."),
        };

        if (target.Lossy)
        {
            warnings.Add(new Warning
            {
                Code = WarningCodes.LossyConversion,
                Message = $"PDF conversion to {request.Output.Format.Id} may not preserve every layout or interactive feature.",
                Hint = request.Output.Format.Id == "html"
                    ? "Open the HTML in a browser and compare it with the PDF before relying on exact pagination, forms or annotations; review does not lay out HTML made from a PDF, so review the PDF itself."
                    : "Inspect the produced file before relying on exact pagination, forms or annotations.",
            });
        }

        return new PdfConvertResult
        {
            Input = PdfInfoProjection.Source(filePath),
            Outputs = outputs,
            Pages = request.Pages?.Text,
            License = EnvelopeParts.License(state),
            Warnings = warnings.Count == 0 ? null : warnings,
        };
    }

    /// <summary>
    /// Converts the opened document itself, which this command never saves back, so the
    /// document properties reach formats that carry them; a page copy, which evaluation mode
    /// may need, has none.
    /// </summary>
    private static OutputInfo ConvertDocument(
        PdfSession session,
        Document document,
        IReadOnlyList<int> pages,
        PdfConvertRequest request,
        LicenseState state,
        List<Warning> warnings)
    {
        using Document? copied = NarrowToSelection(document, pages, state, warnings, out PdfNavigationCensus degraded);
        Document selected = copied ?? document;
        if (UnselectedNavigation(degraded) is { } navigation)
        {
            warnings.Add(navigation);
        }

        SaveFormat format = PdfEngineFormats.Save(request.Output.Format.Id);
        long size = session.Outputs.Write(
            request.Output.Path,
            request.Output.Overwrite,
            selected,
            temp =>
            {
                if (request.Output.Format.Id == "html")
                {
                    // The HTML writer leaves <title> empty unless it is given the PDF title.
                    selected.Save(temp, new HtmlSaveOptions
                    {
                        PartsEmbeddingMode = HtmlSaveOptions.PartsEmbeddingModes.EmbedAllIntoHtml,
                        Title = selected.Info.Title ?? string.Empty,
                    });
                }
                else
                {
                    selected.Save(temp, format);
                }
            });
        return BuildOutput(request.Output.Path, request.Output.Format.Id, size);
    }

    /// <summary>
    /// Narrows the opened document to the pages --pages selected, and counts the navigation
    /// that led to the others. Evaluation mode cannot delete a page after the ones it shows,
    /// so there the selected pages of a longer document are copied into a new document, which
    /// is returned, and what the copy leaves behind is disclosed.
    /// </summary>
    private static Document? NarrowToSelection(
        Document document,
        IReadOnlyList<int> pages,
        LicenseState state,
        List<Warning> warnings,
        out PdfNavigationCensus degraded)
    {
        if (state == LicenseState.Evaluation
            && document.Pages.Count > PdfEvaluation.VisiblePages
            && pages.Count < document.Pages.Count)
        {
            degraded = default;
            warnings.Add(new Warning
            {
                Code = WarningCodes.LossyConversion,
                Message = $"Evaluation mode cannot remove the pages after page {PdfEvaluation.VisiblePages} from the document, so the selected pages were copied into a new one, which has none of the document's bookmarks, attachments and document properties, such as its title, author or subject.",
                Hint = "Apply an Aspose.PDF license to keep them.",
            });
            return Select(document, pages);
        }

        degraded = DeleteUnselected(document, pages);
        return null;
    }

    /// <summary>
    /// Deletes the pages --pages did not select from the opened document, and counts the
    /// navigation that led to them.
    /// </summary>
    private static PdfNavigationCensus DeleteUnselected(Document document, IReadOnlyList<int> pages)
    {
        int[] excluded = Enumerable.Range(1, document.Pages.Count).Except(pages).ToArray();
        if (excluded.Length == 0)
        {
            return default;
        }

        PdfNavigationCensus before = PdfNavigationCensus.Unresolved(document);
        document.Pages.Delete(excluded);
        return PdfNavigationCensus.Degraded(before, PdfNavigationCensus.Unresolved(document));
    }

    private static Warning? UnselectedNavigation(PdfNavigationCensus degraded) => degraded.ToWarning(
        "lead to pages that --pages did not select",
        "Select every page the navigation needs, or delete those bookmarks and links in a 'pdf edit' batch before converting.");

    private static OutputInfo ConvertText(PdfSession session, Document source, IReadOnlyList<int> pages, PdfConvertRequest request)
    {
        long size = session.Outputs.Write(request.Output.Path, request.Output.Overwrite, source, temp =>
            File.WriteAllText(temp, DocumentText(source, pages), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)), rendering: true, pages: pages);
        return BuildOutput(request.Output.Path, "txt", size);
    }

    /// <summary>
    /// Converts the opened document itself, which this command never saves back, so the
    /// outline, attachments, metadata and page labels reach the archive; a page copy carries
    /// none of them, which evaluation mode discloses when it has to copy pages. The engine
    /// removes what the profile does not allow, and each removed attachment or bookmark is
    /// reported on its own.
    /// </summary>
    private static OutputInfo ConvertPdfa(
        PdfSession session,
        Document opened,
        IReadOnlyList<int> pages,
        PdfConvertRequest request,
        LicenseState state,
        List<Warning> warnings)
    {
        string profile = request.Output.Format.Id;
        PdfFormat format = PdfEngineFormats.Archive(profile)
            ?? throw new InvalidOperationException($"'{profile}' is not a PDF/A profile.");

        // Navigation is counted around the page deletion alone; a bookmark the conversion
        // removes is reported with the outline below.
        using Document? copied = NarrowToSelection(opened, pages, state, warnings, out PdfNavigationCensus degraded);
        Document document = copied ?? opened;

        // PDF/A forbids encryption; the engine cannot convert an encrypted document.
        if (document.IsEncrypted)
        {
            document.Decrypt();
        }

        string[] attachments = AttachmentNames(document);
        FileSpecification[] untyped = document.EmbeddedFiles
            .Where(static file => string.IsNullOrEmpty(file.MIMEType))
            .ToArray();
        int bookmarks = Editing.PdfMutationSupport.CountOutline(document.Outlines);
        int changed = 0;
        long size = session.Outputs.Write(request.Output.Path, request.Output.Overwrite, document, temp =>
        {
            using var log = new MemoryStream();
            PdfComplianceLog.EnsureConverted(
                document.Convert(log, format, ConvertErrorAction.Delete), log, profile);
            // The identification it writes and the attachments, reported below, are not counted.
            changed = PdfComplianceLog.Parse(log)
                .Count(static problem => problem.Section is not ("Metadata" or "EmbeddedFiles"));
            // Only PDF/A-3 keeps attachments that are not PDF documents.
            if (format == PdfFormat.PDF_A_3B)
            {
                LabelUntypedAttachments(document, untyped);
            }

            document.Save(temp);
            using LoadedPdf saved = session.Loader.OpenPublishedCandidate(temp, password: null);
            using var validation = new MemoryStream();
            PdfComplianceLog.EnsureConformant(
                saved.Document.Validate(validation, format), validation, profile, page => ButtonFields(saved.Document, page));
        });

        foreach (string removed in attachments.Except(AttachmentNames(document), StringComparer.Ordinal))
        {
            warnings.Add(new Warning
            {
                Code = WarningCodes.LossyConversion,
                Location = $"attachment {removed}",
                Message = $"Attachment '{removed}' was removed: {AttachmentRule(profile)}.",
                Hint = profile == "pdfa-3b"
                    ? "Deliver the file alongside the archive."
                    : "Convert to pdfa-3b to keep attachments, or deliver the file alongside the archive.",
            });
        }

        int lostBookmarks = bookmarks - Editing.PdfMutationSupport.CountOutline(document.Outlines);
        if (lostBookmarks > 0)
        {
            warnings.Add(new Warning
            {
                Code = WarningCodes.LossyConversion,
                Location = "outline",
                Message = $"{lostBookmarks} bookmark(s) were removed by the conversion to {profile}.",
                Hint = "Compare 'pdf inspect --detail outline' of both files and re-create the missing bookmarks with add_bookmark.",
            });
        }

        if (UnselectedNavigation(degraded) is { } navigation)
        {
            warnings.Add(navigation);
        }

        if (changed > 0)
        {
            warnings.Add(new Warning
            {
                Code = WarningCodes.LossyConversion,
                Message = $"Conversion to {profile} changed {changed} item(s) the profile does not allow, such as fonts that were not embedded, transparency, actions or prohibited annotation entries.",
                Hint = $"Run 'pdf validate <original> --profile {profile}' to list them, and compare the pages of both files.",
            });
        }

        return BuildOutput(request.Output.Path, profile, size);
    }

    /// <summary>
    /// The full names of the check boxes and radio groups on a page whose normal appearance is
    /// one stream rather than one per state, which clause 6.3.3 rejects; the form lists a radio
    /// group as its buttons. The SDK names an appearance per state <c>N.&lt;state&gt;</c>.
    /// </summary>
    private static IEnumerable<string> ButtonFields(Document document, int page) => document.Form.Fields
        .Where(field => field is (Aspose.Pdf.Forms.CheckboxField or Aspose.Pdf.Forms.RadioButtonField or Aspose.Pdf.Forms.RadioButtonOptionField)
            && field.PageIndex == page
            && (PdfForms.RadioGroup(field) ?? field).Appearance.Keys.Contains("N"))
        .Select(static field => field.FullName);

    /// <summary>
    /// The conversion labels every attachment that had no media type <c>application/pdf</c>
    /// (PDF-PDFA-ATTACHMENT-TYPE); one that is not a PDF is labelled
    /// <c>application/octet-stream</c>, the type of unidentified data, instead.
    /// </summary>
    private static void LabelUntypedAttachments(Document document, IReadOnlyCollection<FileSpecification> untyped)
    {
        foreach (FileSpecification file in document.EmbeddedFiles)
        {
            if (untyped.Contains(file) && !StartsAsPdf(file))
            {
                file.MIMEType = "application/octet-stream";
            }
        }
    }

    private static bool StartsAsPdf(FileSpecification file)
    {
        Stream? contents = file.Contents;
        if (contents is null)
        {
            return false;
        }

        if (contents.CanSeek)
        {
            contents.Position = 0;
        }

        return ContainerSignatures.HasPdfHeader(ContainerSignatures.ReadPrefix(contents));
    }

    private static string[] AttachmentNames(Document document) =>
        document.EmbeddedFiles
            .Select(static file => file.UnicodeName ?? file.Name)
            .Where(static name => !string.IsNullOrEmpty(name))
            .ToArray();

    /// <summary>Why the profile does not keep an attachment.</summary>
    private static string AttachmentRule(string profile) => profile switch
    {
        "pdfa-1b" => "PDF/A-1 does not allow attachments",
        "pdfa-2b" => "PDF/A-2 allows only attachments that are PDF/A documents",
        _ => $"the engine could not make it conform to {profile}",
    };

    private static IReadOnlyList<OutputInfo> ConvertPages(
        PdfSession session,
        Document document,
        IReadOnlyList<int> pages,
        PdfConvertRequest request)
    {
        string outputDirectory = request.Output.Directory;
        using OutputSet<Document> writer = session.Outputs.BeginSet([outputDirectory], "pdf-convert");
        var paths = new List<string>(pages.Count);
        foreach (int pageNumber in pages)
        {
            string path = request.Output.Part(PdfRaster.PagePart, pageNumber, pages.Count);
            writer.Stage(path, request.Output.Overwrite, document, temp =>
            {
                using FileStream stream = File.Create(temp);
                PdfRaster.RenderPage(session.Budgets, document, pageNumber, request.Output.Format.Id, ImageDpi, stream);
            }, rendering: true, pages: [pageNumber]);
            paths.Add(path);
        }

        IReadOnlyList<long> sizes = writer.Commit();
        return paths.Select((path, index) =>
            BuildOutput(path, request.Output.Format.Id, sizes[index])).ToArray();
    }

    private static OutputInfo ConvertTiff(
        PdfSession session,
        Document source,
        IReadOnlyList<int> pages,
        PdfConvertRequest request)
    {
        foreach (int pageNumber in pages)
        {
            PdfRaster.EnsurePageFits(session.Budgets, source.Pages[pageNumber], ImageDpi);
        }

        using Document selected = Select(source, pages);
        long size = session.Outputs.Write(request.Output.Path, request.Output.Overwrite, selected, temp =>
        {
            using FileStream stream = File.Create(temp);
            var device = new TiffDevice(new Resolution(ImageDpi), new TiffSettings());
            device.Process(selected, 1, selected.Pages.Count, stream);
        }, rendering: true);
        return BuildOutput(request.Output.Path, "tiff", size);
    }
}
