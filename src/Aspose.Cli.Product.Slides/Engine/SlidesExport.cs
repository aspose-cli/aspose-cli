using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Rendering;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.Sdk.Tables;
using Aspose.Slides;
using Aspose.Slides.Export;
using static Aspose.Cli.Product.Slides.Engine.SlidesRenderSizing;
using static Aspose.Cli.Product.Slides.Engine.SlidesResultParts;
using static Aspose.Cli.Product.Slides.Engine.SlidesSlideSelection;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>
/// Conversion and rendering of a presentation or selected slides: the engine's save and the
/// engine's slide images, with the engine's default options except those a command names.
/// </summary>
internal static class SlidesExport
{
    internal static SlidesConvertResult Convert(SlidesSession session, SlidesConvertRequest request)
    {
        LicenseState state = session.Outputs.License;
        var log = new EngineWarningLog();
        using LoadedPresentation loaded = session.Loader.OpenTable(request.Input, request.Password, log);
        Presentation presentation = loaded.Presentation;
        IReadOnlyList<int>? slides = request.Slides is null ? null : ResolveSlideRange(request.Slides, presentation.Slides.Count);
        SaveFormat format = SlidesEngineFormats.SaveFormatOf(request.Output.Format.Id);
        OutputProtection protection = OutputProtection.For(request.Output.Format, request.EncryptPassword, loaded.Protection, request.Password);
        SlidesOutputProtection.Protect(presentation, protection);
        IReadOnlyList<OutputInfo> written = session.Outputs.Write(request.Output, protection.Encrypted, temp =>
        {
            log.During = "save";
            Save(presentation, temp, format, slides, log);
            loaded.Resources.ThrowIfFailed();
        });
        return new SlidesConvertResult
        {
            Input = Source(request.Input, loaded),
            Outputs = [.. written.Select(static file => new SlidesConvertOutput(file))],
            Slides = request.Slides?.Text,
            EngineWarnings = log.Warnings.Count == 0 ? null : log.Warnings,
            License = EnvelopeParts.License(state),
            Warnings = InputWarnings(loaded),
        };
    }

    /// <summary>Saves the whole presentation in <paramref name="format"/>, the engine's options set only for its warning callback.</summary>
    internal static void Save(Presentation presentation, string temp, SaveFormat format, EngineWarningLog log) =>
        Save(presentation, temp, format, slides: null, log);

    private static void Save(Presentation presentation, string temp, SaveFormat format, IReadOnlyList<int>? slides, EngineWarningLog log)
    {
        // The Markdown export makes its image folder under its base path even when it writes no
        // image; a private folder keeps it out of the working directory.
        DirectoryInfo? scratch = format == SaveFormat.Md ? Directory.CreateTempSubdirectory("aspose-cli-md-") : null;
        try
        {
            Save(presentation, temp, format, slides, Options(format, log, scratch?.FullName));
        }
        finally
        {
            scratch?.Delete(recursive: true);
        }
    }

    private static void Save(Presentation presentation, string temp, SaveFormat format, IReadOnlyList<int>? slides, ISaveOptions? options)
    {
        if (slides is not null && Exports(format))
        {
            if (options is null)
            {
                presentation.Save(temp, [.. slides], format);
            }
            else
            {
                presentation.Save(temp, [.. slides], format, options);
            }

            return;
        }

        if (slides is not null)
        {
            // A presentation format keeps the selected slides with the deck's size, properties,
            // masters and protection: the others leave this private loaded copy.
            RemoveUnselectedSlides(presentation, slides);
        }

        if (options is null)
        {
            presentation.Save(temp, format);
        }
        else
        {
            presentation.Save(temp, format, options);
        }
    }

    /// <summary>
    /// The engine's save options of a format, with nothing set but the warning callback; null for
    /// a format the engine has no options for, which reports no warning. A Markdown export writes
    /// its images under its base path, so it gets a private folder and writes the text alone.
    /// </summary>
    private static ISaveOptions? Options(SaveFormat format, EngineWarningLog log, string? basePath)
    {
        ISaveOptions? options = format switch
        {
            SaveFormat.Ppt => new PptOptions(),
            SaveFormat.Pptx or SaveFormat.Pptm => new PptxOptions(),
            SaveFormat.Pdf => new PdfOptions(),
            SaveFormat.Xps => new XpsOptions(),
            SaveFormat.Html => new HtmlOptions(),
            SaveFormat.Html5 => new Html5Options(),
            SaveFormat.Gif => new GifOptions(),
            SaveFormat.Md => new MarkdownSaveOptions { ExportType = MarkdownExportType.TextOnly, BasePath = basePath },
            _ => null,
        };
        if (options is not null)
        {
            options.WarningCallback = new SlidesWarnings(log);
        }

        return options;
    }

    // Whether the format is an export, which the engine saves selected slides of.
    private static bool Exports(SaveFormat format) =>
        format is SaveFormat.Pdf or SaveFormat.Xps or SaveFormat.Html or SaveFormat.Html5 or SaveFormat.Gif or SaveFormat.Md;

    internal static SlidesRenderResult Render(SlidesSession session, SlidesRenderRequest request)
    {
        LicenseState state = session.Outputs.License;
        var log = new EngineWarningLog();
        using LoadedPresentation loaded = session.Loader.OpenTable(request.Input, request.Password, log);
        IReadOnlyList<int> slides = RenderSelection.Resolve(request.Slides, loaded.Presentation.Slides.Count, Sdk.Errors.ErrorCodes.AddressNotFound, "slide");
        log.During = "save";
        IReadOnlyList<SlidesSlideOutput> outputs = RenderImages(session, loaded, request, slides, log);
        return new SlidesRenderResult
        {
            Input = Source(request.Input, loaded),
            Outputs = outputs,
            Dpi = !request.Output.Format.Raster || request.Width is not null
                ? null
                : request.Dpi ?? RenderPixelGuard.DefaultDpi,
            Width = request.Output.Format.Raster ? request.Width : null,
            EngineWarnings = log.Warnings.Count == 0 ? null : log.Warnings,
            License = EnvelopeParts.License(state),
            Warnings = InputWarnings(loaded),
        };
    }

    private static List<SlidesSlideOutput> RenderImages(
        SlidesSession session,
        LoadedPresentation loaded,
        SlidesRenderRequest request,
        IReadOnlyList<int> slides,
        EngineWarningLog log)
    {
        Presentation presentation = loaded.Presentation;
        float scale = RenderScale(presentation, request);
        if (request.Output.Format.Raster)
        {
            long width = (long)Math.Ceiling(presentation.SlideSize.Size.Width * scale);
            long height = (long)Math.Ceiling(presentation.SlideSize.Size.Height * scale);
            EnsureRasterFits(
                session.Budgets,
                width,
                height,
                request.Width is null ? request.Dpi ?? RenderPixelGuard.DefaultDpi : null);
        }

        string directory = request.Output.Directory;
        using OutputSet<Presentation> transaction = session.Outputs.BeginSet([directory], "slides-render");
        var targets = new List<int>(slides.Count);
        ImageFormat? image = SlidesEngineFormats.SlideImageOf(request.Output.Format.Id);
        var options = new RenderingOptions { WarningCallback = new SlidesWarnings(log) };
        foreach ((int number, string path) in slides.Zip(request.Output.Parts(slides)))
        {
            ISlide slide = presentation.Slides[number - 1];
            targets.Add(number);
            transaction.Stage(path, request.Output.Format, request.Output.Overwrite, temp =>
            {
                if (image is not { } bitmap)
                {
                    using FileStream stream = File.Create(temp);
                    slide.WriteAsSvg(stream);
                    return;
                }

                using IImage drawn = slide.GetImage(options, scale, scale);
                using FileStream outputStream = File.Create(temp);
                drawn.Save(outputStream, bitmap, quality: 92);
            });
        }

        loaded.Resources.ThrowIfFailed();
        IReadOnlyList<PublishedFile> files = transaction.Commit();
        return [.. targets.Select((number, index) => new SlidesSlideOutput(FileObjects.Output(files[index], request.Output.Format, encrypted: false)) { Slide = number })];
    }
}
