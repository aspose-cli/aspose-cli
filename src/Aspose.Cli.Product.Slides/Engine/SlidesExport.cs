using System.Text.RegularExpressions;
using Aspose.Cli.Sdk;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility.Commanding;
using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Rendering;
using Aspose.Cli.Sdk.Results;
using Aspose.Slides;
using Aspose.Slides.Export;
using static Aspose.Cli.Product.Slides.Engine.SlidesRenderSizing;
using static Aspose.Cli.Product.Slides.Engine.SlidesResultParts;
using static Aspose.Cli.Product.Slides.Engine.SlidesShapeText;
using static Aspose.Cli.Product.Slides.Engine.SlidesSlideSelection;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>Conversion and rendering of a presentation or selected slides.</summary>
internal static partial class SlidesExport
{
    internal static SlidesConvertResult Convert(SlidesSession session, SlidesConvertRequest request)
    {
        LicenseState state = session.Outputs.License;
        using LoadedPresentation loaded = session.Loader.Open(request.Input, request.Password);
        Presentation presentation = loaded.Presentation;
        string formatId = request.Output.Format.Id;
        // A slide selection converts every slide it names, hidden or not. Without one, an export
        // leaves the hidden slides out, as PowerPoint's own export does, unless nothing would be
        // left; a presentation format keeps them, hidden as they were.
        IReadOnlyList<int>? slides = request.Slides is null
            ? null
            : ResolveSlideRange(request.Slides, presentation.Slides.Count);
        int[] skipped = slides is null && !KeepsHiddenSlides(SlidesEngineFormats.SaveFormatOf(formatId))
            ? [.. AllSlides(presentation.Slides.Count).Where(number => presentation.Slides[number - 1].Hidden)]
            : [];
        if (skipped.Length == presentation.Slides.Count)
        {
            skipped = [];
        }

        IReadOnlyList<int>? selection = skipped.Length == 0 ? slides : [.. AllSlides(presentation.Slides.Count).Except(skipped)];
        IReadOnlyList<int> shown = selection ?? AllSlides(presentation.Slides.Count);
        if (shown.Count > 1 && SlidesEngineFormats.SavesOneSlide(formatId))
        {
            throw OneSlideOnly(request, shown, skipped.Length > 0);
        }

        // Read before the save, which removes the unselected slides from this private copy for a
        // presentation format, so the slides keep their numbers in the deck.
        Warning? lost = ContentLost(presentation, formatId, shown);
        Warning? pictures = PicturesLeftOut(loaded, request, shown);
        IReadOnlyList<SlidesConvertOutput> outputs = ConvertOutputs(session, loaded, request, selection);
        List<Warning> warnings = BuildConvertWarnings(loaded, formatId, skipped, shown, lost);
        if (pictures is not null)
        {
            warnings.Add(pictures);
        }

        return new SlidesConvertResult
        {
            Input = Source(request.Input, loaded),
            Outputs = outputs,
            Slides = request.Slides?.Text,
            License = EnvelopeParts.License(state),
            Warnings = warnings.Count == 0 ? null : warnings,
        };
    }

    /// <summary>
    /// The refusal of a convert of several slides to a format whose save writes the first slide
    /// alone, a TIFF (SLIDES-TIFF-ONE-FRAME), with the commands that write every slide: 'slides
    /// render', one TIFF per slide and at most <see cref="RenderSelection.MaximumParts"/> slides a
    /// run, and a convert of the slides to PDF, which opens without a password, then 'pdf convert'
    /// of the PDF to one multi-page TIFF.
    /// </summary>
    private static CliException OneSlideOnly(SlidesConvertRequest request, IReadOnlyList<int> shown, bool hiddenSkipped)
    {
        ContinuationInput input = request.Continuation ?? new ContinuationInput(request.Input);
        string tiff = request.Output.Path;
        var requested = ContinuationOutput.Requested(tiff, request.Output.Overwrite);
        // The PDF between the two steps is a new file beside the TIFF.
        var pdf = ContinuationOutput.New(Path.Combine(Path.GetDirectoryName(tiff)!, Path.GetFileNameWithoutExtension(tiff) + ".pdf"));
        static ContinuationCommand Selecting(ContinuationCommand command, string? slides) => slides is null ? command : command.Option("--slides", slides);
        ContinuationCommand render = Selecting(input.ContinuationAt("slides", "render"), request.Slides?.Text ?? (hiddenSkipped ? string.Join(",", shown) : null))
            .Option("--to", request.Output.Format.Id).Out(requested);
        ContinuationCommand toPdf = Selecting(input.ContinuationAt("slides", "convert"), request.Slides?.Text).Option("--to", "pdf").Out(pdf);
        ContinuationCommand toTiff = new ContinuationInput(pdf.Path).ContinuationAt("pdf", "convert")
            .Option("--to", request.Output.Format.Id).Out(requested);
        string perSlide = shown.Count <= RenderSelection.MaximumParts
            ? $"For one TIFF per slide, run {render.Quoted()}, which writes them beside {tiff}"
            : $"For one TIFF per slide, run 'slides render' with --to tiff and --slides on at most {RenderSelection.MaximumParts} slides at a time";
        return CliErrors.EngineDefect(
            $"Aspose.Slides writes only the first slide to a TIFF file, and this conversion selects {shown.Count} slides; nothing was written.",
            $"{perSlide}; for one multi-page TIFF, run {ContinuationCommand.Steps([toPdf, toTiff])}.");
    }

    // Whether a conversion to the format keeps the hidden slides: a presentation format carries
    // them hidden, while an export shows only what a slide show shows.
    private static bool KeepsHiddenSlides(SaveFormat format) =>
        format is SaveFormat.Ppt or SaveFormat.Pptx or SaveFormat.Pptm or SaveFormat.Odp;

    private static IReadOnlyList<SlidesConvertOutput> ConvertOutputs(
        SlidesSession session,
        LoadedPresentation loaded,
        SlidesConvertRequest request,
        IReadOnlyList<int>? slides)
    {
        Presentation presentation = loaded.Presentation;
        SaveFormat format = SlidesEngineFormats.SaveFormatOf(request.Output.Format.Id);
        OutputProtection protection = OutputProtection.For(request.Output.Format, request.EncryptPassword, loaded.Protection, request.Password);
        SlidesOutputProtection.Protect(presentation, protection);

        if (format == SaveFormat.Md)
        {
            IReadOnlyList<int> shown = slides ?? AllSlides(presentation.Slides.Count);
            StateInheritedBullets(presentation, shown);
            JoinHeadingLines(presentation, shown);
        }

        if (slides is not null && Exports(format))
        {
            // A selected slide is converted even when it is hidden; every export but HTML5 and
            // GIF could show it by option, so it is shown in this private copy for all of them.
            foreach (int number in slides)
            {
                presentation.Slides[number - 1].Hidden = false;
            }
        }

        // An export of selected slides shows those slides alone, numbered as in the deck, so only
        // their evaluation marks are its own.
        IReadOnlyList<OutputInfo> written = session.Outputs.Write(request.Output, presentation, protection.Encrypted, temp =>
        {
            Save(temp);
            if (format == SaveFormat.Odp)
            {
                SlidesOdfPackage.PutMimeTypeFirst(temp);
            }

            loaded.Resources.ThrowIfFailed();
        }, pages: slides is not null && Exports(format) ? slides : null);
        return [.. written.Select(static file => new SlidesConvertOutput(file))];

        void Save(string temp)
        {
            if (slides is null)
            {
                if (format == SaveFormat.Md)
                {
                    SaveMarkdown(presentation, temp, null);
                    return;
                }

                presentation.Save(temp, format);
                return;
            }

            if (Exports(format))
            {
                SaveSlides(presentation, temp, slides, format);
                return;
            }

            // Editable formats keep the source's size, properties, masters and protection:
            // the unselected slides leave this private loaded copy instead of cloning into a new deck.
            RemoveUnselectedSlides(presentation, slides);
            presentation.Save(temp, format);
        }
    }

    private static void SaveSlides(Presentation presentation, string temp, IReadOnlyList<int> slides, SaveFormat format)
    {
        if (format == SaveFormat.Md)
        {
            SaveMarkdown(presentation, temp, slides);
            return;
        }

        presentation.Save(temp, [.. slides], format);
    }

    /// <summary>
    /// Writes the bullet each paragraph inherits from its layout or master as its own, in this
    /// private loaded copy, because the Markdown export lists only a paragraph's own bullet and
    /// would write a placeholder's inherited list as plain lines (SLIDES-MD-INHERITED-BULLETS).
    /// </summary>
    private static void StateInheritedBullets(Presentation presentation, IReadOnlyList<int> slides)
    {
        foreach (int number in slides)
        {
            foreach (IParagraph paragraph in presentation.Slides[number - 1].Shapes
                .SelectMany(TextFrames)
                .SelectMany(static frame => frame.Paragraphs)
                .Where(static paragraph => paragraph.ParagraphFormat.Bullet.Type == BulletType.NotDefined && !string.IsNullOrWhiteSpace(paragraph.Text)))
            {
                IBulletFormatEffectiveData inherited = paragraph.ParagraphFormat.GetEffective().Bullet;
                IBulletFormat bullet = paragraph.ParagraphFormat.Bullet;
                switch (inherited.Type)
                {
                    case BulletType.Symbol:
                        bullet.Type = BulletType.Symbol;
                        bullet.Char = inherited.Char;
                        break;
                    case BulletType.Numbered:
                        bullet.Type = BulletType.Numbered;
                        bullet.NumberedBulletStyle = inherited.NumberedBulletStyle;
                        bullet.NumberedBulletStartWith = inherited.NumberedBulletStartWith;
                        break;
                }
            }
        }
    }

    /// <summary>
    /// Saves Markdown that starts each slide with a comment naming its number in the deck,
    /// selected or not; a comment stays out of the rendered text and is not taken for the
    /// slide's heading. The export puts a separator line before every slide's comment; the one
    /// that would open the file goes, since many Markdown tools take a file that opens with
    /// "---" for YAML front matter and hide the first slide's comment in it.
    /// </summary>
    private static void SaveMarkdown(Presentation presentation, string temp, IReadOnlyList<int>? slides)
    {
        // The export makes its image folder under BasePath, the process's working directory
        // unless set, even when it writes no image; a private folder keeps it out of there.
        DirectoryInfo scratch = Directory.CreateTempSubdirectory("aspose-cli-md-");
        try
        {
            var options = new MarkdownSaveOptions
            {
                ExportType = MarkdownExportType.TextOnly,
                ShowSlideNumber = true,
                SlideNumberFormat = "<!-- slide {0} -->",
                BasePath = scratch.FullName,
            };
            if (slides is null)
            {
                presentation.Save(temp, SaveFormat.Md, options);
            }
            else
            {
                presentation.Save(temp, [.. slides], SaveFormat.Md, options);
            }
        }
        finally
        {
            scratch.Delete(recursive: true);
        }

        string markdown = File.ReadAllText(temp).TrimStart();
        if (markdown.StartsWith("---", StringComparison.Ordinal))
        {
            int lineEnd = markdown.IndexOf('\n', StringComparison.Ordinal);
            markdown = lineEnd < 0 ? string.Empty : markdown[(lineEnd + 1)..];
        }

        File.WriteAllText(temp, SeparatorAfterMarker().Replace(markdown, "$1"), new System.Text.UTF8Encoding(false));
    }

    // The export also puts a separator line after every slide's comment, parting the comment
    // from its slide; without it each slide after the first is a separator, its comment and its
    // content.
    [GeneratedRegex(@"^(<!-- slide \d+ -->\r?\n)---[ \t]*\r?\n", RegexOptions.Multiline)]
    private static partial Regex SeparatorAfterMarker();

    /// <summary>
    /// Writes the lines of each title and subtitle as one paragraph, its lines joined by " / ",
    /// in this private loaded copy: the Markdown export writes each paragraph of one as a
    /// heading of its own and runs the lines of a paragraph together where they break.
    /// </summary>
    private static void JoinHeadingLines(Presentation presentation, IReadOnlyList<int> slides)
    {
        foreach (int number in slides)
        {
            foreach (ITextFrame frame in presentation.Slides[number - 1].Shapes.OfType<IAutoShape>()
                .Where(static shape => shape.Placeholder?.Type is PlaceholderType.Title or PlaceholderType.CenteredTitle or PlaceholderType.Subtitle)
                .Select(static shape => shape.TextFrame)
                .OfType<ITextFrame>())
            {
                string[] lines = [.. frame.Paragraphs
                    .SelectMany(static paragraph => paragraph.Text.Split('\v'))
                    .Select(static line => line.Trim())
                    .Where(static line => line.Length > 0)];
                IParagraph? first = frame.Paragraphs.FirstOrDefault(static paragraph => !string.IsNullOrWhiteSpace(paragraph.Text));
                if (lines.Length < 2 || first is null)
                {
                    continue;
                }

                first.Text = string.Join(" / ", lines);
                foreach (IParagraph other in frame.Paragraphs.Where(paragraph => !ReferenceEquals(paragraph, first)).ToArray())
                {
                    frame.Paragraphs.Remove(other);
                }
            }
        }
    }

    // Whether the format is an export, which can show selected slides alone; a presentation
    // format keeps them by removing the others.
    private static bool Exports(SaveFormat format) =>
        format is SaveFormat.Pdf or SaveFormat.Xps or SaveFormat.Html or SaveFormat.Html5
            or SaveFormat.Tiff or SaveFormat.Gif or SaveFormat.Md;

    private static List<Warning> BuildConvertWarnings(
        LoadedPresentation loaded,
        string targetFormatId,
        IReadOnlyList<int> skipped,
        IReadOnlyList<int> shown,
        Warning? lost)
    {
        var warnings = WrittenWarnings(loaded, shown)?.ToList() ?? [];
        if (skipped.Count > 0)
        {
            string numbers = string.Join(", ", skipped);
            warnings.Add(new Warning(SlidesDiagnostics.HiddenSlidesSkipped,
                $"The {targetFormatId} output leaves out the hidden slide(s) {numbers}, as a slide show and PowerPoint's export do.")
            {
                Hint = $"To include them, name every slide to convert with --slides, such as --slides 1-{loaded.Presentation.Slides.Count}; a selected hidden slide is converted.",
            });
        }

        // An export holds the slides alone, with no notes pages, so the speaker notes of the
        // slides it shows are not in it; a presentation format keeps them.
        int[] noted = Exports(SlidesEngineFormats.SaveFormatOf(targetFormatId))
            ? [.. shown.Where(number => Notes(loaded.Presentation.Slides[number - 1]) is not null)]
            : [];
        if (noted.Length > 0)
        {
            warnings.Add(ContentLoss.Warning([ContentKind.SpeakerNotes],
                $"The {targetFormatId} output does not include the speaker notes of slide(s) {string.Join(", ", noted)}: an export holds the slides alone.",
                $"Read the notes with 'slides read --slides {string.Join(",", noted)} --notes'; a pptx output keeps them."));
        }

        if (lost is not null)
        {
            warnings.Add(lost);
        }

        if (SlidesEngineFormats.IsLossy(targetFormatId))
        {
            warnings.Add(new Warning(WarningCodes.LossyConversion, $"Slides conversion to {targetFormatId} may not preserve every presentation feature.")
            {
                Hint = "Keep the source deck and inspect the produced file for layout, animation and interactive-media changes.",
            });
        }

        return warnings;
    }

    /// <summary>
    /// The warning that an output of the slides' text holds none of the pictures they show
    /// (<see cref="SlidesContentLoss.Pictures"/>), counted as the extract its hint names writes
    /// them, each once (<see cref="SlidesExtract.ShownMedia"/>); null when the format keeps them or
    /// the shown slides have none.
    /// </summary>
    private static Warning? PicturesLeftOut(LoadedPresentation loaded, SlidesConvertRequest request, IReadOnlyList<int> shown)
    {
        string format = request.Output.Format.Id;
        int[] pictured = SlidesEngineFormats.Losses(format).HasFlag(SlidesContentLoss.Pictures)
            ? [.. shown.Where(number => SlidesExtract.ShownMedia(loaded.Presentation, [number]).OfType<IPPImage>().Any())]
            : [];
        if (pictured.Length == 0)
        {
            return null;
        }

        string md = request.Output.Path;
        ContinuationCommand extract = (request.Continuation ?? new ContinuationInput(request.Input)).ContinuationAt("slides", "extract")
            .Option("--what", SlidesExtractKinds.Media).Option("--slides", string.Join(",", pictured))
            .OutDir(ContinuationOutput.New(Path.Combine(Path.GetDirectoryName(md)!, Path.GetFileNameWithoutExtension(md) + ".pictures")));
        return ContentLoss.Warning([ContentKind.Pictures],
            $"The {format} output holds the slides' text alone and leaves out the {SlidesExtract.ShownMedia(loaded.Presentation, pictured).OfType<IPPImage>().Count()} picture(s) shown on slide(s) {string.Join(", ", pictured)}.",
            $"Extract them with {extract.Quoted()}, which writes each of them once as a media.image file, beside the audio and video of those slides; a pptx output keeps them.");
    }

    // What a save in the format does not keep of the slides it shows (SlidesEngineFormat.Loses),
    // named by their numbers in the deck: an ODP save (SLIDES-ODP-NATIVE-CONTENT) turns their
    // native charts into pictures and drops the transitions and the sections; a PPT save
    // (SLIDES-PPT-NATIVE-CONTENT) turns the charts into pictures and drops the table styles, and
    // the format holds no sections; an HTML5 export draws the charts as pictures, with no text a
    // reader finds. A selection keeps every section, so the sections are the deck's. Null when the
    // shown slides have none of what the format loses. The slides look as in the deck unless a
    // table loses its style.
    private static Warning? ContentLost(Presentation presentation, string format, IReadOnlyList<int> shown)
    {
        SlidesContentLoss loses = SlidesEngineFormats.Losses(format);
        if (loses == SlidesContentLoss.None)
        {
            return null;
        }

        int[] charts = Slides(SlidesContentLoss.Charts, static slide => Has(slide.Shapes, static shape => shape is Aspose.Slides.Charts.IChart));
        int[] transitions = Slides(SlidesContentLoss.Transitions, static slide => slide.SlideShowTransition.Type != Aspose.Slides.SlideShow.TransitionType.None);
        int sections = loses.HasFlag(SlidesContentLoss.Sections) ? presentation.Sections.Count : 0;
        // A table without a style, or with the style that draws nothing, has nothing to lose.
        int[] tables = Slides(SlidesContentLoss.TableStyles, static slide => Has(slide.Shapes, static shape =>
            shape is ITable { StylePreset: not (TableStylePreset.None or TableStylePreset.NoStyleNoGrid) }));
        (string Text, ContentKind Kind)[] lost =
        [
            .. charts.Length == 0 ? [] : new[] { ($"the native charts of slide(s) {string.Join(", ", charts)}, which become pictures", ContentKind.Charts) },
            .. transitions.Length == 0 ? [] : new[] { ($"the transitions of slide(s) {string.Join(", ", transitions)}", ContentKind.Transitions) },
            .. sections == 0 ? [] : new[] { ($"the {sections} section(s)", ContentKind.Sections) },
            .. tables.Length == 0 ? [] : new[] { ($"the table styles of slide(s) {string.Join(", ", tables)}, so those tables lose the fills, borders and text formatting of their style", ContentKind.TableStyles) },
        ];
        if (lost.Length == 0)
        {
            return null;
        }

        string[] kept =
        [
            .. loses.HasFlag(SlidesContentLoss.Charts) ? new[] { "the charts editable" } : [],
            .. loses.HasFlag(SlidesContentLoss.Transitions) ? new[] { "the transitions" } : [],
            .. loses.HasFlag(SlidesContentLoss.Sections) ? new[] { "the sections" } : [],
            .. loses.HasFlag(SlidesContentLoss.TableStyles) ? new[] { "the table styles" } : [],
        ];
        string keep = kept.Length == 1 ? kept[0] : $"{string.Join(", ", kept[..^1])} and {kept[^1]}";
        return ContentLoss.Warning([.. lost.Select(static item => item.Kind)],
            $"The {format} output does not keep {string.Join("; ", lost.Select(static item => item.Text))}{(tables.Length == 0 ? ", though its slides look as in the deck." : ".")}",
            $"Keep the source deck for further edits; to keep {keep}, convert to pptx instead.");

        int[] Slides(SlidesContentLoss kind, Func<ISlide, bool> holds) => loses.HasFlag(kind)
            ? [.. shown.Where(number => holds(presentation.Slides[number - 1]))]
            : [];

        static bool Has(IShapeCollection shapes, Func<IShape, bool> kind) => SlidesTextUnits.Shapes(shapes).Any(found => kind(found.Shape));
    }

    internal static SlidesRenderResult Render(SlidesSession session, SlidesRenderRequest request)
    {
        LicenseState state = session.Outputs.License;
        using LoadedPresentation loaded = session.Loader.Open(request.Input, request.Password);
        IReadOnlyList<int> slides = RenderSelection.Resolve(request.Slides, loaded.Presentation.Slides.Count, SlidesDiagnostics.SlideNotFound, "slide");
        IReadOnlyList<SlidesSlideOutput> outputs = RenderImages(session, loaded, request, slides);
        return new SlidesRenderResult
        {
            Input = Source(request.Input, loaded),
            Outputs = outputs,
            Dpi = !request.Output.Format.Raster || request.Width is not null
                ? null
                : request.Dpi ?? RenderPixelGuard.DefaultDpi,
            Width = request.Output.Format.Raster ? request.Width : null,
            License = EnvelopeParts.License(state),
            Warnings = EnvelopeParts.CombineWarnings(WrittenWarnings(loaded, slides), HiddenRendered(loaded.Presentation, slides)),
        };
    }

    /// <summary>
    /// The warning that a render or review drew hidden slides, which a slide show and a
    /// conversion of every slide leave out; null when none of the drawn slides is hidden.
    /// </summary>
    internal static IReadOnlyList<Warning>? HiddenRendered(Presentation presentation, IEnumerable<int> slides)
    {
        int[] hidden = [.. slides.Where(number => presentation.Slides[number - 1].Hidden)];
        return hidden.Length == 0
            ? null
            : [new Warning(SlidesDiagnostics.HiddenSlidesRendered,
                $"The rendered slide(s) {string.Join(", ", hidden)} are hidden: a slide show and a convert of every slide leave them out.")
            {
                Hint = $$"""To show them in the deck, edit it with {"op":"set_slide_hidden","slides":"{{string.Join(",", hidden)}}","hidden":false}; otherwise leave them out of what you report as the deck's content.""",
            }];
    }

    private static IReadOnlyList<SlidesSlideOutput> RenderImages(
        SlidesSession session,
        LoadedPresentation loaded,
        SlidesRenderRequest request,
        IReadOnlyList<int> slides)
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
        foreach ((int number, string path) in slides.Zip(request.Output.Parts(slides)))
        {
            ISlide slide = presentation.Slides[number - 1];
            targets.Add(number);
            transaction.Stage(path, request.Output.Format, request.Output.Overwrite, presentation, temp =>
            {
                if (image is not { } bitmap)
                {
                    using FileStream stream = File.Create(temp);
                    slide.WriteAsSvg(stream);
                    return;
                }

                using IImage drawn = slide.GetImage(scale, scale);
                using FileStream outputStream = File.Create(temp);
                drawn.Save(outputStream, bitmap, quality: 92);
            }, rendering: true, pages: [number]);
        }

        loaded.Resources.ThrowIfFailed();
        IReadOnlyList<PublishedFile> files = transaction.Commit();
        return [.. targets.Select((number, index) => new SlidesSlideOutput(FileObjects.Output(files[index], request.Output.Format, encrypted: false)) { Slide = number })];
    }
}
