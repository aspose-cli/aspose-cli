using System.Text;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Rendering;
using Aspose.Cli.Sdk.Results;
using Aspose.Pdf;
using Aspose.Pdf.Devices;
using Aspose.Pdf.Text;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>
/// <c>pdf convert</c>: a PDF, or an HTML page the engine imports, saved in another format with the
/// engine's call for it: Document.Save with the format's save options; for PDF/A,
/// Document.Convert(log, format, convertErrorAction) and then Document.Save; for TIFF the engine's
/// TiffDevice; for text the engine's TextAbsorber over every page.
/// </summary>
internal static class PdfConvert
{
    internal static PdfConvertResult Run(PdfSession session, PdfConvertRequest request)
    {
        LicenseState state = session.Outputs.License;
        bool html = PdfFormats.IsHtmlInput(request.Input);
        using HtmlImportResources? resources = html ? new HtmlImportResources(request.Input, session.Budgets) : null;
        using LoadedPdf loaded = resources is null
            ? session.Loader.OpenForConvert(request.Input, request.Password)
            : session.Loader.OpenHtml(request.Input, resources);
        Document document = loaded.Document;
        PdfEngineFormat target = PdfEngineFormats.Of(request.Output.Format.Id);
        string? log = null;
        bool? converted = null;
        void Write(string temp)
        {
            switch (target.Write)
            {
                case PdfEngineWrite.Archive:
                    using (var stream = new MemoryStream())
                    {
                        converted = document.Convert(stream, target.Archive!.Value, ErrorAction(request.ConvertErrorAction!));
                        log = Encoding.UTF8.GetString(stream.ToArray());
                    }

                    document.Save(temp);
                    break;
                case PdfEngineWrite.Tiff:
                    Tiff(session, document, request, temp);
                    break;
                case PdfEngineWrite.Text:
                    var absorber = new TextAbsorber();
                    document.Pages.Accept(absorber);
                    File.WriteAllText(temp, absorber.Text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                    break;
                default:
                    if (target.Options is { } options)
                    {
                        document.Save(temp, options(request.Pages?.Resolve(document.Pages.Count)));
                    }
                    else
                    {
                        document.Save(temp, target.Save!.Value);
                    }
                    break;
            }

            resources?.ThrowIfFailed();
        }

        // A format whose save writes files beside the output, such as Markdown's pictures, is published with them as one set.
        IReadOnlyList<OutputInfo> outputs = request.Output.Format.CompanionFiles
            ? session.Outputs.Write(request.Output, document, encrypted: false, Write)
            : [FileObjects.Output(
                session.Outputs.Write(request.Output.Path, request.Output.Format, request.Output.Overwrite, document, Write,
                    rendering: target.Write is PdfEngineWrite.Tiff or PdfEngineWrite.Text),
                request.Output.Format,
                encrypted: false)];
        return new PdfConvertResult
        {
            Input = FileObjects.Input(request.Input, html ? "html" : loaded.FormatId, loaded.Protection.Encrypted),
            Outputs = [.. outputs.Select(static output => new PdfConvertOutput(output))],
            Pages = request.Pages?.Text,
            Converted = converted,
            ConversionLog = log,
            License = EnvelopeParts.License(state),
            Warnings = resources?.Warnings is { Count: > 0 } warnings ? warnings : null,
        };
    }

    /// <summary>TiffDevice.Process over the pages --pages gives, or every page, at the default resolution.</summary>
    private static void Tiff(PdfSession session, Document document, PdfConvertRequest request, string path)
    {
        IReadOnlyList<int> pages = request.Pages?.Resolve(document.Pages.Count) ?? [.. Enumerable.Range(1, document.Pages.Count)];
        foreach (int number in pages)
        {
            PdfRaster.EnsurePageFits(session.Budgets, document.Pages[number], RenderPixelGuard.DefaultDpi);
        }

        using FileStream stream = File.Create(path);
        new TiffDevice(new Resolution(RenderPixelGuard.DefaultDpi), new TiffSettings()).Process(document, pages[0], pages[^1], stream);
    }

    /// <summary>The member of the engine's ConvertErrorAction that <paramref name="name"/> names.</summary>
    private static ConvertErrorAction ErrorAction(string name) =>
        Enum.GetNames<ConvertErrorAction>().Contains(name, StringComparer.Ordinal)
            ? Enum.Parse<ConvertErrorAction>(name)
            : throw CliErrors.OptionInvalid("--convert-error-action", $"'{name}' is no member of ConvertErrorAction; it is one of: {string.Join(", ", Enum.GetNames<ConvertErrorAction>())}");
}
