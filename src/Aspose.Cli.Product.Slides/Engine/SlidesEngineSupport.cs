using System.Globalization;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Rendering;
using Aspose.Slides;
using Aspose.Slides.Charts;
using Aspose.Slides.SmartArt;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>Shared Slides projection, formatting, range, and output helpers.</summary>
internal static class SlidesEngineSupport
{
    internal const string EvaluationTruncationMarker = "truncated due to evaluation version limitation";

    internal static void RemoveUnselectedSlides(Presentation presentation, IReadOnlyList<int> slides)
    {
        var keep = new HashSet<int>(slides);
        ISlide[] unselected = presentation.Slides.Where((_, index) => !keep.Contains(index + 1)).ToArray();
        foreach (ISlide slide in unselected)
        {
            presentation.Slides.Remove(slide);
        }
    }

    internal static SlidesSlideInfo ProjectInfo(ISlide slide, int number, IReadOnlyList<IComment> comments, bool preview)
    {
        string? title = preview ? Title(slide) : null;
        return new SlidesSlideInfo
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

    internal static SlidesSlideData ProjectSlide(
        ISlide slide,
        int number,
        string content,
        bool includeNotes,
        IReadOnlyList<IComment> comments,
        ref int remaining)
    {
        bool includeShapes = content is SlidesReadContents.Shapes or SlidesReadContents.Full;
        bool contentTruncated = false;
        string? title = Take(Title(slide), ref remaining, ref contentTruncated);
        var textBlocks = content == SlidesReadContents.Text ? new List<string>() : null;
        var shapes = new List<SlidesShapeData>();
        bool full = content == SlidesReadContents.Full;
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
                shapes.Add(SlidesStateProjection.Shape(shape, text, full, ref remaining, ref contentTruncated));
            }
        }
        string? notes = full || includeNotes ? Take(Notes(slide), ref remaining, ref contentTruncated) : null;
        var read = new SlidesSlideData
        {
            Slide = number,
            SlideId = slide.SlideId,
            Name = EmptyToNull(slide.Name),
            Layout = EmptyToNull(slide.LayoutSlide?.Name),
            Title = title,
            Text = textBlocks,
            Shapes = shapes,
            Notes = notes is null ? null : new SlidesNotesData { Text = notes },
            ContentTruncated = contentTruncated,
        };
        if (!full)
        {
            return read;
        }

        // --content full also reports the comments, footer, background and transition.
        IReadOnlyList<SlidesCommentData> slideComments = SlideComments(slide, comments, ref remaining, ref contentTruncated);
        SlidesFooterData footer = SlidesStateProjection.Footer(slide, ref remaining, ref contentTruncated);
        return read with
        {
            Comments = slideComments,
            Footer = footer,
            Background = SlidesStateProjection.Background(slide),
            Transition = SlidesStateProjection.Transition(slide),
            ContentTruncated = contentTruncated,
        };
    }

    private static List<SlidesCommentData> SlideComments(
        ISlide slide,
        IReadOnlyList<IComment> comments,
        ref int remaining,
        ref bool truncated)
    {
        var projected = new List<SlidesCommentData>();
        foreach (IComment comment in comments.Where(comment => ReferenceEquals(comment.Slide, slide)))
        {
            if (remaining == 0 && !string.IsNullOrWhiteSpace(comment.Text))
            {
                truncated = true;
                break;
            }

            projected.Add(new SlidesCommentData
            {
                Author = comment.Author.Name,
                Text = Take(comment.Text, ref remaining, ref truncated) ?? string.Empty,
            });
        }

        return projected;
    }

    internal static string? Take(string? value, ref int remaining, ref bool truncated)
    {
        if (Clean(value) is not { } clean)
        {
            return null;
        }

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
    /// Text as results report it: each line keeps its words, one space apart, and the lines stay
    /// apart, as inspect and search show them; null for text that shows nothing.
    /// </summary>
    internal static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : string.Join(
                "\n",
                value.Split('\n')
                    .Select(static line => string.Join(" ", line.Split(default(string[]), StringSplitOptions.RemoveEmptyEntries)))
                    .Where(static line => line.Length > 0));

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

    /// <summary>
    /// Warnings about what a result read from the loaded presentation may be missing. A result
    /// that reads the presentation's text tells the write pipeline
    /// (<c>ReadInput</c>), which discloses the text evaluation mode cut short.
    /// </summary>
    internal static IReadOnlyList<Warning>? InputWarnings(LoadedPresentation loaded) =>
        Warnings(loaded, output: false);

    /// <summary>
    /// Whether a shape is the watermark text box an evaluation save adds to every slide: a
    /// select- and position-locked shape, not a placeholder, whose text is the evaluation notice
    /// "Evaluation only." followed by "Created with Aspose.Slides ...". Evaluation mode reads each
    /// run of it cut to its first characters, so there the runs must start "Evalu" and "Creat";
    /// a box of the user's that only starts "Evaluation" is never taken for the watermark.
    /// </summary>
    internal static bool IsEvaluationWatermark(IShape shape)
    {
        if (shape is not IAutoShape { Placeholder: null, TextFrame: { } frame } box
            || box.ShapeLock is not { SelectLocked: true, PositionLocked: true })
        {
            return false;
        }

        string[] runs = [.. frame.Paragraphs.SelectMany(static paragraph => paragraph.Portions)
            .Select(static portion => portion.Text)
            .Where(static text => !string.IsNullOrWhiteSpace(text))];
        return runs.Any(CutByEvaluation)
            ? runs is [{ } evaluation, { } created, ..]
                && evaluation.StartsWith("Evalu", StringComparison.Ordinal)
                && created.StartsWith("Creat", StringComparison.Ordinal)
            : frame.Text is { } text
                && text.StartsWith("Evaluation only.", StringComparison.Ordinal)
                && text.Contains("Created with Aspose.Slides", StringComparison.Ordinal);
    }

    /// <summary>Whether evaluation mode cut short text it read from the presentation, other than its own watermark.</summary>
    internal static bool EvaluationInputTruncated(Presentation presentation) =>
        presentation.Slides.Any(static slide =>
            slide.Shapes.Any(static shape => !IsEvaluationWatermark(shape) && CutByEvaluation(ShapeText(shape)))
            || CutByEvaluation(Notes(slide)));

    /// <summary>Whether evaluation mode replaced the read text with its truncation notice.</summary>
    internal static bool CutByEvaluation(string? text) =>
        text?.Contains(EvaluationTruncationMarker, StringComparison.OrdinalIgnoreCase) == true;

    internal static IReadOnlyList<SlidesTextRunData>? Runs(
        IShape shape,
        ref int remaining,
        ref bool truncated)
    {
        var runs = new List<SlidesTextRunData>();
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
                runs.Add(new SlidesTextRunData
                {
                    Text = text,
                    Font = EmptyToNull(effective.LatinFont?.FontName),
                    EastAsianFont = EmptyToNull(effective.EastAsianFont?.FontName),
                    ComplexScriptFont = EmptyToNull(effective.ComplexScriptFont?.FontName),
                    LatinFont = EmptyToNull(portion.PortionFormat.LatinFont?.FontName),
                    FontSize = effective.FontHeight,
                    Bold = effective.FontBold,
                    Italic = effective.FontItalic,
                    Color = RunColor(effective),
                });
            }
        }

        return runs.Count == 0 ? null : runs;
    }

    /// <summary>The solid color a run's text is drawn in, stated or inherited, as #RRGGBB; null for text without a solid fill.</summary>
    internal static string? RunColor(IBasePortionFormatEffectiveData effective) =>
        effective.FillFormat.FillType == FillType.Solid ? SlidesContrast.Hex(effective.FillFormat.SolidFillColor) : null;

    /// <summary>The geometry of an autoshape that insert_shape draws, or null.</summary>
    internal static string? Geometry(IShape shape) => shape is IAutoShape auto && auto.Placeholder is null
        ? auto.ShapeType switch
        {
            ShapeType.Rectangle => SlidesShapeGeometries.Rectangle,
            ShapeType.RoundCornerRectangle => SlidesShapeGeometries.RoundedRectangle,
            ShapeType.Ellipse => SlidesShapeGeometries.Ellipse,
            ShapeType.Line => SlidesShapeGeometries.Line,
            ShapeType.Chevron => SlidesShapeGeometries.Chevron,
            _ => null,
        }
        : null;

    internal static string ShapeKind(IShape shape) => shape switch
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
            <= SlidesSlideInfo.PreviewTextLength => value,
            _ => value[..SlidesSlideInfo.PreviewTextLength],
        };
    }

    /// <summary>
    /// The slide's title: the text of its title placeholder, none when that placeholder is
    /// empty; on a slide without a title placeholder, the text of its first shape with text.
    /// </summary>
    internal static string? Title(ISlide slide)
    {
        IAutoShape[] shapes = [.. slide.Shapes.OfType<IAutoShape>()];
        IAutoShape? title = shapes.Any(static shape => SlidesPlaceholders.IsTitle(shape.Placeholder))
            ? shapes.FirstOrDefault(static shape =>
                SlidesPlaceholders.IsTitle(shape.Placeholder)
                && !string.IsNullOrWhiteSpace(shape.TextFrame?.Text))
            : shapes.FirstOrDefault(static shape => !string.IsNullOrWhiteSpace(shape.TextFrame?.Text));
        return EmptyToNull(title?.TextFrame?.Text);
    }

    internal static string? Notes(ISlide slide) =>
        EmptyToNull(Lines(slide.NotesSlideManager.NotesSlide?.NotesTextFrame?.Text));

    internal static IComment[] Comments(Presentation presentation) =>
        presentation.CommentAuthors
            .SelectMany(static author => author.Comments.Cast<IComment>())
            .ToArray();

    internal static IReadOnlyList<SlidesMediaInfo> Media(Presentation presentation)
    {
        var media = new List<SlidesMediaInfo>();
        foreach (IPPImage image in presentation.Images)
        {
            media.Add(new SlidesMediaInfo
            {
                Index = media.Count + 1,
                Kind = "image",
                ContentType = EmptyToNull(image.ContentType),
                SizeBytes = image.BinaryData.LongLength,
            });
        }

        foreach (IAudio audio in presentation.Audios)
        {
            media.Add(new SlidesMediaInfo
            {
                Index = media.Count + 1,
                Kind = "audio",
                ContentType = EmptyToNull(audio.ContentType),
                SizeBytes = audio.BinaryData.LongLength,
            });
        }

        foreach (IVideo video in presentation.Videos)
        {
            media.Add(new SlidesMediaInfo
            {
                Index = media.Count + 1,
                Kind = "video",
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

    internal static float RenderScale(Presentation presentation, SlidesRenderRequest request)
    {
        if (request.Width is int width)
        {
            return (float)(width / presentation.SlideSize.Size.Width);
        }

        return (request.Dpi ?? RenderPixelGuard.DefaultDpi) / 72f;
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
    internal static void ApplySlideSize(Presentation presentation, string? preset)
    {
        if (preset is null)
        {
            return;
        }

        presentation.SlideSize.SetSize(
            preset == SlidesSlideSizePresets.Widescreen ? SlideSizeType.OnScreen16x9 : SlideSizeType.OnScreen,
            SlideSizeScaleType.EnsureFit);
    }

    /// <summary>
    /// The warnings of an output produced from the presentation; the write pipeline adds the
    /// evaluation disclosure. Evaluation mode cuts text short only where it is read: the
    /// pipeline finds it in a Markdown output (<see cref="SlidesEvaluationProfile"/>), and an
    /// extraction of text tells the pipeline it read the presentation; saved presentations,
    /// PDFs and images keep the full text.
    /// </summary>
    internal static IReadOnlyList<Warning>? WrittenWarnings(LoadedPresentation loaded) =>
        Warnings(loaded, output: true);

    private static IReadOnlyList<Warning>? Warnings(LoadedPresentation loaded, bool output)
    {
        var warnings = new List<Warning>();
        if (loaded.Resources.Warning is { } omitted)
        {
            warnings.Add(omitted);
        }

        if (output)
        {
            foreach ((int slide, string chart) in loaded.ImplicitTitleCharts)
            {
                warnings.Add(new Warning(SlidesDiagnostics.ChartTitleOverlaid, $"The chart '{chart}' on slide {slide} has an implicit automatic title, which this output draws over the plot, enlarging the plot area.")
                {
                    Hint = "Inspect the chart in PowerPoint and restore its title layout there if it changed.",
                    Location = string.Create(CultureInfo.InvariantCulture, $"slide {slide}"),
                    AffectsCompleteness = true,
                });
            }
        }

        return warnings.Count == 0 ? null : warnings;
    }

    internal static InputInfo Source(string path, LoadedPresentation loaded) =>
        FileObjects.Input(path, loaded.FormatId, loaded.Encrypted);

    internal static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>The slide numbers a range selects; a presentation without slides has none to select.</summary>
    internal static IReadOnlyList<int> ResolveSlideRange(PageRange range, int slideCount) =>
        range.Resolve(slideCount, SlidesDiagnostics.SlideNotFound, "slide");

    /// <summary>Every slide number, as the range <c>1-</c> selects them.</summary>
    internal static IReadOnlyList<int> AllSlides(int slideCount) =>
        ResolveSlideRange(PageRange.Parse("1-"), slideCount);
}
