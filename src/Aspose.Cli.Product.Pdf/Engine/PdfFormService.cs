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
using static Aspose.Cli.Product.Pdf.Engine.PdfArtifactSupport;
using static Aspose.Cli.Product.Pdf.Engine.PdfEngineSupport;
using static Aspose.Cli.Product.Pdf.Engine.PdfMutationSupport;
using PdfColor = Aspose.Pdf.Color;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>Owns AcroForm and XFA read, fill and export behavior.</summary>
internal sealed class PdfFormService
{
    private readonly ILicenseGate _licenseGate;
    private readonly SafeFileWriter _writer;
    private readonly PdfDocumentLoader _loader;

    internal PdfFormService(
        ILicenseGate licenseGate,
        SafeFileWriter writer,
        PdfDocumentLoader loader)
    {
        _licenseGate = licenseGate ?? throw new ArgumentNullException(nameof(licenseGate));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _loader = loader;
    }

    /// <inheritdoc />

    public PdfFormResult ReadForm(string filePath, PdfFormReadRequest request)
    {
        LicenseState state = _licenseGate.EnsureApplied();
        using LoadedPdf loaded = _loader.Open(filePath, request.Password);
        Form form = loaded.Document.Form;
        var fields = form.Fields.OrderBy(static field => field.FullName, StringComparer.Ordinal)
            .Select(static field => new PdfFormField
            {
                Name = field.FullName,
                Type = field.GetType().Name,
                Value = field.Value,
                Options = field is ChoiceField choice
                    ? choice.Options.Select(static option => option.Value ?? option.Name).ToArray()
                    : null,
                ReadOnly = field.ReadOnly,
                Required = field.Required,
                Page = field.PageIndex > 0 ? field.PageIndex : null,
            })
            .ToArray();
        return new PdfFormResult
        {
            Input = PdfInfoProjection.Source(filePath),
            Type = form.HasXfa ? "xfa" : form.Count > 0 ? "acro" : "none",
            ReadOnly = form.HasXfa,
            Fields = fields,
            License = EnvelopeParts.License(state),
        };
    }

    public PdfFormExportResult ExportForm(string filePath, PdfFormExportRequest request)
    {
        if (request.TargetFormatId is not ("json" or "fdf" or "xfdf"))
        {
            throw CliErrors.FormatUnsupported(request.TargetFormatId, ["json", "fdf", "xfdf"]);
        }

        LicenseState state = _licenseGate.EnsureApplied();
        using LoadedPdf loaded = _loader.Open(filePath, request.Password);
        EnsureAcroForm(loaded.Document);
        long size = _writer.Write(request.OutputPath, request.Overwrite, temp =>
        {
            var facade = new Aspose.Pdf.Facades.Form(loaded.Document);
            using FileStream stream = File.Create(temp);
            switch (request.TargetFormatId)
            {
                case "json": facade.ExportJson(stream, indented: true); break;
                case "fdf": facade.ExportFdf(stream); break;
                case "xfdf": facade.ExportXfdf(stream); break;
            }
        });
        return new PdfFormExportResult
        {
            Input = PdfInfoProjection.Source(filePath),
            Output = BuildOutput(request.OutputPath, request.TargetFormatId, size),
            License = EnvelopeParts.License(state),
            Warnings = OutputWarnings(state),
        };
    }
}
