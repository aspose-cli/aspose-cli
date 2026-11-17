using System.Globalization;
using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.IO.Input;
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

        // A layout name names one of the master of the slide before the new one first, or after it at the start;
        // without a name the new slide takes that master's first layout.
        IMasterSlide? beside = _presentation.Slides.Count == 0 ? null : _presentation.Slides[Math.Max(at - 2, 0)].LayoutSlide.MasterSlide;
        ISlide slide = _presentation.Slides.AddEmptySlide(operation.Layout is { } name
            ? ResolveLayout(_presentation, name, beside)
            : beside?.LayoutSlides[0] ?? _presentation.LayoutSlides[0]);
        if (at <= _presentation.Slides.Count)
        {
            _presentation.Slides.Reorder(at - 1, slide);
        }

        if (operation.Layout is not null && beside is not null)
        {
            NameMasterChange(slide, beside, at > 1 ? "before" : "after");
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
            (IShape Shape, string? Role)[] placeholders = [.. slide.Shapes.Where(static shape => shape.Placeholder is not null).Select(static shape => (shape, PlaceholderRole(shape)))];
            IMasterSlide master = slide.LayoutSlide.MasterSlide;
            slide.LayoutSlide = ResolveLayout(_presentation, operation.Layout, master);
            NameRoleChanges(slide, placeholders);
            NameMasterChange(slide, master);
        }

        FollowLayoutBackgrounds(Slides);
        return default;
    }

    /// <summary>
    /// Names each placeholder of the slide whose role, as a read reports it, its new layout changed:
    /// the engine may give a placeholder the layout has none of the same type and index for a role of
    /// the layout the slide has no placeholder for, the title first, and its text then stands for it.
    /// </summary>
    private void NameRoleChanges(ISlide slide, (IShape Shape, string? Role)[] placeholders)
    {
        int number = _presentation.Slides.IndexOf(slide) + 1;
        // One warning per shape, so each location is one shape's address.
        foreach ((IShape shape, string? was) in placeholders.Where(static item => PlaceholderRole(item.Shape) != item.Role))
        {
            string? now = PlaceholderRole(shape);
            _warnings.Add(new Warning(SlidesDiagnostics.PlaceholderRoleChanged, string.Create(CultureInfo.InvariantCulture,
                $"Shape {shape.OfficeInteropShapeId} on slide {number} was its {was} placeholder and is its {now} placeholder under layout '{slide.LayoutSlide.Name}'{(now == SlidesPlaceholderRoles.Title ? ", so the slide's title is now the text of that shape" : "")}."))
            {
                Location = SlidesStateProjection.ShapeAddress(number, shape.OfficeInteropShapeId),
                Details = new JsonObject { ["shapeId"] = shape.OfficeInteropShapeId, ["from"] = was, ["to"] = now },
            });
        }
    }

    /// <summary>
    /// Names a slide that set_layout moved, or insert_slide added, to another master than its own or
    /// the one beside it: the layout name named none of that master's (<see cref="ResolveLayout"/>),
    /// and the slide shows the other master's theme and art. <paramref name="beside"/> says, for an
    /// inserted slide, whether that master is the one of the slide before it or after it.
    /// </summary>
    private void NameMasterChange(ISlide slide, IMasterSlide was, string? beside = null)
    {
        IMasterSlide now = slide.LayoutSlide.MasterSlide;
        if (now == was)
        {
            return;
        }

        int number = _presentation.Slides.IndexOf(slide) + 1;
        IMasterSlide[] masters = [.. _presentation.Masters];
        (int from, int to) = (Array.IndexOf(masters, was) + 1, Array.IndexOf(masters, now) + 1);
        _warnings.Add(new Warning(SlidesDiagnostics.SlideMasterChanged, string.Create(CultureInfo.InvariantCulture,
            $"Slide {number} took layout '{slide.LayoutSlide.Name}' of master {to} '{now.Name}', since {(beside is null ? "its " : "")}master {from} '{was.Name}'{(beside is null ? "" : $" of the slide {beside} it")} has no layout of that name, and now shows the theme and art of master {to}."))
        {
            Location = SlidesStateProjection.SlideAddress(number),
            Details = new JsonObject { ["fromMaster"] = from, ["toMaster"] = to },
        });
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

        // One warning per slide, so each location is one slide's address.
        foreach (int number in reset)
        {
            _warnings.Add(new Warning(SlidesDiagnostics.SlideBackgroundReset, string.Create(
                CultureInfo.InvariantCulture, $"The own background of slide {number} was removed, so it shows its layout's background."))
            {
                Location = SlidesStateProjection.SlideAddress(number),
            });
        }
    }

    public ValueTuple Apply(SetBackgroundOp operation)
    {
        IPPImage? image = null;
        if (operation.Path is not null)
        {
            EnsureFile(operation.Path);
            image = SlidesAuthoring.AddImage(_presentation, OperationImages.Read(_inputs, operation.Path));
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

        if (_presentation.Sections.FirstOrDefault(section => section.StartedFromSlide == Slide) is { } starting)
        {
            throw new OperationInvalidException(
                string.Create(CultureInfo.InvariantCulture, $"Section '{starting.Name}' already starts at slide {operation.At}, and a slide starts one section at most."));
        }

        _presentation.Sections.AddSection(operation.Name, Slide);
        return default;
    }

    public ValueTuple Apply(ImportSlidesOp operation)
    {
        using LoadedPresentation source = _loader.Open(operation.Path, PasswordSource.Option(OperationSecrets.Resolve(_secrets, operation.SourcePasswordEnv)));
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
        ((IAutoShape)Shape).TextFrame.Text = operation.Text;
        return default;
    }

    public ValueTuple Apply(SetBodyOp operation)
    {
        // Bullets and spacing come from the placeholder's levels, as in PowerPoint.
        SlidesAuthoring.WriteParagraphs(
            ((IAutoShape)Shape).TextFrame,
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
