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
                ResourceBudgetCapabilities.Domain(PdfBudgetDomains.Pages, 10_000, 100_000, "items", "post-load"),
                ResourceBudgetCapabilities.Domain(PdfBudgetDomains.Objects, 1_000_000, 5_000_000, "items", "post-load"),
            ],
            Engine = ProductEngineCapabilities.LicenseAware(
                "aspose",
                ProductBuildMetadata.EngineName,
                ProductBuildMetadata.SdkVersion),
            AvailableEngines = ["aspose"],
    };

    public ProductDefinition Define() =>
        Aspose.Cli.Sdk.Extensibility.Product.Define<PdfSession>(Manifest)
            .Formats(PdfFormats.Definitions)
            .Diagnostics(PdfDiagnostics.All)
            .Json(ProductJsonContext.Definition)
            .View(new PdfViewAdapter())
            // Only the EVALUATION_LIMIT refusal; the write pipeline discloses evaluation output.
            .Guard(static (session, run) => PdfEvaluation.Run(session.Outputs, run))
            .Describe("PDF automation with page, security and fixed-layout semantics.", Help)
            .Command(InspectCommand.Create, PdfInfo.Run)
            .Group("query", "Query bounded PDF pages, forms or text matches.", static query => query
                .Command(ReadCommand.Create, PdfRead.Run)
                .Command(FormsCommand.Create, PdfForms.Read)
                .Command(SearchCommand.Create, PdfSearch.Run))
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
            "pdf inspect report.pdf --preview --detail permissions forms signatures",
            "pdf query pages report.pdf --pages 1-5 --mode layout --output json",
            "pdf edit report.pdf --ops ops.json --out reviewed.pdf",
            "pdf sign reviewed.pdf --certificate signer.pfx --certificate-password-env PDF_SIGNING_PASSWORD --out approved.pdf",
        ],
        [
            CommandHelpLink.Docs(Manifest, "editing", "fixed-layout operations and safe mutation"),
            CommandHelpLink.Docs(Manifest, "verification", "read-back, rendering and PDF/A evidence"),
            CommandHelpLink.Schema<PdfOp>("the operation JSON schema"),
        ]);
}
