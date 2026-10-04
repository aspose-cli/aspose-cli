using System.Globalization;
using System.Text.Json.Nodes;
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
        Message = "Aspose.Slides evaluation mode reads text longer than five characters as its first characters and a truncation notice, so the text in this result is not the presentation's text.",
        Hint = "Check the text in rendered slide images, which show it in full, or apply a license and retry. Presentations, PDFs and images the CLI saves keep the full text.",
    };

    internal static void RemoveUnselectedSlides(Presentation presentation, IReadOnlyList<int> slides)
    {
        var keep = new HashSet<int>(slides);
        ISlide[] unselected = presentation.Slides.Where((_, index) => !keep.Contains(index + 1)).ToArray();
        foreach (ISlide slide in unselected)
        {
            presentation.Slides.Remove(slide);
        }
    }

    internal static SlideInfo ProjectInfo(ISlide slide, int number, IReadOnlyList<IComment> comments, bool preview)
    {
        string? title = preview ? Title(slide) : null;
        return new SlideInfo
        {
            Slide = number,
            SlideId = slide.SlideId,
            Name = EmptyToNull(slide.Name),
            Layout = EmptyToNull(slide.LayoutSlide?.Name),
            Title = title,
            PreviewText = preview ? PreviewText(slide) : null,
            ShapeCount = slide.Shapes.Count,
            Hidden = slide.Hidden,
            HasNotes = !string.IsNullOrWhiteSpace(Notes(slide)),
            CommentCount = comments.Count(comment => ReferenceEquals(comment.Slide, slide)),
        };
    }

    internal static SlideData ProjectSlide(
        ISlide slide,
        int number,
        string scope,
        bool includeNotes,
        IReadOnlyList<IComment> comments,
        bool evaluation,
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
                    ShapeName = EmptyToNull(shape.Name),
                    Type = ShapeTypeName(shape),
                    Placeholder = SlidesPlaceholders.Role(shape.Placeholder?.Type),
                    AltText = EmptyToNull(shape.AlternativeText),
                    Text = text,
                    Runs = scope == PresentationReadScopes.Full ? Runs(shape, ref remaining, ref contentTruncated) : null,
                    ZOrder = zOrder++,
                    HasOpaqueFill = SlidesReviewProjection.HasOpaqueFill(shape),
                    TextRect = scope == PresentationReadScopes.Full ? SlidesReviewProjection.TextRect(shape) : null,
                    TextAutofits = scope == PresentationReadScopes.Full && SlidesReviewProjection.TextAutofits(shape),
                    EvaluationWatermark = evaluation && IsEvaluationWatermark(shape),
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
            Slide = number,
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

    /// <summary>
    /// The text a shape shows: its own frame, table cells, group children and SmartArt nodes,
    /// one paragraph or line break per line.
    /// </summary>
    internal static string? ShapeText(IShape shape) =>
        EmptyToNull(string.Join(
            "\n",
            TextFrames(shape).Select(static frame => Lines(frame.Text)).Where(static text => !string.IsNullOrWhiteSpace(text))));

    /// <summary>
    /// Ends each line of engine text with a line feed: the engine ends paragraphs with a
    /// carriage return and line breaks with a vertical tab. Offsets into the text stay valid.
    /// </summary>
    private static string? Lines(string? text) => text?.Replace('\r', '\n').Replace('\v', '\n');

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

    /// <summary>Warnings about what a result read from the loaded presentation may be missing.</summary>
    internal static IReadOnlyList<Warning>? InputWarnings(LicenseState state, LoadedPresentation loaded, bool textRead = true) =>
        Warnings(state, loaded, output: false, textRead);

    /// <summary>
    /// Whether a shape is the watermark text box an evaluation save adds to every slide: a
    /// select- and position-locked shape, not a placeholder, whose text starts with the
    /// evaluation notice, which evaluation mode itself reads cut short.
    /// </summary>
    internal static bool IsEvaluationWatermark(IShape shape) =>
        shape is IAutoShape { Placeholder: null, TextFrame: { } frame } box
        && box.ShapeLock is { SelectLocked: true, PositionLocked: true }
        && frame.Text?.StartsWith("Evalu", StringComparison.Ordinal) == true;

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
                    Color = effective.FillFormat.FillType == FillType.Solid
                        ? SlidesReviewProjection.Hex(effective.FillFormat.SolidFillColor)
                        : null,
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
        EmptyToNull(Lines(slide.NotesSlideManager.NotesSlide?.NotesTextFrame?.Text));

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

        return media;
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


    internal static void Encrypt(Presentation presentation, string? password)
    {
        if (password is not null)
        {
            presentation.ProtectionManager.Encrypt(password);
        }
    }

    /// <summary>
    /// The warnings of an output produced from the presentation, with its evaluation watermark.
    /// Evaluation mode cuts text short only where it is read, so the output warns of it only
    /// when the output holds text the CLI read, as extracted text and Markdown do; saved
    /// presentations, PDFs and images keep the full text.
    /// </summary>
    internal static IReadOnlyList<Warning>? OutputWarnings(LicenseState state, LoadedPresentation loaded, bool textRead = false) =>
        Warnings(state, loaded, output: true, textRead);

    private static IReadOnlyList<Warning>? Warnings(LicenseState state, LoadedPresentation loaded, bool output, bool textRead)
    {
        var warnings = new List<Warning>();
        if (output && state == LicenseState.Evaluation)
        {
            warnings.Add(EnvelopeParts.EvaluationWatermark);
        }

        if (textRead && state == LicenseState.Evaluation && EvaluationInputTruncated(loaded.Presentation))
        {
            warnings.Add(EvaluationInputWarning);
        }

        if (loaded.Resources.Warning is { } omitted)
        {
            warnings.Add(omitted);
        }

        if (output)
        {
            foreach ((int slide, string chart) in loaded.ImplicitTitleCharts)
            {
                warnings.Add(new Warning
                {
                    Code = SlidesDiagnostics.ChartTitleOverlaid,
                    Message = $"The chart '{chart}' on slide {slide} has an implicit automatic title, which this output draws over the plot, enlarging the plot area.",
                    Hint = "Inspect the chart in PowerPoint and restore its title layout there if it changed.",
                    Location = string.Create(CultureInfo.InvariantCulture, $"slide {slide}"),
                    AffectsCompleteness = true,
                });
            }
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

    /// <summary>The slide numbers a range selects; a presentation without slides has none to select.</summary>
    internal static IReadOnlyList<int> ResolveSlideRange(PageRange range, int slideCount) =>
        range.Resolve(slideCount, SlidesDiagnostics.SlideNotFound, "slide");

    /// <summary>Every slide number, as the range <c>1-</c> selects them.</summary>
    internal static IReadOnlyList<int> AllSlides(int slideCount) =>
        ResolveSlideRange(PageRange.Parse("1-"), slideCount);

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

