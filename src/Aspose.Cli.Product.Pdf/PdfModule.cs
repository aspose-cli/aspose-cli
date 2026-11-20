using Aspose.Cli.Product.Pdf.Commands;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Pdf;

/// <summary>Pure build-time definition and deferred activation for PDF.</summary>
public sealed class PdfModule : IProductModule
{
    public static ProductManifest Manifest { get; } = new()
    {
        Id = ProductBuildMetadata.ProductId,
        DisplayName = ProductBuildMetadata.DisplayName,
        DisplayOrder = ProductBuildMetadata.DisplayOrder,
        IsDefaultCandidate = ProductBuildMetadata.IsDefaultCandidate,
            ResourceBudgets =
            [
                ResourceBudget.Domain(PdfBudgetDomains.Pages, 10_000, 100_000, "items", "post-load"),
                ResourceBudget.Domain(PdfBudgetDomains.Objects, 1_000_000, 5_000_000, "items", "post-load"),
            ],
            Engine = new ProductEngine
            {
                Sdk = ProductBuildMetadata.EngineName,
                SdkVersion = ProductBuildMetadata.SdkVersion,
            },
    };

    /// <summary>Loads the PDF object table, which names engine types: the commands call it only when they run.</summary>
    internal static Aspose.Cli.Sdk.Tables.DocumentTable Table() => Engine.Mapping.PdfTable.Definition;

    public ProductDefinition Define() =>
        Aspose.Cli.Sdk.Extensibility.Product.Define<PdfSession>(Manifest)
            .Formats(PdfFormats.Definitions)
            .Diagnostics(PdfDiagnostics.All)
            .Json(ProductJsonContext.Definition)
            .Describe("Read, edit, convert and render PDF documents through the Aspose.PDF object model.", Help)
            .Command(ReadCommand.Create, PdfRead.Run)
            .Command(SearchCommand.Create, PdfSearch.Run)
            .Command(ConvertCommand.Create, PdfConvert.Run)
            .Command(RenderCommand.Create, PdfRender.Run)
            .Command(CreateCommand.Create, PdfCreate.Run)
            .Command(MergeCommand.Create, PdfMerge.Run)
            .Command(SplitCommand.Create, PdfSplit.Run)
            .Command(ExtractCommand.Create, PdfExtract.Run)
            .Command(EditCommand.Create, PdfEdit.Run)
            .Command(ValidateCommand.Create, PdfValidate.Run)
            .Command(SignCommand.Create, PdfSign.Run)
            .Activator(PdfActivation.Activate)
            .Build();

    private static CommandHelp Help() => new(
        [
            "pdf read report.pdf",
            "pdf read report.pdf --at page/1/text",
            "pdf edit form.pdf --ops ops.json --out filled.pdf",
        ],
        [
            new("aspose-cli schema v2/pdf/ops/directory", "the object table: kinds, addresses, rows, adds and actions"),
        ]);
}
