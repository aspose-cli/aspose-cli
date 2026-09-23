using System.Globalization;
using System.Text.Json.Nodes;
using Aspose.Cli.Product.Slides.Contracts;
using Aspose.Cli.Product.Slides.Engine.Mapping;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Rendering;
using Aspose.Cli.Sdk.Results;
using Aspose.Slides;
using Aspose.Slides.Charts;
using Aspose.Slides.Export;
using Aspose.Slides.SmartArt;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>Shared Slides projection, formatting, range, and output helpers.</summary>
internal static class SlidesEngineSupport
{
    internal const string EvaluationTruncationMarker = "truncated due to evaluation version limitation";
    internal const int DefaultRasterDpi = 192;

    internal static Warning EvaluationInputWarning { get; } = new()
    {
        Code = WarningCodes.EvalInputTruncated,
        Message = "Aspose.Slides evaluation mode replaced presentation text while loading the input.",
        Hint = "Do not treat the returned text or converted output as complete; apply a license and retry.",
    };

    internal static Presentation SelectSlides(Presentation source, IReadOnlyList<int> slides)
    {
        var selected = new Presentation();
        selected.Slides.RemoveAt(0);
        foreach (int number in slides)
        {
            selected.Slides.AddClone(source.Slides[number - 1]);
        }

        return selected;
    }

    internal static SlideInfo ProjectInfo(ISlide slide, int number, IReadOnlyList<IComment> comments, bool preview)
    {
        string? title = preview ? Title(slide) : null;
        return new SlideInfo
        {
            Number = number,
            SlideId = slide.SlideId,
            Name = EmptyToNull(slide.Name),
            Layout = EmptyToNull(slide.LayoutSlide?.Name),
            Title = title,
            PreviewText = preview ? PreviewText(slide) : null,
            Shapes = slide.Shapes.Count,
            Hidden = slide.Hidden,
            HasNotes = !string.IsNullOrWhiteSpace(Notes(slide)),
            Comments = comments.Count(comment => ReferenceEquals(comment.Slide, slide)),
        };
    }

    internal static SlideData ProjectSlide(
        ISlide slide,
        int number,
        string scope,
        bool includeNotes,
        IReadOnlyList<IComment> comments,
        ref int remaining)
    {
        bool includeShapes = scope is PresentationReadScopes.Shapes or PresentationReadScopes.Full;
        bool contentTruncated = false;
        string? title = Take(Title(slide), ref remaining, ref contentTruncated);
        var textBlocks = scope == PresentationReadScopes.Text ? new List<string>() : null;
        var shapes = new List<SlideShapeData>();
        int zOrder = 0;
        foreach (IShape shape in slide.Shapes)
        {
            string? sourceText = ShapeText(shape);
            if (remaining == 0 && (includeShapes || !string.IsNullOrWhiteSpace(sourceText)))
            {
                contentTruncated = true;
                break;
            }
            string? text = Take(sourceText, ref remaining, ref contentTruncated);
            if (textBlocks is not null && !string.IsNullOrEmpty(text))
            {
                textBlocks.Add(text);
            }
            if (includeShapes)
            {
                shapes.Add(new SlideShapeData
                {
                    ShapeId = shape.OfficeInteropShapeId,
                    Name = EmptyToNull(shape.Name),
                    Type = ShapeTypeName(shape),
                    Role = SlidesPlaceholders.Role(shape.Placeholder?.Type),
                    Text = text,
                    Runs = scope == PresentationReadScopes.Full ? Runs(shape, ref remaining, ref contentTruncated) : null,
                    ZOrder = zOrder++,
                    HasOpaqueFill = SlidesReviewProjection.HasOpaqueFill(shape),
                    Rect = new SlideRect
                    {
                        X = shape.X,
                        Y = shape.Y,
                        Width = shape.Width,
                        Height = shape.Height,
                    },
                });
            }
        }
        string? notes = scope == PresentationReadScopes.Full || includeNotes
            ? Take(Notes(slide), ref remaining, ref contentTruncated)
            : null;
        var projectedComments = scope == PresentationReadScopes.Full ? new List<SlideCommentData>() : null;
        if (projectedComments is not null)
        {
            foreach (IComment comment in comments.Where(comment => ReferenceEquals(comment.Slide, slide)))
            {
                if (remaining == 0 && !string.IsNullOrWhiteSpace(comment.Text))
                {
                    contentTruncated = true;
                    break;
                }
                projectedComments.Add(new SlideCommentData
                {
                    Author = comment.Author.Name,
                    Text = Take(comment.Text, ref remaining, ref contentTruncated) ?? string.Empty,
                });
            }
        }
        return new SlideData
        {
            Number = number,
            SlideId = slide.SlideId,
            Name = EmptyToNull(slide.Name),
            Layout = EmptyToNull(slide.LayoutSlide?.Name),
            Title = title,
            Text = textBlocks,
            Shapes = shapes,
            Notes = notes,
            Comments = projectedComments,
            ContentTruncated = contentTruncated,
        };
    }

    internal static string? Take(string? value, ref int remaining, ref bool truncated)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string clean = string.Join(" ", value.Split(default(string[]), StringSplitOptions.RemoveEmptyEntries));
        if (clean.Length <= remaining)
        {
            remaining -= clean.Length;
            return clean;
        }

        truncated = true;
        string result = remaining == 0 ? string.Empty : clean[..remaining];
        remaining = 0;
        return result;
    }

    /// <summary>The text a shape shows: its own frame, table cells, group children and SmartArt nodes.</summary>
    internal static string? ShapeText(IShape shape) =>
        EmptyToNull(string.Join(
            "\n",
            TextFrames(shape).Select(static frame => frame.Text).Where(static text => !string.IsNullOrWhiteSpace(text))));

    /// <summary>
    /// Every text frame a shape shows, in reading order: its own frame, each table cell
    /// (a merged range once), each group child and each SmartArt node. Chart text is not included.
    /// </summary>
    internal static IEnumerable<ITextFrame> TextFrames(IShape shape)
    {
        switch (shape)
        {
            case IGroupShape group:
                foreach (ITextFrame frame in group.Shapes.SelectMany(TextFrames))
                {
                    yield return frame;
                }

                break;
            case ITable table:
                for (int row = 0; row < table.Rows.Count; row++)
                {
                    for (int column = 0; column < table.Columns.Count; column++)
                    {
                        ICell cell = table[column, row];
                        if (cell.FirstRowIndex == row && cell.FirstColumnIndex == column && cell.TextFrame is { } text)
                        {
                            yield return text;
                        }
                    }
                }

                break;
            case ISmartArt smartArt:
                foreach (ISmartArtNode node in smartArt.AllNodes)
                {
                    if (node.TextFrame is { } text)
                    {
                        yield return text;
                    }
                }

                break;
            case IAutoShape { TextFrame: { } text }:
                yield return text;
                break;
        }
    }

    internal static IReadOnlyList<Warning>? EvaluationInputWarnings(
        LicenseState state,
        Presentation presentation) =>
        state == LicenseState.Evaluation && EvaluationInputTruncated(presentation)
            ? [EvaluationInputWarning]
            : null;

    internal static bool EvaluationInputTruncated(Presentation presentation) =>
        presentation.Slides.Any(slide =>
            slide.Shapes.Any(shape =>
                ShapeText(shape)?.Contains(
                    EvaluationTruncationMarker,
                    StringComparison.OrdinalIgnoreCase) == true)
            || Notes(slide)?.Contains(
                EvaluationTruncationMarker,
                StringComparison.OrdinalIgnoreCase) == true);

    internal static IReadOnlyList<SlideTextRunData>? Runs(
        IShape shape,
        ref int remaining,
        ref bool truncated)
    {
        var runs = new List<SlideTextRunData>();
        foreach (IParagraph paragraph in TextFrames(shape).SelectMany(static frame => frame.Paragraphs))
        {
            foreach (IPortion portion in paragraph.Portions)
            {
                if (remaining == 0 && !string.IsNullOrWhiteSpace(portion.Text))
                {
                    truncated = true;
                    return runs;
                }
                string? text = Take(portion.Text, ref remaining, ref truncated);
                if (text is null)
                {
                    continue;
                }

                IBasePortionFormatEffectiveData effective =
                    portion.PortionFormat.GetEffective().AsIBasePortionFormatEffectiveData;
                runs.Add(new SlideTextRunData
                {
                    Text = text,
                    Font = EmptyToNull(effective.LatinFont?.FontName),
                    Size = effective.FontHeight,
                    Bold = effective.FontBold,
                    Italic = effective.FontItalic,
                });
            }
        }

        return runs.Count == 0 ? null : runs;
    }

    internal static string ShapeTypeName(IShape shape) => shape switch
    {
        _ when shape.Placeholder is not null => "placeholder",
        IChart => "chart",
        ITable => "table",
        IAudioFrame => "audio",
        IVideoFrame => "video",
        IPictureFrame => "image",
        IGroupShape => "group",
        _ => "shape",
    };

    internal static string? PreviewText(ISlide slide)
    {
        string value = string.Join(
            " · ",
            slide.Shapes.Select(ShapeText).Where(static text => text is not null));
        return value.Length switch
        {
            0 => null,
            <= 240 => value,
            _ => value[..240],
        };
    }

    internal static string? Title(ISlide slide)
    {
        IAutoShape? title = slide.Shapes
            .OfType<IAutoShape>()
            .FirstOrDefault(static shape =>
                SlidesPlaceholders.IsTitle(shape.Placeholder)
                &&!string.IsNullOrWhiteSpace(shape.TextFrame?.Text));
        title ??= slide.Shapes
            .OfType<IAutoShape>()
            .FirstOrDefault(static shape => !string.IsNullOrWhiteSpace(shape.TextFrame?.Text));
        return EmptyToNull(title?.TextFrame?.Text);
    }

    internal static string? Notes(ISlide slide) =>
        EmptyToNull(slide.NotesSlideManager.NotesSlide?.NotesTextFrame?.Text);

    internal static IComment[] Comments(Presentation presentation) =>
        presentation.CommentAuthors
            .SelectMany(static author => author.Comments.Cast<IComment>())
            .ToArray();

    internal static IReadOnlyList<PresentationMediaInfo> Media(Presentation presentation)
    {
        var media = new List<PresentationMediaInfo>();
        foreach (IPPImage image in presentation.Images)
        {
            media.Add(new PresentationMediaInfo
            {
                Index = media.Count + 1,
                Type = "image",
                ContentType = EmptyToNull(image.ContentType),
                SizeBytes = image.BinaryData.LongLength,
            });
        }

        foreach (IAudio audio in presentation.Audios)
        {
            media.Add(new PresentationMediaInfo
            {
                Index = media.Count + 1,
                Type = "audio",
                ContentType = EmptyToNull(audio.ContentType),
                SizeBytes = audio.BinaryData.LongLength,
            });
        }

        foreach (IVideo video in presentation.Videos)
        {
            media.Add(new PresentationMediaInfo
            {
                Index = media.Count + 1,
                Type = "video",
                ContentType = EmptyToNull(video.ContentType),
                SizeBytes = video.BinaryData.LongLength,
            });
        }

        return media.Take(100).ToArray();
    }

    internal static int FindSlideNumber(Presentation presentation, ISlide slide)
    {
        for (int index = 0; index < presentation.Slides.Count; index++)
        {
            if (ReferenceEquals(presentation.Slides[index], slide))
            {
                return index + 1;
            }
        }

        return 0;
    }

    internal static IReadOnlyDictionary<string, string?> Properties(IDocumentProperties properties) =>
        new SortedDictionary<string, string?>(StringComparer.Ordinal)
        {
            ["author"] = EmptyToNull(properties.Author),
            ["company"] = EmptyToNull(properties.Company),
            ["keywords"] = EmptyToNull(properties.Keywords),
            ["subject"] = EmptyToNull(properties.Subject),
            ["title"] = EmptyToNull(properties.Title),
        };

    internal static void StageMedia(
        Presentation presentation,
        PresentationExtractRequest request,
        AtomicOutputSetWriter transaction,
        List<(string Path, string Kind, int? Slide, uint? SlideId, int? Index, string? Name, string? ContentType)> items)
    {
        int index = 0;
        foreach (IPPImage image in presentation.Images)
        {
            index++;
            string path = Path.Combine(
                request.OutputDirectory,
                $"media.image.{index}{ContentExtension(image.ContentType, ".bin")}");
            transaction.Stage(path, request.Overwrite, temp => File.WriteAllBytes(temp, image.BinaryData));
            items.Add((path, "image", null, null, index, null, EmptyToNull(image.ContentType)));
        }

        foreach (IAudio audio in presentation.Audios)
        {
            index++;
            string path = Path.Combine(
                request.OutputDirectory,
                $"media.audio.{index}{ContentExtension(audio.ContentType, ".bin")}");
            transaction.Stage(path, request.Overwrite, temp => File.WriteAllBytes(temp, audio.BinaryData));
            items.Add((path, "audio", null, null, index, null, EmptyToNull(audio.ContentType)));
        }

        foreach (IVideo video in presentation.Videos)
        {
            index++;
            string path = Path.Combine(
                request.OutputDirectory,
                $"media.video.{index}{ContentExtension(video.ContentType, ".bin")}");
            transaction.Stage(path, request.Overwrite, temp => File.WriteAllBytes(temp, video.BinaryData));
            items.Add((path, "video", null, null, index, null, EmptyToNull(video.ContentType)));
        }
    }

    internal static string ContentExtension(string? contentType, string fallback) =>
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

    internal static float RenderScale(Presentation presentation, PresentationRenderRequest request)
    {
        if (request.Width is int width)
        {
            return (float)(width / presentation.SlideSize.Size.Width);
        }

        return (request.Dpi ?? DefaultRasterDpi) / 72f;
    }

    internal static void EnsureRasterFits(
        ResourceBudgetLedger resourceBudgets,
        long width,
        long height,
        int? dpi) =>
        RenderPixelGuard.EnsureFits(
            resourceBudgets,
            width,
            height,
            dpi,
            "Lower --dpi or pass a smaller --width.");

    /// <summary>
    /// Resizes the canvas and scales masters, layouts and slides with it, so inherited
    /// placeholder geometry (footers, slide numbers, dates) stays inside the new canvas.
    /// </summary>
    internal static void ApplySlideSize(Presentation presentation, string? size)
    {
        if (size is null)
        {
            return;
        }

        presentation.SlideSize.SetSize(
            size switch
            {
                "16x9" => SlideSizeType.OnScreen16x9,
                "4x3" => SlideSizeType.OnScreen,
                _ => throw Sdk.Errors.CliErrors.OptionInvalid(
                    "--size",
                    $"unsupported slide size '{size}'",
                    "Use 16x9 or 4x3."),
            },
            SlideSizeScaleType.EnsureFit);
    }

    internal static void EnsureEncryptionSupported(string? password, string targetFormat)
    {
        if (password is not null && targetFormat is not ("pptx" or "pptm"))
        {
            throw new CliException(
                ErrorCodes.FeatureUnsupported,
                $"Slides output encryption is not supported for {targetFormat}.",
                hint: "Write PPTX or PPTM when --encrypt is required.");
        }
    }

    internal static void Encrypt(Presentation presentation, string? password)
    {
        if (password is not null)
        {
            presentation.ProtectionManager.Encrypt(password);
        }
    }

    internal static IReadOnlyList<Warning>? OutputWarnings(
        LicenseState state,
        Presentation? presentation)
    {
        var warnings = new List<Warning>();
        if (state == LicenseState.Evaluation)
        {
            warnings.Add(EnvelopeParts.EvaluationWatermark);
        }

        if (presentation is not null
            && state == LicenseState.Evaluation
            && EvaluationInputTruncated(presentation))
        {
            warnings.Add(EvaluationInputWarning);
        }

        return warnings.Count == 0 ? null : warnings;
    }

    internal static SourceInfo Source(string path, string format) => new()
    {
        Path = Path.GetFullPath(path),
        Format = format,
        SizeBytes = new FileInfo(path).Length,
        Fingerprint = FileFingerprints.Capture(path),
    };

    internal static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    internal static IReadOnlyList<int> ResolveSlideRange(PageRange range, int slideCount)
    {
        EnsureHasSlides(slideCount);
        try
        {
            return range.Resolve(slideCount);
        }
        catch (CliException exception) when (exception.Code == ErrorCodes.PageNotFound)
        {
            throw new CliException(
                SlidesDiagnostics.SlideNotFound,
                $"The requested slide selection is outside the presentation's {slideCount} slide(s).",
                hint: $"Use slide numbers from 1 to {slideCount}.",
                details: exception.Details,
                innerException: exception);
        }
    }

    /// <summary>Every slide number; a presentation without slides has nothing to select.</summary>
    internal static IReadOnlyList<int> AllSlides(int slideCount)
    {
        EnsureHasSlides(slideCount);
        return Enumerable.Range(1, slideCount).ToArray();
    }

    private static void EnsureHasSlides(int slideCount)
    {
        if (slideCount == 0)
        {
            throw new CliException(
                SlidesDiagnostics.SlideNotFound,
                "The presentation has no slides.",
                hint: "Add a slide with an add_slide operation in 'slides edit' first.");
        }
    }

    internal static SaveFormat SaveFormatFor(string format) => format switch
    {
        "pptx" => SaveFormat.Pptx,
        "ppt" => SaveFormat.Ppt,
        "pptm" => SaveFormat.Pptm,
        "odp" => SaveFormat.Odp,
        "pdf" => SaveFormat.Pdf,
        "xps" => SaveFormat.Xps,
        "html" => SaveFormat.Html,
        "html5" => SaveFormat.Html5,
        "tiff" => SaveFormat.Tiff,
        "gif" => SaveFormat.Gif,
        "md" => SaveFormat.Md,
        _ => throw Sdk.Errors.CliErrors.FormatUnsupported(format, SlidesFormats.ConvertIds),
    };

}

