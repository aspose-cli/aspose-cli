using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Rendering;
using Aspose.Cli.Sdk.Results;
using Aspose.Slides;
using Aspose.Slides.Export;
using static Aspose.Cli.Product.Slides.Engine.SlidesEngineSupport;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>Conversion and rendering of a presentation or selected slides.</summary>
internal static class SlidesExport
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
        int[] skipped = slides is null && !KeepsHiddenSlides(SlidesEngineFormats.IsSlideImage(formatId) ? null : SlidesEngineFormats.SaveFormatOf(formatId))
            ? [.. AllSlides(presentation.Slides.Count).Where(number => presentation.Slides[number - 1].Hidden)]
            : [];
        if (skipped.Length == presentation.Slides.Count)
        {
            skipped = [];
        }

        IReadOnlyList<int>? selection = skipped.Length == 0 ? slides : [.. AllSlides(presentation.Slides.Count).Except(skipped)];
        IReadOnlyList<SlidesSlideOutput> outputs = ConvertOutputs(session, loaded, request, selection);
        List<Warning> warnings = BuildConvertWarnings(loaded, formatId, skipped);
        return new SlidesConvertResult
        {
            Input = Source(request.Input, loaded),
            Outputs = outputs,
            Slides = request.Slides?.Text,
            License = EnvelopeParts.License(state),
            Warnings = warnings.Count == 0 ? null : warnings,
        };
    }

    // Whether a conversion to the format keeps the hidden slides: a presentation format carries
    // them hidden, while an export, images included (null), shows only what a slide show shows.
    private static bool KeepsHiddenSlides(SaveFormat? format) =>
        format is SaveFormat.Ppt or SaveFormat.Pptx or SaveFormat.Pptm or SaveFormat.Odp;

    private static IReadOnlyList<SlidesSlideOutput> ConvertOutputs(
        SlidesSession session,
        LoadedPresentation loaded,
        SlidesConvertRequest request,
        IReadOnlyList<int>? slides)
    {
        Presentation presentation = loaded.Presentation;
        if (SlidesEngineFormats.IsSlideImage(request.Output.Format.Id))
        {
            IReadOnlyList<int> selected = slides ?? AllSlides(presentation.Slides.Count);
            return RenderImages(
                    session,
                    loaded,
                    new SlidesRenderRequest { Input = request.Input, Output = request.Output },
                    selected,
                    "slides-convert");
        }

        SaveFormat format = SlidesEngineFormats.SaveFormatOf(request.Output.Format.Id);
        // A format that can carry a password keeps the source's encryption unless --encrypt
        // replaces it; any other is written without it, which the write pipeline discloses.
        bool protectable = request.Output.Format.Protectable;
        bool encrypted = protectable && (request.EncryptPassword is not null || presentation.ProtectionManager.IsEncrypted);
        if (!protectable && presentation.ProtectionManager.IsEncrypted)
        {
            presentation.ProtectionManager.RemoveEncryption();
        }

        if (format == SaveFormat.Md)
        {
            StateInheritedBullets(presentation, slides ?? AllSlides(presentation.Slides.Count));
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
        IReadOnlyList<OutputInfo> written = session.Outputs.Write(request.Output, presentation, encrypted, temp =>
        {
            Save(temp);
            if (format == SaveFormat.Odp)
            {
                SlidesOdfPackage.PutMimeTypeFirst(temp);
            }

            loaded.Resources.ThrowIfFailed();
        }, pages: slides is not null && Exports(format) ? slides : null);
        return [.. written.Select(static file => new SlidesSlideOutput(file))];

        void Save(string temp)
        {
            if (slides is null)
            {
                Encrypt(presentation, request.EncryptPassword);
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
            Encrypt(presentation, request.EncryptPassword);
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
            File.WriteAllText(temp, lineEnd < 0 ? string.Empty : markdown[(lineEnd + 1)..], new System.Text.UTF8Encoding(false));
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
        IReadOnlyList<int> skipped)
    {
        var warnings = WrittenWarnings(loaded)?.ToList() ?? [];
        if (skipped.Count > 0)
        {
            string numbers = string.Join(", ", skipped);
            warnings.Add(new Warning(SlidesDiagnostics.HiddenSlidesSkipped,
                $"The {targetFormatId} output leaves out the hidden slide(s) {numbers}, as a slide show and PowerPoint's export do.")
            {
                Hint = $"To include them, name every slide to convert with --slides, such as --slides 1-{loaded.Presentation.Slides.Count}; a selected hidden slide is converted.",
            });
        }

        if (targetFormatId == "odp" && OdpLosses(loaded.Presentation) is { } losses)
        {
            warnings.Add(new Warning(WarningCodes.LossyConversion, $"The odp output does not keep {losses}, though its slides look as in the deck.")
            {
                Hint = "Keep the source deck for further edits; to keep the charts editable, the transitions and the sections, convert to pptx instead.",
            });
        }

        if (targetFormatId is "html" or "html5" or "md")
        {
            warnings.Add(new Warning(WarningCodes.LossyConversion, $"Slides conversion to {targetFormatId} may not preserve every presentation feature.")
            {
                Hint = "Keep the source deck and inspect the produced file for layout, animation and interactive-media changes.",
            });
        }

        return warnings;
    }

    // What an ODP save of the presentation does not keep (SLIDES-ODP-NATIVE-CONTENT): its native charts, which become
    // pictures, the slide transitions and the sections; null when it has none of them.
    private static string? OdpLosses(Presentation presentation)
    {
        int[] charts = [.. AllSlides(presentation.Slides.Count).Where(number => HasChart(presentation.Slides[number - 1].Shapes))];
        int[] transitions = [.. AllSlides(presentation.Slides.Count)
            .Where(number => presentation.Slides[number - 1].SlideShowTransition.Type != Aspose.Slides.SlideShow.TransitionType.None)];
        string[] lost =
        [
            .. charts.Length == 0 ? [] : new[] { $"the native charts of slide(s) {string.Join(", ", charts)}, which become pictures" },
            .. transitions.Length == 0 ? [] : new[] { $"the transitions of slide(s) {string.Join(", ", transitions)}" },
            .. presentation.Sections.Count == 0 ? [] : new[] { $"the {presentation.Sections.Count} section(s)" },
        ];
        return lost.Length == 0 ? null : string.Join("; ", lost);

        static bool HasChart(IShapeCollection shapes) =>
            shapes.Any(static shape => shape is Aspose.Slides.Charts.IChart || (shape is IGroupShape group && HasChart(group.Shapes)));
    }

    internal static SlidesRenderResult Render(SlidesSession session, SlidesRenderRequest request)
    {
        LicenseState state = session.Outputs.License;
        using LoadedPresentation loaded = session.Loader.Open(request.Input, request.Password);
        IReadOnlyList<int> slides = RenderSelection.Resolve(request.Slides, loaded.Presentation.Slides.Count, SlidesDiagnostics.SlideNotFound, "slide");
        IReadOnlyList<SlidesSlideOutput> outputs = RenderImages(session, loaded, request, slides, "slides-render");
        return new SlidesRenderResult
        {
            Input = Source(request.Input, loaded),
            Outputs = outputs,
            Dpi = request.Output.Format.Id == "svg" || request.Width is not null
                ? null
                : request.Dpi ?? RenderPixelGuard.DefaultDpi,
            Width = request.Output.Format.Id == "svg" ? null : request.Width,
            License = EnvelopeParts.License(state),
            Warnings = WrittenWarnings(loaded),
        };
    }

    private static IReadOnlyList<SlidesSlideOutput> RenderImages(
        SlidesSession session,
        LoadedPresentation loaded,
        SlidesRenderRequest request,
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
                request.Width is null ? request.Dpi ?? RenderPixelGuard.DefaultDpi : null);
        }

        string directory = request.Output.Directory;
        using OutputSet<Presentation> transaction = session.Outputs.BeginSet([directory], transactionName);
        var targets = new List<int>(slides.Count);
        foreach ((int number, string path) in slides.Zip(request.Output.Parts(slides)))
        {
            ISlide slide = presentation.Slides[number - 1];
            targets.Add(number);
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
        IReadOnlyList<PublishedFile> files = transaction.Commit();
        return [.. targets.Select((number, index) => new SlidesSlideOutput(FileObjects.Output(files[index], request.Output.Format, encrypted: false)) { Slide = number })];
    }
}
