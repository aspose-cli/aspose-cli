using System.Text;
using Aspose.Cli.Product.Slides.Engine.Mapping;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Slides;
using static Aspose.Cli.Product.Slides.Engine.SlidesEngineSupport;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>Owns bounded extraction of embedded media, speaker notes and slide text.</summary>
internal sealed class SlidesExtractionService
{
    private readonly ILicenseGate _licenseGate;
    private readonly ResourceBudgetLedger _resourceBudgets;
    private readonly SlidesPresentationLoader _loader;

    internal SlidesExtractionService(
        ILicenseGate licenseGate,
        ResourceBudgetLedger resourceBudgets,
        SlidesPresentationLoader loader)
    {
        _licenseGate = licenseGate ?? throw new ArgumentNullException(nameof(licenseGate));
        _resourceBudgets = resourceBudgets;
        _loader = loader;
    }

    internal SlidesExtractResult Extract(string filePath, PresentationExtractRequest request)
    {
        LicenseState state = _licenseGate.EnsureApplied();
        using LoadedPresentation loaded = _loader.Open(filePath, request.Password);
        IReadOnlyList<int> slides = request.Slides is null
            ? Enumerable.Range(1, loaded.Presentation.Slides.Count).ToArray()
            : ResolveSlideRange(request.Slides, loaded.Presentation.Slides.Count);
        using var guard = new ExtractionGuard(_resourceBudgets, request.OutputDirectory, request.Overwrite);
        var items = new List<SlidesExtractedItem>();

        if (request.What == PresentationExtractKinds.Media)
        {
            ExtractMedia(loaded.Presentation, request.Slides is null ? null : slides, guard, items);
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
                byte[] bytes = Encoding.UTF8.GetBytes(text);
                items.Add(new SlidesExtractedItem
                {
                    Path = guard.WriteAllBytes($"slide.s{number}.{suffix}.txt", bytes),
                    Kind = suffix,
                    SizeBytes = bytes.LongLength,
                    Slide = number,
                    SlideId = slide.SlideId,
                    Name = EmptyToNull(slide.Name),
                    ContentType = "text/plain",
                });
            }
        }

        loaded.Resources.ThrowIfFailed();
        guard.Commit();
        return new SlidesExtractResult
        {
            Input = Source(filePath, loaded.FormatId),
            What = request.What,
            Items = items,
            License = EnvelopeParts.License(state),
            Warnings = OutputWarnings(state, loaded),
        };
    }

    /// <summary>
    /// Extracts embedded media. With a slide selection only the media those slides show
    /// (pictures, picture fills, backgrounds, audio and video) is extracted; indexes stay the
    /// presentation-wide positions.
    /// </summary>
    private static void ExtractMedia(
        Presentation presentation,
        IReadOnlyList<int>? slides,
        ExtractionGuard guard,
        List<SlidesExtractedItem> items)
    {
        HashSet<object>? shown = slides is null ? null : ShownMedia(presentation, slides);
        int index = 0;
        void Add(object media, string kind, string? contentType, Func<byte[]> read)
        {
            index++;
            if (shown?.Contains(media) == false)
            {
                return;
            }

            byte[] bytes = read();
            items.Add(new SlidesExtractedItem
            {
                Path = guard.WriteAllBytes($"media.{kind}.{index}{ContentExtension(contentType, ".bin")}", bytes),
                Kind = kind,
                SizeBytes = bytes.LongLength,
                Index = index,
                ContentType = EmptyToNull(contentType),
            });
        }

        foreach (IPPImage image in presentation.Images)
        {
            Add(image, "image", image.ContentType, () => image.BinaryData);
        }

        foreach (IAudio audio in presentation.Audios)
        {
            Add(audio, "audio", audio.ContentType, () => audio.BinaryData);
        }

        foreach (IVideo video in presentation.Videos)
        {
            Add(video, "video", video.ContentType, () => video.BinaryData);
        }
    }

    private static HashSet<object> ShownMedia(Presentation presentation, IReadOnlyList<int> slides)
    {
        var shown = new HashSet<object>(ReferenceEqualityComparer.Instance);
        foreach (int number in slides)
        {
            ISlide slide = presentation.Slides[number - 1];
            AddPicture(shown, slide.Background.FillFormat);
            foreach (IShape shape in slide.Shapes)
            {
                AddShown(shown, shape);
            }
        }

        return shown;
    }

    private static void AddShown(HashSet<object> shown, IShape shape)
    {
        switch (shape)
        {
            case IGroupShape group:
                foreach (IShape child in group.Shapes)
                {
                    AddShown(shown, child);
                }

                break;
            case IAudioFrame { EmbeddedAudio: { } audio }:
                shown.Add(audio);
                break;
            case IVideoFrame { EmbeddedVideo: { } video }:
                shown.Add(video);
                break;
            case IPictureFrame { PictureFormat.Picture.Image: { } image }:
                shown.Add(image);
                break;
        }

        AddPicture(shown, shape.FillFormat);
    }

    private static void AddPicture(HashSet<object> shown, IFillFormat? fill)
    {
        if (fill is { FillType: FillType.Picture, PictureFillFormat.Picture.Image: { } image })
        {
            shown.Add(image);
        }
    }

    private static string ContentExtension(string? contentType, string fallback) =>
        contentType?.ToLowerInvariant() switch
        {
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "image/gif" => ".gif",
            "image/bmp" => ".bmp",
            "image/tiff" => ".tiff",
            "image/svg+xml" => ".svg",
            "audio/mpeg" => ".mp3",
            "audio/wav" or "audio/x-wav" => ".wav",
            "video/mp4" => ".mp4",
            "video/mpeg" => ".mpeg",
            _ => fallback,
        };
}
