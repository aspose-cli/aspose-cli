using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aspose.Slides;
using static Aspose.Cli.Product.Slides.Engine.Mapping.SlidesStateProjection;
using static Aspose.Cli.Product.Slides.Engine.SlidesEngineSupport;

namespace Aspose.Cli.Product.Slides.Engine.Editing;

/// <summary>
/// The objects of a presentation one operation can change, as the SDK snapshots them around the
/// operation (<see cref="IOperationModel{TOp}.Snapshot"/>): the slide order (<c>slide/3</c>, by
/// slide id), a whole slide with its shapes and settings (<c>slide/3</c>), the top-level shapes
/// of a slide (<c>slide/3/shape/7</c>, by shape id), a slide's speaker notes
/// (<c>slide/3/notes</c>), the sections (<c>section/Intro</c>), the document properties
/// (<c>property/title</c>) and the slide size (<c>presentation/slide-size</c>). A slide is
/// addressed by its number in the presentation as the snapshot finds it. The state, as the engine
/// stores it, and the addresses come from <see cref="SlidesStateProjection"/>.
/// </summary>
/// <param name="presentation">The presentation being edited.</param>
/// <param name="target">The operation's targets, resolved just before it runs.</param>
internal sealed class SlidesSnapshots(Presentation presentation, SlidesMutationHandlers.ResolvedSlidesOp target)
    : ISlidesOpHandler<OperationSnapshot>
{
    public OperationSnapshot Apply(InsertSlideOp operation) => SlideOrder();

    public OperationSnapshot Apply(DeleteSlidesOp operation) => SlideOrder();

    public OperationSnapshot Apply(MoveSlideOp operation) => SlideOrder();

    public OperationSnapshot Apply(CopySlideOp operation) => SlideOrder();

    public OperationSnapshot Apply(ImportSlidesOp operation) => SlideOrder();

    public OperationSnapshot Apply(SetSlideHiddenOp operation) => WholeSlides();

    public OperationSnapshot Apply(SetLayoutOp operation) => WholeSlides();

    public OperationSnapshot Apply(SetBackgroundOp operation) => WholeSlides();

    public OperationSnapshot Apply(SetFooterOp operation) => WholeSlides();

    public OperationSnapshot Apply(SetTransitionOp operation) => WholeSlides();

    /// <summary>The sections, each by the id of its first slide; a new section splits nothing else.</summary>
    public OperationSnapshot Apply(InsertSectionOp operation) => new OperationSnapshot().AddSequence("section", Sections(presentation));

    public OperationSnapshot Apply(SetTitleOp operation) => Shapes();

    public OperationSnapshot Apply(SetBodyOp operation) => Shapes();

    public OperationSnapshot Apply(SetTextOp operation) => Shapes();

    /// <summary>Every shape and every slide's notes the operation's scope reaches.</summary>
    public OperationSnapshot Apply(SlidesReplaceTextOp operation)
    {
        var snapshot = new OperationSnapshot();
        bool shapes = operation.Scope is SlidesSearchScopes.Shapes or SlidesSearchScopes.All;
        bool notes = operation.Scope is SlidesSearchScopes.Notes or SlidesSearchScopes.All;
        for (int number = 1; number <= presentation.Slides.Count; number++)
        {
            ISlide slide = presentation.Slides[number - 1];
            if (shapes)
            {
                AddShapes(snapshot, slide, number);
            }

            if (notes)
            {
                snapshot.Add(NotesAddress(number), Notes(slide) ?? string.Empty);
            }
        }

        return snapshot;
    }

    public OperationSnapshot Apply(SetNotesOp operation)
    {
        int number = Number(Slide);
        return new OperationSnapshot().Add(NotesAddress(number), Notes(Slide) ?? string.Empty);
    }

    public OperationSnapshot Apply(SlidesInsertImageOp operation) => Shapes();

    public OperationSnapshot Apply(InsertShapeOp operation) => Shapes();

    public OperationSnapshot Apply(SlidesInsertTableOp operation) => Shapes();

    public OperationSnapshot Apply(SlidesSetTableCellOp operation) => Shapes();

    public OperationSnapshot Apply(InsertChartOp operation) => Shapes();

    public OperationSnapshot Apply(SetChartDataOp operation) => Shapes();

    public OperationSnapshot Apply(DeleteShapeOp operation) => Shapes();

    public OperationSnapshot Apply(FormatShapeOp operation) => Shapes();

    public OperationSnapshot Apply(SetShapeBoundsOp operation) => Shapes();

    /// <summary>The document properties <c>slides inspect</c> reports, each by its value.</summary>
    public OperationSnapshot Apply(SlidesSetPropertiesOp operation)
    {
        var snapshot = new OperationSnapshot();
        foreach ((string name, string? value) in Properties(presentation.DocumentProperties))
        {
            snapshot.Add(PropertyAddress(name), value ?? string.Empty);
        }

        return snapshot;
    }

    /// <summary>The slide size and every slide's top-level shapes, which scaling the content moves and resizes.</summary>
    public OperationSnapshot Apply(SetSlideSizeOp operation)
    {
        var snapshot = new OperationSnapshot().Add(SlideSizeAddress, SlideSize(presentation) is var size
            ? string.Create(CultureInfo.InvariantCulture, $"{size.Width:R}x{size.Height:R}")
            : string.Empty);
        for (int number = 1; number <= presentation.Slides.Count; number++)
        {
            AddShapes(snapshot, presentation.Slides[number - 1], number);
        }

        return snapshot;
    }

    private ISlide Slide => target.Slide!;

    /// <summary>The order of the slides, each by its id: inserting, deleting, copying or moving slides changes it.</summary>
    private OperationSnapshot SlideOrder() =>
        new OperationSnapshot().AddSequence(SlideUnit, SlideIds(presentation));

    /// <summary>The slides a multi-slide operation selects, each by everything it shows.</summary>
    private OperationSnapshot WholeSlides()
    {
        var snapshot = new OperationSnapshot();
        foreach (ISlide slide in target.Slides!.Where(presentation.Slides.Contains))
        {
            int number = Number(slide);
            snapshot.Add(SlideAddress(number), SlideState(slide));
        }

        return snapshot;
    }

    /// <summary>The top-level shapes of the operation's slide, each by its id.</summary>
    private OperationSnapshot Shapes()
    {
        var snapshot = new OperationSnapshot();
        AddShapes(snapshot, Slide, Number(Slide));
        return snapshot;
    }

    private static void AddShapes(OperationSnapshot snapshot, ISlide slide, int number)
    {
        foreach (IShape shape in slide.Shapes)
        {
            snapshot.Add(ShapeAddress(number, shape.OfficeInteropShapeId), ShapeState(shape));
        }
    }

    private int Number(ISlide slide) => presentation.Slides.IndexOf(slide) + 1;

    /// <summary>
    /// A digest of everything a slide shows that an operation can change: its layout, visibility,
    /// own background, transition, notes and each of its shapes.
    /// </summary>
    internal static string SlideState(ISlide slide) => Digest([
        $"{Layout(slide) ?? "-"}|{LayoutPosition(slide)}",
        slide.Hidden ? "hidden" : "shown",
        StoredBackground(slide),
        TransitionState(slide),
        Notes(slide) ?? "-",
        .. slide.Shapes.Select(static shape => $"{shape.OfficeInteropShapeId}|{ShapeState(shape)}"),
    ]);

    /// <summary>A digest of what an operation can change on a shape, as the engine stores it.</summary>
    internal static string ShapeState(IShape shape) => Digest(StoredShape(shape));

    private static string Digest(IEnumerable<string> facts)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (string fact in facts)
        {
            hash.AppendData(Encoding.UTF8.GetBytes(fact));
            hash.AppendData([0]);
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
}
