using System.Globalization;
using System.Net;
using System.Text;
using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Product.Pdf.Engine.Mapping;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Rendering;
using Aspose.Cli.Sdk.Results;
using Aspose.Pdf;
using Aspose.Pdf.Annotations;
using Aspose.Pdf.Devices;
using Aspose.Pdf.Text;
using static Aspose.Cli.Product.Pdf.Engine.PdfEngineSupport;
using DrawingImageFormat = Aspose.Pdf.Drawing.ImageFormat;

using static Aspose.Cli.Product.Pdf.Engine.PdfArtifactSupport;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>Owns transactional PDF splitting and bounded artifact extraction.</summary>
internal sealed class PdfExtractionService
{
    private readonly ILicenseGate _licenseGate;
    private readonly ResourceBudgetLedger _resourceBudgets;
    private readonly SafeFileWriter _writer;
    private readonly PdfDocumentLoader _loader;

    internal PdfExtractionService(
        ILicenseGate licenseGate,
        ResourceBudgetLedger resourceBudgets,
        SafeFileWriter writer,
        PdfDocumentLoader loader)
    {
        _licenseGate = licenseGate ?? throw new ArgumentNullException(nameof(licenseGate));
        _resourceBudgets = resourceBudgets;
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _loader = loader;
    }

    internal PdfSplitResult Split(string filePath, PdfSplitRequest request)
    {
        int modes = request.PageGroups is { Count: > 0 } ? 1 : 0;
        modes += request.Every.HasValue ? 1 : 0;
        modes += request.ByBookmarks ? 1 : 0;
        if (modes != 1)
        {
            throw CliErrors.Usage(["Choose exactly one of --pages, --every or --by-bookmarks."]);
        }

        LicenseState state = _licenseGate.EnsureApplied();
        using LoadedPdf loaded = _loader.Open(filePath, request.Password);
        IReadOnlyList<PdfArtifactSupport.SplitPart> parts = SplitParts(loaded.Document, request);
        string root = Path.GetFullPath(request.OutputDirectory);
        using var writer = new AtomicOutputSetWriter(_writer, root, "pdf-split");
        string stem = Path.GetFileNameWithoutExtension(filePath);
        var targets = new List<(PdfArtifactSupport.SplitPart Part, string Path)>();
        foreach (PdfArtifactSupport.SplitPart part in parts)
        {
            string name = SplitName(request.NameTemplate, stem, part);
            string target = Path.Combine(root, name);
            writer.Stage(target, request.Overwrite, staged =>
            {
                using Document selected = Select(loaded.Document, part.Pages);
                selected.Save(staged);
            });
            targets.Add((part, target));
        }

        IReadOnlyList<long> sizes = writer.Commit();
        return new PdfSplitResult
        {
            Input = PdfInfoProjection.Source(filePath),
            Outputs = targets.Select((item, index) => new PdfSplitOutput
            {
                Index = item.Part.Index,
                Pages = PageRangeText(item.Part.Pages),
                Bookmark = item.Part.Bookmark,
                Output = BuildOutput(item.Path, "pdf", sizes[index]),
            }).ToArray(),
            License = EnvelopeParts.License(state),
            Warnings = OutputWarnings(state),
        };
    }

    internal PdfExtractResult Extract(string filePath, PdfExtractRequest request)
    {
        if (!PdfExtractKinds.All.Contains(request.What, StringComparer.Ordinal))
        {
            throw CliErrors.OptionInvalid(
                "--what",
                $"unknown extraction kind '{request.What}'",
                "Use images, attachments, text or tables.");
        }

        if (request.What == "attachments" && request.Pages is not null)
        {
            throw CliErrors.OptionInvalid(
                "--pages",
                "attachments belong to the document rather than individual pages",
                "Omit --pages when extracting attachments.");
        }

        LicenseState state = _licenseGate.EnsureApplied();
        using LoadedPdf loaded = _loader.Open(filePath, request.Password);
        IReadOnlyList<int> pages = request.Pages?.Resolve(loaded.Document.Pages.Count)
            ?? Enumerable.Range(1, loaded.Document.Pages.Count).ToArray();
        using var guard = new ExtractionGuard(
            _resourceBudgets,
            request.OutputDirectory);
        IReadOnlyList<PdfExtractedItem> items = request.What switch
        {
            "images" => ExtractImages(loaded.Document, pages, guard),
            "attachments" => ExtractAttachments(loaded.Document, guard),
            "text" => ExtractTextArtifact(loaded.Document, pages, guard),
            "tables" => ExtractTables(loaded.Document, pages, guard),
            _ => throw new InvalidOperationException("The extraction registry and handler are out of sync."),
        };
        guard.Commit();
        return new PdfExtractResult
        {
            Input = PdfInfoProjection.Source(filePath),
            What = request.What,
            Items = items,
            License = EnvelopeParts.License(state),
            Warnings = OutputWarnings(state),
        };
    }

}

