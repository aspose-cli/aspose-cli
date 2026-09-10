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

    public PdfFormResult ReadForm(string filePath, PdfFormReadRequest request) =>
        PdfErrorTranslator.Execute("query forms", () => ReadFormCore(filePath, request));

    /// <inheritdoc />
    public PdfEditResult FillForm(string filePath, PdfFormFillRequest request) =>
        PdfErrorTranslator.Execute("edit", () => FillFormCore(filePath, request));

    /// <inheritdoc />
    public PdfFormExportResult ExportForm(string filePath, PdfFormExportRequest request) =>
        PdfErrorTranslator.Execute("extract forms", () => ExportFormCore(filePath, request));

    /// <inheritdoc />

    private PdfFormResult ReadFormCore(string filePath, PdfFormReadRequest request)
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

    private PdfEditResult FillFormCore(string filePath, PdfFormFillRequest request)
    {
        EnsurePdfOutput(request.OutputPath);
        FileWritePrecondition precondition = FileWritePrecondition.Capture(filePath);
        LicenseState state = _licenseGate.EnsureApplied();
        using LoadedPdf loaded = _loader.Open(filePath, request.Password);
        EnsureAcroForm(loaded.Document);
        SourceInfo input = PdfInfoProjection.Source(filePath, includeFingerprint: true);
        FileFingerprints.EnsureUnchanged(filePath, precondition.Fingerprint, input.Fingerprint!);
        var outcomes = new List<BoundedOperationOutcome>();
        int index = 0;
        foreach ((string name, string value) in request.Values.OrderBy(static item => item.Key, StringComparer.Ordinal))
        {
            Field? field = loaded.Document.Form.Fields.FirstOrDefault(
                item => string.Equals(item.FullName, name, StringComparison.Ordinal));
            if (field is null)
            {
                throw InvalidOp(index, "set_form_field", $"Form field '{name}' was not found.");
            }

            field.Value = value;
            outcomes.Add(new BoundedOperationOutcome
            {
                Id = $"op-{index + 1:D4}",
                Index = index++,
                Op = "set_form_field",
                Status = OpStatuses.Ok,
                ItemsAffected = 1,
                Targets = ["pdf/form"],
            });
        }

        if (request.Flatten)
        {
            int count = loaded.Document.Form.Count;
            loaded.Document.Form.Flatten();
            outcomes.Add(new BoundedOperationOutcome
            {
                Id = $"op-{index + 1:D4}",
                Index = index,
                Op = "flatten_forms",
                Status = OpStatuses.Ok,
                ItemsAffected = count,
                Targets = ["pdf/form"],
            });
        }

        long size = _writer.Write(
            request.OutputPath,
            request.Overwrite,
            backupPath: null,
            precondition,
            temp =>
            {
                loaded.Document.Save(temp);
                using LoadedPdf reopened = _loader.Open(temp, request.Password);
            }).SizeBytes;
        return new PdfEditResult
        {
            Input = input,
            Output = BuildOutput(request.OutputPath, "pdf", size) with
            {
                Fingerprint = FileFingerprints.Capture(request.OutputPath),
            },
            DryRun = false,
            Applied = outcomes,
            Mutation = new MutationReceipt { Verification = "reopened" },
            License = EnvelopeParts.License(state),
            Warnings = OutputWarnings(state),
        };
    }

    private PdfFormExportResult ExportFormCore(string filePath, PdfFormExportRequest request)
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
