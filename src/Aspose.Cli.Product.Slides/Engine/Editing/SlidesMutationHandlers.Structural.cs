using Aspose.Slides;
using static Aspose.Cli.Product.Slides.Engine.Editing.SlidesMutationSupport;

namespace Aspose.Cli.Product.Slides.Engine.Editing;

// Slide and section structure.
internal sealed partial class SlidesMutationHandlers
{
    public long Apply(AddSlideOp operation)
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

        _touched.Add(slide.SlideId);
        return 1;
    }

    public long Apply(DeleteSlidesOp operation)
    {
        if (Slides.Count == _presentation.Slides.Count)
        {
            throw new OperationInvalidException("A presentation must retain at least one slide.");
        }

        foreach (ISlide slide in Slides)
        {
            _presentation.Slides.Remove(slide);
        }

        return Slides.Count;
    }

    public long Apply(MoveSlideOp operation)
    {
        if (operation.To > _presentation.Slides.Count)
        {
            throw SlideNotFound(operation.To, _presentation.Slides.Count);
        }

        _presentation.Slides.Reorder(operation.To - 1, Slide);
        _touched.Add(Slide.SlideId);
        return 1;
    }

    public long Apply(DuplicateSlideOp operation)
    {
        int at = operation.At ?? _presentation.Slides.Count + 1;
        if (at > _presentation.Slides.Count + 1)
        {
            throw SlideNotFound(at, _presentation.Slides.Count + 1, "slide position");
        }

        ISlide clone = at == _presentation.Slides.Count + 1
            ? _presentation.Slides.AddClone(Slide)
            : _presentation.Slides.InsertClone(at - 1, Slide);
        _touched.Add(clone.SlideId);
        return 1;
    }

    public long Apply(SetSlideHiddenOp operation)
    {
        foreach (ISlide slide in Slides)
        {
            slide.Hidden = operation.Hidden;
            _touched.Add(slide.SlideId);
        }

        return Slides.Count;
    }

    public long Apply(ApplyLayoutOp operation)
    {
        foreach (ISlide slide in Slides)
        {
            slide.LayoutSlide = _target.Layout!;
            _touched.Add(slide.SlideId);
        }

        FollowLayoutBackgrounds(Slides);
        return Slides.Count;
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
            _warnings.Add(new Warning
            {
                Code = SlidesDiagnostics.SlideBackgroundReset,
                Message = $"The own background of slide(s) {numbers} was removed, so they show their layout's background.",
                Hint = "To keep a background, set it again with set_background after this operation, then review the slides.",
                Location = (reset.Length == 1 ? "slide " : "slides ") + numbers,
            });
        }
    }

    public long Apply(SetBackgroundOp operation)
    {
        IPPImage? image = null;
        if (operation.ImagePath is not null)
        {
            EnsureFile(operation.ImagePath);
            image = _presentation.Images.AddImage(_inputs.ReadAllBytes(operation.ImagePath));
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

            _touched.Add(slide.SlideId);
        }

        return Slides.Count;
    }

    public long Apply(AddSectionOp operation)
    {
        if (_presentation.Sections.Any(section => string.Equals(section.Name, operation.Name, StringComparison.Ordinal)))
        {
            throw new OperationInvalidException($"Section '{operation.Name}' already exists.");
        }

        _presentation.Sections.AddSection(operation.Name, Slide);
        return 1;
    }

    public long Apply(AppendPresentationOp operation)
    {
        using LoadedPresentation source = _loader.Open(operation.Path, password: null);
        var clones = new List<ISlide>();
        foreach (ISlide slide in source.Presentation.Slides)
        {
            ISlide clone = operation.MasterPolicy == SlidesMasterPolicies.KeepSource
                ? _presentation.Slides.AddClone(slide)
                : _presentation.Slides.AddClone(slide, _presentation.Masters[0], allowCloneMissingLayout: true);
            _touched.Add(clone.SlideId);
            clones.Add(clone);
        }

        if (operation.MasterPolicy != SlidesMasterPolicies.KeepSource)
        {
            FollowLayoutBackgrounds(clones);
        }

        return clones.Count;
    }

    public long Apply(SetTitleOp operation)
    {
        SlidesAuthoring.Title(Slide).TextFrame.Text = operation.Text;
        _touched.Add(Slide.SlideId);
        return 1;
    }

    public long Apply(SetBodyOp operation)
    {
        // Bullets and spacing come from the placeholder's levels, as in PowerPoint.
        SlidesAuthoring.WriteParagraphs(
            SlidesAuthoring.Body(Slide).TextFrame,
            operation.Paragraphs.Select(static input => new AuthoredParagraph(
                [new AuthoredRun(input.Text)],
                input.Level,
                input.Bullet ? ParagraphList.Inherit : ParagraphList.None)));

        _touched.Add(Slide.SlideId);
        return operation.Paragraphs.Count;
    }
}
