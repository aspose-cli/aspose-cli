using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Slides;
using Aspose.Slides.Export;
using static Aspose.Cli.Product.Slides.Engine.SlidesEngineSupport;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>Conversion and rendering of a presentation or selected slides.</summary>
internal static class SlidesExport
{
    /// <summary>Marks the slide number in a multi-slide output name: <c>deck.s3.png</c>.</summary>
    private const string SlidePartMarker = "s";

    internal static SlidesConvertResult Convert(SlidesSession session, PresentationConvertRequest request)
    {
        LicenseState state = session.Outputs.License;
        using LoadedPresentation loaded = session.Loader.Open(request.Input, request.Password);
        IReadOnlyList<int>? slides = request.Slides is null
            ? null
            : ResolveSlideRange(request.Slides, loaded.Presentation.Slides.Count);
        IReadOnlyList<OutputInfo> outputs = ConvertOutputs(session, loaded, request, slides);
        List<Warning> warnings = BuildConvertWarnings(state, loaded, request.Output.Format.Id);
        return new SlidesConvertResult
        {
            Input = Source(request.Input, loaded.FormatId),
            Outputs = outputs,
            Slides = request.Slides?.Text,
            License = EnvelopeParts.License(state),
            Warnings = warnings.Count == 0 ? null : warnings,
        };
    }

    private static IReadOnlyList<OutputInfo> ConvertOutputs(
        SlidesSession session,
        LoadedPresentation loaded,
        PresentationConvertRequest request,
        IReadOnlyList<int>? slides)
    {
        Presentation presentation = loaded.Presentation;
        if (SlidesEngineFormats.IsSlideImage(request.Output.Format.Id))
        {
            IReadOnlyList<int> selected = slides ?? AllSlides(presentation.Slides.Count);
            return RenderImages(
                    session,
                    loaded,
                    new PresentationRenderRequest { Input = request.Input, Output = request.Output },
                    selected,
                    "slides-convert")
                .Select(static item => item.Output).ToArray();
        }
        else
        {
            SaveFormat format = SlidesEngineFormats.SaveFormatOf(request.Output.Format.Id);
            return session.Outputs.Write(request.Output, presentation, temp =>
            {
                Save(temp);
                loaded.Resources.ThrowIfFailed();
            });

            void Save(string temp)
            {
                if (slides is null)
                {
                    Encrypt(presentation, request.EncryptPassword);
                    presentation.Save(temp, format);
                    return;
                }

                if (format is SaveFormat.Pdf or SaveFormat.Xps or SaveFormat.Html
                    or SaveFormat.Html5 or SaveFormat.Tiff or SaveFormat.Gif)
                {
                    presentation.Save(temp, slides.ToArray(), format);
                    return;
                }

                // Editable formats keep the source's size, properties, masters and protection:
                // the unselected slides leave this private loaded copy instead of cloning into a new deck.
                RemoveUnselectedSlides(presentation, slides);
                Encrypt(presentation, request.EncryptPassword);
                presentation.Save(temp, format);
            }
        }
    }

    private static List<Warning> BuildConvertWarnings(
        LicenseState state,
        LoadedPresentation loaded,
        string targetFormatId)
    {
        var warnings = WrittenWarnings(state, loaded, textRead: targetFormatId == "md")?.ToList() ?? [];
        if (targetFormatId is "html" or "html5" or "md")
        {
            warnings.Add(new Warning(WarningCodes.LossyConversion, $"Slides conversion to {targetFormatId} may not preserve every presentation feature.")
            {
                Hint = "Keep the source deck and inspect the produced file for layout, animation and interactive-media changes.",
            });
        }

        return warnings;
    }

    internal static SlidesRenderResult Render(SlidesSession session, PresentationRenderRequest request)
    {
        LicenseState state = session.Outputs.License;
        using LoadedPresentation loaded = session.Loader.Open(request.Input, request.Password);
        IReadOnlyList<int> slides = request.AllSlides
            ? AllSlides(loaded.Presentation.Slides.Count)
            : ResolveSlideRange(request.Slides ?? PageRange.Parse("1"), loaded.Presentation.Slides.Count);
        IReadOnlyList<SlideRenderOutput> outputs = RenderImages(session, loaded, request, slides, "slides-render");
        return new SlidesRenderResult
        {
            Input = Source(request.Input, loaded.FormatId),
            Outputs = outputs,
            Dpi = request.Output.Format.Id == "svg" || request.Width is not null
                ? null
                : request.Dpi ?? DefaultRasterDpi,
            Width = request.Output.Format.Id == "svg" ? null : request.Width,
            License = EnvelopeParts.License(state),
            Warnings = WrittenWarnings(state, loaded, textRead: false),
        };
    }

    private static IReadOnlyList<SlideRenderOutput> RenderImages(
        SlidesSession session,
        LoadedPresentation loaded,
        PresentationRenderRequest request,
        IReadOnlyList<int> slides,
        string transactionName)
    {
        Presentation presentation = loaded.Presentation;
        float scale = RenderScale(presentation, request);
        if (request.Output.Format.Id != "svg")
        {
            long width = (long)Math.Ceiling(presentation.SlideSize.Size.Width * scale);
            long height = (long)Math.Ceiling(presentation.SlideSize.Size.Height * scale);
            EnsureRasterFits(
                session.Budgets,
                width,
                height,
                request.Width is null ? request.Dpi ?? DefaultRasterDpi : null);
        }

        string directory = request.Output.Directory;
        using OutputSet<Presentation> transaction = session.Outputs.BeginSet([directory], transactionName);
        var targets = new List<(int Number, uint SlideId, string Path)>(slides.Count);
        foreach (int number in slides)
        {
            ISlide slide = presentation.Slides[number - 1];
            string path = request.Output.Part(SlidePartMarker, number, slides.Count);
            targets.Add((number, slide.SlideId, path));
            transaction.Stage(path, request.Output.Overwrite, presentation, temp =>
            {
                if (request.Output.Format.Id == "svg")
                {
                    using FileStream stream = File.Create(temp);
                    slide.WriteAsSvg(stream);
                    return;
                }

                using IImage image = slide.GetImage(scale, scale);
                using FileStream outputStream = File.Create(temp);
                image.Save(
                    outputStream,
                    request.Output.Format.Id == "png" ? ImageFormat.Png : ImageFormat.Jpeg,
                    quality: 92);
            }, rendering: true, pages: [number]);
        }

        loaded.Resources.ThrowIfFailed();
        IReadOnlyList<long> sizes = transaction.Commit();
        return targets.Select((target, index) => new SlideRenderOutput
        {
            Slide = target.Number,
            SlideId = target.SlideId,
            Output = new OutputInfo
            {
                Path = target.Path,
                Format = request.Output.Format.Id,
                SizeBytes = sizes[index],
            },
        }).ToArray();
    }
}
