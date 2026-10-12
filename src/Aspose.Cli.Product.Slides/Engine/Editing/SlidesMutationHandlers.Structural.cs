using Aspose.Slides;
using static Aspose.Cli.Product.Slides.Engine.Editing.SlidesMutationSupport;

namespace Aspose.Cli.Product.Slides.Engine.Editing;

// Slide and section structure.
internal sealed partial class SlidesMutationHandlers
{
    public ValueTuple Apply(InsertSlideOp operation)
    {
        int at = operation.At ?? _presentation.Slides.Count + 1;
        if (at > _presentation.Slides.Count + 1)
        {
            throw SlideNotFound(at, _presentation.Slides.Count + 1, "slide position");
        }

        ISlide slide = _presentation.Slides.AddEmptySlide(_target.Layout ?? _presentation.LayoutSlides[0]);
        if (at <= _presentation.Slides.Count)
        {
            _presentation.Slides.Reorder(at - 1, slide);
        }

        return default;
    }

    public ValueTuple Apply(DeleteSlidesOp operation)
    {
        if (Slides.Count == _presentation.Slides.Count)
        {
            throw new OperationInvalidException("A presentation must retain at least one slide.");
        }

        foreach (ISlide slide in Slides)
        {
            _presentation.Slides.Remove(slide);
        }

        return default;
    }

    public ValueTuple Apply(MoveSlideOp operation)
    {
        if (operation.To > _presentation.Slides.Count)
        {
            throw SlideNotFound(operation.To, _presentation.Slides.Count);
        }

        _presentation.Slides.Reorder(operation.To - 1, Slide);
        return default;
    }

    public ValueTuple Apply(CopySlideOp operation)
    {
        int at = operation.At ?? _presentation.Slides.Count + 1;
        if (at > _presentation.Slides.Count + 1)
        {
            throw SlideNotFound(at, _presentation.Slides.Count + 1, "slide position");
        }

        _ = at == _presentation.Slides.Count + 1
            ? _presentation.Slides.AddClone(Slide)
            : _presentation.Slides.InsertClone(at - 1, Slide);
        return default;
    }

    public ValueTuple Apply(SetSlideHiddenOp operation)
    {
        foreach (ISlide slide in Slides)
        {
            slide.Hidden = operation.Hidden;
        }

        return default;
    }

    public ValueTuple Apply(SetLayoutOp operation)
    {
        foreach (ISlide slide in Slides)
        {
            slide.LayoutSlide = _target.Layout!;
        }

        FollowLayoutBackgrounds(Slides);
        return default;
    }

    /// <summary>
    /// Drops the slides' own backgrounds, which were chosen for their previous design, so they
    /// show the backgrounds of the layouts they now follow, and names the slides that had one.
    /// </summary>
    private void FollowLayoutBackgrounds(IReadOnlyList<ISlide> slides)
    {
        int[] reset = [.. slides.Where(static slide => slide.Background.Type == BackgroundType.OwnBackground)
            .Select(slide => _presentation.Slides.IndexOf(slide) + 1)];
        foreach (ISlide slide in slides)
        {
            slide.Background.Type = BackgroundType.NotDefined;
        }

        if (reset.Length > 0)
        {
            string numbers = string.Join(", ", reset);
            _warnings.Add(new Warning(SlidesDiagnostics.SlideBackgroundReset, $"The own background of slide(s) {numbers} was removed, so they show their layout's background.")
            {
                Hint = "To keep a background, set it again with set_background after this operation, then review the slides.",
                Location = (reset.Length == 1 ? "slide " : "slides ") + numbers,
            });
        }
    }

    public ValueTuple Apply(SetBackgroundOp operation)
    {
        IPPImage? image = null;
        if (operation.Path is not null)
        {
            EnsureFile(operation.Path);
            image = _presentation.Images.AddImage(OperationImages.Read(_inputs, operation.Path));
        }

        foreach (ISlide slide in Slides)
        {
            slide.Background.Type = BackgroundType.OwnBackground;
            if (operation.Color is not null)
            {
                slide.Background.FillFormat.FillType = FillType.Solid;
                slide.Background.FillFormat.SolidFillColor.Color = ParseColor(operation.Color);
            }
            else
            {
                slide.Background.FillFormat.FillType = FillType.Picture;
                slide.Background.FillFormat.PictureFillFormat.PictureFillMode = PictureFillMode.Stretch;
                slide.Background.FillFormat.PictureFillFormat.Picture.Image = image;
            }

        }

        return default;
    }

    public ValueTuple Apply(InsertSectionOp operation)
    {
        if (_presentation.Sections.Any(section => string.Equals(section.Name, operation.Name, StringComparison.Ordinal)))
        {
            throw new OperationInvalidException($"Section '{operation.Name}' already exists.");
        }

        _presentation.Sections.AddSection(operation.Name, Slide);
        return default;
    }

    public ValueTuple Apply(ImportSlidesOp operation)
    {
        using LoadedPresentation source = _loader.Open(operation.Path, OperationSecrets.Resolve(_secrets, operation.SourcePasswordEnv));
        var clones = new List<ISlide>();
        foreach (ISlide slide in source.Presentation.Slides)
        {
            ISlide clone = operation.Formatting == FormattingModes.KeepSource
                ? _presentation.Slides.AddClone(slide)
                : _presentation.Slides.AddClone(slide, _presentation.Masters[0], allowCloneMissingLayout: true);
            clones.Add(clone);
        }

        if (operation.Formatting != FormattingModes.KeepSource)
        {
            FollowLayoutBackgrounds(clones);
        }

        return default;
    }

    public ValueTuple Apply(SetTitleOp operation)
    {
        SlidesAuthoring.Title(Slide).TextFrame.Text = operation.Text;
        return default;
    }

    public ValueTuple Apply(SetBodyOp operation)
    {
        // Bullets and spacing come from the placeholder's levels, as in PowerPoint.
        SlidesAuthoring.WriteParagraphs(
            SlidesAuthoring.Body(Slide).TextFrame,
            operation.Paragraphs.Select(static input => new AuthoredParagraph(
                [new AuthoredRun(input.Text)],
                input.List is { } list ? list.Level - 1 : 0,
                input.List switch
                {
                    null => ParagraphList.None,
                    { Kind: "number" } => ParagraphList.Numbered,
                    _ => ParagraphList.Bullet,
                })));

        return default;
    }
}
