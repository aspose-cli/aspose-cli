using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aspose.Slides;
using static Aspose.Cli.Product.Slides.Engine.Mapping.SlidesStateProjection;
using static Aspose.Cli.Product.Slides.Engine.SlidesEvaluationMarks;
using static Aspose.Cli.Product.Slides.Engine.SlidesShapeText;

namespace Aspose.Cli.Product.Slides.Engine.Editing;

/// <summary>
/// The objects of a presentation one operation can change, as the SDK snapshots them around the
/// operation (<see cref="IOperationModel{TOp}.Snapshot"/>): the slide order (<c>slide/3</c>, by
/// slide id), a whole slide with its shapes and settings (<c>slide/3</c>), the top-level shapes
/// of a slide (<c>slide/3/shape/7</c>, by shape id), a slide's speaker notes
/// (<c>slide/3/notes</c>), the sections (<c>section/Intro</c>), the document properties
/// (<c>property/title</c>) and the slide size (<c>presentation/slide-size</c>). A slide is
/// addressed by its number in the presentation as the snapshot finds it, and names the slide as
/// its owner, so the batch numbers it where its output holds the slide
/// (<see cref="SlidesEditModel.Positions"/>). The state, as the engine stores it, and the
/// addresses come from <see cref="SlidesStateProjection"/>.
/// </summary>
/// <param name="presentation">The presentation being edited.</param>
/// <param name="target">The operation's targets, resolved just before it runs.</param>
/// <param name="evaluation">
/// Whether the engine runs in evaluation mode, whose save puts back the watermark boxes it adds, so
/// an operation that moves or scales them changes nothing a read of the output shows; the shapes
/// leave them out.
/// </param>
/// <param name="readCut">
/// Set only for the snapshot taken after the operation in evaluation mode: the text the
/// operation wrote reads cut short there, as its first characters and a notice, so its state
/// cannot show whether it changed. The object then counts as changed, and this is called to
/// disclose the cut read.
/// </param>
internal sealed class SlidesSnapshots(
    Presentation presentation, SlidesMutationHandlers.ResolvedSlidesOp target, bool evaluation, Action? readCut = null)
    : ISlidesOpHandler<OperationSnapshot>
{
    // What the state of written text that reads cut short ends with after the operation, which
    // no state before it has.
    private const string CutWritten = "|written-unread";

    public OperationSnapshot Apply(InsertSlideOp operation) => SlideOrder();

    public OperationSnapshot Apply(DeleteSlidesOp operation) => SlideOrder();

    /// <summary>The slide order, naming the moved slide, which counts once at its new number.</summary>
    public OperationSnapshot Apply(MoveSlideOp operation) =>
        new OperationSnapshot().AddSequence(SlideUnit, SlideIds(presentation), moved: [Slide.SlideId.ToString(CultureInfo.InvariantCulture)], owners: presentation.Slides);

    public OperationSnapshot Apply(CopySlideOp operation) => SlideOrder();

    public OperationSnapshot Apply(ImportSlidesOp operation) => SlideOrder();

    public OperationSnapshot Apply(SetSlideHiddenOp operation) => WholeSlides();

    public OperationSnapshot Apply(SetLayoutOp operation) => WholeSlides();

    public OperationSnapshot Apply(SetBackgroundOp operation) => WholeSlides();

    public OperationSnapshot Apply(SetFooterOp operation) => WholeSlides();

    public OperationSnapshot Apply(SetTransitionOp operation) => WholeSlides();

    /// <summary>The sections, each by the id of its first slide; a new section splits nothing else.</summary>
    public OperationSnapshot Apply(InsertSectionOp operation) => new OperationSnapshot().AddSequence("section", Sections(presentation));

    public OperationSnapshot Apply(SetTitleOp operation) => Shapes(() => Whole(SlidesAuthoring.Title(Slide)));

    public OperationSnapshot Apply(SetBodyOp operation) => Shapes(() => Whole(SlidesAuthoring.Body(Slide)));

    public OperationSnapshot Apply(SetTextOp operation) => Shapes(() => Whole(target.Shape!));

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
                snapshot.Add(NotesAddress(number), Notes(slide) ?? string.Empty, slide);
            }
        }

        return snapshot;
    }

    public OperationSnapshot Apply(SetNotesOp operation)
    {
        int number = Number(Slide);
        string? notes = Notes(Slide);
        return new OperationSnapshot().Add(NotesAddress(number), (notes ?? string.Empty) + (Unread(notes) ? CutWritten : string.Empty), Slide);
    }

    public OperationSnapshot Apply(SlidesInsertImageOp operation) => Shapes();

    public OperationSnapshot Apply(InsertShapeOp operation) => Shapes();

    public OperationSnapshot Apply(SlidesInsertTableOp operation) => Shapes();

    public OperationSnapshot Apply(SlidesSetTableCellOp operation) => Shapes(() => target.Shape is ITable table
        ? (table, table[operation.Column - 1, operation.Row - 1].TextFrame.Text)
        : Whole(target.Shape!));

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
        new OperationSnapshot().AddSequence(SlideUnit, SlideIds(presentation), owners: presentation.Slides);

    /// <summary>The slides a multi-slide operation selects, each by everything it shows.</summary>
    private OperationSnapshot WholeSlides()
    {
        var snapshot = new OperationSnapshot();
        foreach (ISlide slide in target.Slides!.Where(presentation.Slides.Contains))
        {
            int number = Number(slide);
            snapshot.Add(SlideAddress(number), SlideState(slide), slide);
        }

        return snapshot;
    }

    /// <summary>
    /// The top-level shapes of the operation's slide, each by its id.
    /// </summary>
    /// <param name="written">
    /// The shape the operation writes text into, with the text it wrote, asked for only after
    /// the operation, when the shape exists: one whose text reads cut short then counts as changed.
    /// </param>
    private OperationSnapshot Shapes(Func<(IShape Shape, string? Text)>? written = null)
    {
        var snapshot = new OperationSnapshot();
        IShape? cut = readCut is not null && written?.Invoke() is var (shape, text) && Unread(text) ? shape : null;
        AddShapes(snapshot, Slide, Number(Slide), cut);
        return snapshot;
    }

    private static (IShape Shape, string? Text) Whole(IShape shape) => (shape, ShapeText(shape));

    private void AddShapes(OperationSnapshot snapshot, ISlide slide, int number, IShape? cut = null)
    {
        foreach (IShape shape in slide.Shapes.Where(shape => !evaluation || !IsEvaluationWatermark(shape)))
        {
            snapshot.Add(ShapeAddress(number, shape.OfficeInteropShapeId), ShapeState(shape) + (ReferenceEquals(shape, cut) ? CutWritten : string.Empty), slide);
        }
    }

    // Whether text the operation wrote reads cut short after it; disclosed when it does.
    private bool Unread(string? text)
    {
        if (readCut is null || !CutByEvaluation(text))
        {
            return false;
        }

        readCut();
        return true;
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
