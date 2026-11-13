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
                Hint = $"The layout has no {was} placeholder in that place. Check the slide's placeholders: set_title or set_text rewrites the shape's text, delete_shape removes it, and set_footer sets a footer, date or slide number again; {ShapeListing}.",
                Location = SlidesStateProjection.ShapeAddress(number, shape.OfficeInteropShapeId),
                Details = new JsonObject { ["shapeId"] = shape.OfficeInteropShapeId, ["from"] = was, ["to"] = now },
            });
        }
    }

    /// <summary>
    /// Names a slide that set_layout moved to another master: the layout name named none of its own
    /// master's (<see cref="ResolveLayout"/>), and the slide now shows the other master's theme and art.
    /// </summary>
    private void NameMasterChange(ISlide slide, IMasterSlide was)
    {
        IMasterSlide now = slide.LayoutSlide.MasterSlide;
        if (now == was)
        {
            return;
        }

        int number = _presentation.Slides.IndexOf(slide) + 1;
        IMasterSlide[] masters = [.. _presentation.Masters];
        (int from, int to) = (Array.IndexOf(masters, was) + 1, Array.IndexOf(masters, now) + 1);
        // The names set_layout, which looks a name up in the slide's own master first, takes back to its former master.
        string[] back = [.. was.LayoutSlides
            .Where(layout => !string.IsNullOrWhiteSpace(layout.Name) && ResolveLayout(_presentation, layout.Name, now).MasterSlide == was)
            .Select(static layout => $"'{layout.Name}'")];
        _warnings.Add(new Warning(SlidesDiagnostics.SlideMasterChanged, string.Create(CultureInfo.InvariantCulture,
            $"Slide {number} took layout '{slide.LayoutSlide.Name}' of master {to} '{now.Name}', since its master {from} '{was.Name}' has no layout of that name, and now shows the theme and art of master {to}."))
        {
            Hint = back.Length > 0
                ? string.Create(CultureInfo.InvariantCulture, $"To take the slide back to master {from}, set_layout it to one of {string.Join(", ", back)}, the layouts of master {from} whose names master {to} has none of; 'slides inspect --detail masters' lists the masters in order.")
                : string.Create(CultureInfo.InvariantCulture, $"No layout name takes the slide back to master {from}: set_layout looks a name up in the slide's own master first, then takes the first layout of the presentation by that name, and none of those is a layout of master {from}."),
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
                Hint = "To keep a background, set it again with set_background after this operation, then review the slides.",
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

        _presentation.Sections.AddSection(operation.Name, Slide);
        return default;
    }

    public ValueTuple Apply(ImportSlidesOp operation)
    {
        using LoadedPresentation source = _loader.Open(operation.Path, PasswordSource.OperationField(
            OperationSecrets.Resolve(_secrets, operation.SourcePasswordEnv), "sourcePasswordEnv"));
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
