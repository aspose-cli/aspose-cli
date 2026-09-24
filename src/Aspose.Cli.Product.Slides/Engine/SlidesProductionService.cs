using System.Globalization;
using System.Text;
using Aspose.Cli.Product.Slides.Engine.Mapping;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.Sdk.Views;
using Aspose.Slides;
using Aspose.Slides.Export;
using static Aspose.Cli.Product.Slides.Engine.SlidesEngineSupport;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>Owns conversion, rendering, creation, extraction, and preview output.</summary>
internal sealed class SlidesProductionService
{
    /// <summary>Marks the slide number in a multi-slide output name: <c>deck.s3.png</c>.</summary>
    private const string SlidePartMarker = "s";

    private readonly ILicenseGate _licenseGate;
    private readonly ResourceBudgetLedger _resourceBudgets;
    private readonly SafeFileWriter _writer;
    private readonly SlidesPresentationLoader _loader;

    internal SlidesProductionService(
        ILicenseGate licenseGate,
        ResourceBudgetLedger resourceBudgets,
        SafeFileWriter writer,
        SlidesPresentationLoader loader)
    {
        _licenseGate = licenseGate ?? throw new ArgumentNullException(nameof(licenseGate));
        _resourceBudgets = resourceBudgets;
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _loader = loader;
    }

    /// <summary>Renders the slides of one view, opening the presentation once.</summary>
    internal ViewManifest RenderView(
        string filePath,
        ViewRenderRequest request,
        IViewArtifactSink artifacts)
    {
        const int evidenceWidth = 1600;
        const int displayWidth = 1920;
        const int cssWidth = 960;
        ArgumentNullException.ThrowIfNull(artifacts);
        LicenseState state = _licenseGate.EnsureApplied();
        using LoadedPresentation loaded = _loader.Open(filePath, request.Password);
        Presentation presentation = loaded.Presentation;
        int pixelWidth = request.Purpose == ViewPurpose.Display ? displayWidth : evidenceWidth;
        float scale = (float)(pixelWidth / presentation.SlideSize.Size.Width);
        int total = presentation.Slides.Count;
        int count = Math.Min(total, request.MaxParts);
        EnsureRasterFits(
            _resourceBudgets,
            (long)Math.Ceiling(presentation.SlideSize.Size.Width * scale),
            (long)Math.Ceiling(presentation.SlideSize.Size.Height * scale),
            dpi: null);
        double cssPerPoint = cssWidth / presentation.SlideSize.Size.Width;
        int cssHeight = Math.Max(1, (int)Math.Round(
            presentation.SlideSize.Size.Height * cssPerPoint,
            MidpointRounding.AwayFromZero));
        var parts = new List<ViewPart>(count);
        for (int index = 0; index < count; index++)
        {
            ISlide slide = presentation.Slides[index];
            int number = index + 1;
            string file = string.Create(CultureInfo.InvariantCulture, $"slide-{number:0000}.png");
            using (IImage image = slide.GetImage(scale, scale))
            {
                artifacts.Write(file, stream => image.Save(stream, ImageFormat.Png));
            }
            string? notes = Notes(slide);
            parts.Add(new ViewPart
            {
                Id = string.Create(CultureInfo.InvariantCulture, $"slide-{slide.SlideId}"),
                Label = Title(slide) ?? string.Create(CultureInfo.InvariantCulture, $"Slide {number}"),
                File = file,
                Kind = ViewPartKinds.Image,
                Width = cssWidth,
                Height = cssHeight,
                Hidden = slide.Hidden,
                Elements = SlidesViewLayout.Elements(slide, cssPerPoint),
                Properties = notes is null
                    ? null
                    : new Dictionary<string, string>(StringComparer.Ordinal) { ["notes"] = notes },
            });
        }

        return new ViewManifest
        {
            View = SlidesViews.Slides,
            SourceFormat = loaded.FormatId,
            SourceSizeBytes = new FileInfo(filePath).Length,
            TotalParts = total,
            Parts = parts,
            Warnings = InputWarnings(state, loaded),
        };
    }

    internal SlidesConvertResult Convert(string filePath, PresentationConvertRequest request)
    {
        LicenseState state = _licenseGate.EnsureApplied();
        using LoadedPresentation loaded = _loader.Open(filePath, request.Password);
        IReadOnlyList<int>? slides = request.Slides is null
            ? null
            : ResolveSlideRange(request.Slides, loaded.Presentation.Slides.Count);
        IReadOnlyList<OutputInfo> outputs = ConvertOutputs(loaded, request, slides);
        List<Warning> warnings = BuildConvertWarnings(state, loaded, request.TargetFormatId);
        return new SlidesConvertResult
        {
            Input = Source(filePath, loaded.FormatId),
            Outputs = outputs,
            Slides = request.Slides?.Text,
            License = EnvelopeParts.License(state),
            Warnings = warnings.Count == 0 ? null : warnings,
        };
    }

    private IReadOnlyList<OutputInfo> ConvertOutputs(
        LoadedPresentation loaded,
        PresentationConvertRequest request,
        IReadOnlyList<int>? slides)
    {
        Presentation presentation = loaded.Presentation;
        if (request.TargetFormatId is "png" or "jpeg" or "svg")
        {
            IReadOnlyList<int> selected = slides ?? AllSlides(presentation.Slides.Count);
            return RenderImages(loaded, new PresentationRenderRequest
            {
                TargetFormatId = request.TargetFormatId,
                OutputPath = request.OutputPath,
                Overwrite = request.Overwrite,
            }, selected, "slides-convert").Select(static item => item.Output).ToArray();
        }
        else
        {
            SaveFormat format = SaveFormatFor(request.TargetFormatId);
            long size = _writer.Write(request.OutputPath, request.Overwrite, temp =>
            {
                Save(temp);
                loaded.Resources.ThrowIfFailed();
            });
            return
            [
                new OutputInfo
                {
                    Path = request.OutputPath,
                    Format = request.TargetFormatId,
                    SizeBytes = size,
                },
            ];

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
        var warnings = OutputWarnings(state, loaded)?.ToList() ?? [];
        if (targetFormatId is "html" or "html5" or "md")
        {
            warnings.Add(new Warning
            {
                Code = WarningCodes.LossyConversion,
                Message = $"Slides conversion to {targetFormatId} may not preserve every presentation feature.",
                Hint = "Keep the source deck and inspect the produced file for layout, animation and interactive-media changes.",
            });
        }

        return warnings;
    }

    internal SlidesRenderResult Render(string filePath, PresentationRenderRequest request)
    {
        LicenseState state = _licenseGate.EnsureApplied();
        using LoadedPresentation loaded = _loader.Open(filePath, request.Password);
        IReadOnlyList<int> slides = request.AllSlides
            ? AllSlides(loaded.Presentation.Slides.Count)
            : ResolveSlideRange(request.Slides ?? PageRange.Parse("1"), loaded.Presentation.Slides.Count);
        IReadOnlyList<SlideRenderOutput> outputs = RenderImages(loaded, request, slides, "slides-render");
        return new SlidesRenderResult
        {
            Input = Source(filePath, loaded.FormatId),
            Outputs = outputs,
            Dpi = request.TargetFormatId == "svg" || request.Width is not null
                ? null
                : request.Dpi ?? DefaultRasterDpi,
            Width = request.TargetFormatId == "svg" ? null : request.Width,
            License = EnvelopeParts.License(state),
            Warnings = OutputWarnings(state, loaded),
        };
    }

    private IReadOnlyList<SlideRenderOutput> RenderImages(
        LoadedPresentation loaded,
        PresentationRenderRequest request,
        IReadOnlyList<int> slides,
        string transactionName)
    {
        Presentation presentation = loaded.Presentation;
        float scale = RenderScale(presentation, request);
        if (request.TargetFormatId != "svg")
        {
            long width = (long)Math.Ceiling(presentation.SlideSize.Size.Width * scale);
            long height = (long)Math.Ceiling(presentation.SlideSize.Size.Height * scale);
            EnsureRasterFits(
                _resourceBudgets,
                width,
                height,
                request.Width is null ? request.Dpi ?? DefaultRasterDpi : null);
        }

        string directory = Path.GetDirectoryName(request.OutputPath)!;
        using var transaction = new AtomicOutputSetWriter(_writer, directory, transactionName);
        var targets = new List<(int Number, uint SlideId, string Path)>(slides.Count);
        foreach (int number in slides)
        {
            ISlide slide = presentation.Slides[number - 1];
            string path = slides.Count == 1
                ? request.OutputPath
                : PartOutputPath.For(request.OutputPath, SlidePartMarker, number);
            targets.Add((number, slide.SlideId, path));
            transaction.Stage(path, request.Overwrite, temp =>
            {
                if (request.TargetFormatId == "svg")
                {
                    using FileStream stream = File.Create(temp);
                    slide.WriteAsSvg(stream);
                    return;
                }

                using IImage image = slide.GetImage(scale, scale);
                using FileStream outputStream = File.Create(temp);
                image.Save(
                    outputStream,
                    request.TargetFormatId == "png" ? ImageFormat.Png : ImageFormat.Jpeg,
                    quality: 92);
            });
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
                Format = request.TargetFormatId,
                SizeBytes = sizes[index],
            },
        }).ToArray();
    }
    internal SlidesCreateResult Create(NewPresentationRequest request)
    {
        LicenseState state = _licenseGate.EnsureApplied();
        string format = SlidesFormats.ForOutput(request.OutputPath);
        if (!SlidesFormats.WriteIds.Contains(format, StringComparer.Ordinal))
        {
            throw Sdk.Errors.CliErrors.FormatUnsupported(format, SlidesFormats.WriteIds);
        }

        using LoadedPresentation template = request.TemplatePath is null
            ? SlidesPresentationLoader.OpenDefaultTemplate()
            : _loader.Open(request.TemplatePath, password: null);
        Presentation presentation = template.Presentation;
        ApplySlideSize(presentation, request.Size);
        if (request.MarkdownPath is not null)
        {
            SlidesMarkdownBuilder.Build(
                _resourceBudgets,
                presentation,
                request.MarkdownPath);
        }
        else if (presentation.Slides.Count == 0)
        {
            // A design template may hold only masters and layouts; a presentation needs a slide.
            presentation.Slides.AddEmptySlide(SlidesPlaceholders.Layout(presentation, SlideLayoutType.Title));
        }

        Encrypt(presentation, request.EncryptPassword);
        long size = _writer.Write(
            request.OutputPath,
            request.Overwrite,
            temp =>
            {
                presentation.Save(temp, SaveFormatFor(format));
                template.Resources.ThrowIfFailed();
            });
        return new SlidesCreateResult
        {
            Output = new OutputInfo
            {
                Path = request.OutputPath,
                Format = format,
                SizeBytes = size,
            },
            Slides = presentation.Slides.Count,
            Template = request.TemplatePath is null
                ? null
                : Source(request.TemplatePath, template.FormatId),
            Markdown = request.MarkdownPath is null
                ? null
                : new SourceInfo
                {
                    Path = request.MarkdownPath,
                    Format = "md",
                    SizeBytes = new FileInfo(request.MarkdownPath).Length,
                },
            License = EnvelopeParts.License(state),
            Warnings = OutputWarnings(state, template),
        };
    }

    internal SlidesExtractResult Extract(string filePath, PresentationExtractRequest request)
    {
        LicenseState state = _licenseGate.EnsureApplied();
        using LoadedPresentation loaded = _loader.Open(filePath, request.Password);
        IReadOnlyList<int> slides = request.Slides is null
            ? Enumerable.Range(1, loaded.Presentation.Slides.Count).ToArray()
            : ResolveSlideRange(request.Slides, loaded.Presentation.Slides.Count);
        using var transaction = new AtomicOutputSetWriter(_writer, request.OutputDirectory, "slides-extract");
        var items = new List<(string Path, string Kind, int? Slide, uint? SlideId, int? Index, string? Name, string? ContentType)>();

        if (request.What == PresentationExtractKinds.Media)
        {
            StageMedia(loaded.Presentation, request.Slides is null ? null : slides, request, transaction, items);
        }
        else
        {
            foreach (int number in slides)
            {
                ISlide slide = loaded.Presentation.Slides[number - 1];
                string? text = request.What == PresentationExtractKinds.Notes
                    ? Notes(slide)
                    : string.Join(
                        Environment.NewLine,
                        slide.Shapes.Select(ShapeText).Where(static value => value is not null));
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                string suffix = request.What == PresentationExtractKinds.Notes ? "notes" : "text";
                string path = Path.Combine(request.OutputDirectory, $"slide.s{number}.{suffix}.txt");
                transaction.Stage(
                    path,
                    request.Overwrite,
                    temp => File.WriteAllText(temp, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)));
                items.Add((path, suffix, number, slide.SlideId, null, slide.Name, "text/plain"));
            }
        }

        loaded.Resources.ThrowIfFailed();
        IReadOnlyList<long> sizes = transaction.Commit();
        return new SlidesExtractResult
        {
            Input = Source(filePath, loaded.FormatId),
            What = request.What,
            Items = items.Select((item, index) => new SlidesExtractedItem
            {
                Path = item.Path,
                Kind = item.Kind,
                SizeBytes = sizes[index],
                Slide = item.Slide,
                SlideId = item.SlideId,
                Index = item.Index,
                Name = EmptyToNull(item.Name),
                ContentType = item.ContentType,
            }).ToArray(),
            License = EnvelopeParts.License(state),
            Warnings = OutputWarnings(state, loaded),
        };
    }

}
