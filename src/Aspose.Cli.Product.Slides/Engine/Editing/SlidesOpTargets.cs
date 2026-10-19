namespace Aspose.Cli.Product.Slides.Engine.Editing;

/// <summary>
/// What each operation names as written, which a failed operation reports when its slides or
/// shape did not resolve: one method per operation, so a new operation does not compile until it
/// is placed. An operation names its shape, its slide, or the part of the presentation it changes;
/// one that names no slide changes <c>slide/all</c>.
/// </summary>
internal sealed class SlidesOpTargets : ISlidesOpHandler<SlidesOpTarget>
{
    private static readonly SlidesOpTargets Instance = new();

    private SlidesOpTargets()
    {
    }

    /// <summary>What <paramref name="operation"/> names.</summary>
    internal static SlidesOpTarget Of(SlidesOp operation) => operation.Accept(Instance);

    // ----- slides ---------------------------------------------------------------------------

    public SlidesOpTarget Apply(InsertSlideOp operation) => AllSlides;

    public SlidesOpTarget Apply(DeleteSlidesOp operation) => AllSlides;

    public SlidesOpTarget Apply(MoveSlideOp operation) => Slide(operation);

    public SlidesOpTarget Apply(CopySlideOp operation) => Slide(operation);

    public SlidesOpTarget Apply(ImportSlidesOp operation) => AllSlides;

    public SlidesOpTarget Apply(SetSlideHiddenOp operation) => AllSlides;

    public SlidesOpTarget Apply(SetLayoutOp operation) => AllSlides;

    public SlidesOpTarget Apply(SetBackgroundOp operation) => AllSlides;

    public SlidesOpTarget Apply(SetFooterOp operation) => AllSlides;

    public SlidesOpTarget Apply(SetTransitionOp operation) => AllSlides;

    // A section is named by its own name, even when the slide it starts at resolved.
    public SlidesOpTarget Apply(InsertSectionOp operation) =>
        new([SlidesStateProjection.SectionAddress(operation.Name)]) { ByResolution = false };

    // ----- content of one slide -------------------------------------------------------------

    public SlidesOpTarget Apply(SetTitleOp operation) => Slide(operation);

    public SlidesOpTarget Apply(SetBodyOp operation) => Slide(operation);

    public SlidesOpTarget Apply(SetNotesOp operation) => Slide(operation);

    public SlidesOpTarget Apply(SlidesInsertImageOp operation) => Slide(operation);

    public SlidesOpTarget Apply(InsertShapeOp operation) => Slide(operation);

    public SlidesOpTarget Apply(SlidesInsertTableOp operation) => Slide(operation);

    public SlidesOpTarget Apply(InsertChartOp operation) => Slide(operation);

    // It searches every slide.
    public SlidesOpTarget Apply(SlidesReplaceTextOp operation) => AllSlides;

    // ----- one shape ------------------------------------------------------------------------

    public SlidesOpTarget Apply(SetTextOp operation) => Shape(operation);

    public SlidesOpTarget Apply(SlidesSetTableCellOp operation) => Shape(operation);

    public SlidesOpTarget Apply(SetChartDataOp operation) => Shape(operation);

    public SlidesOpTarget Apply(DeleteShapeOp operation) => Shape(operation);

    public SlidesOpTarget Apply(FormatShapeOp operation) => Shape(operation);

    public SlidesOpTarget Apply(SetShapeBoundsOp operation) => Shape(operation);

    // ----- the presentation -----------------------------------------------------------------

    public SlidesOpTarget Apply(SlidesSetPropertiesOp operation) => new([SlidesStateProjection.AllAddress("property")]);

    public SlidesOpTarget Apply(SetSlideSizeOp operation) => new([SlidesStateProjection.SlideSizeAddress]);

    private static readonly SlidesOpTarget AllSlides = new([SlidesStateProjection.AllAddress(SlidesStateProjection.SlideUnit)]);

    // The slide number as written; a slide named by id alone has no number to report.
    private static SlidesOpTarget Slide(SlideTargetOp operation) =>
        operation.Slide is int number ? new([SlidesStateProjection.SlideAddress(number)]) : AllSlides;

    // A shape named by id on a numbered slide, else the slide.
    private static SlidesOpTarget Shape(ShapeTargetOp operation) => operation is { Slide: int number, ShapeId: long shapeId }
        ? new([SlidesStateProjection.ShapeAddress(number, shapeId)])
        : Slide(operation);
}

/// <summary>What one operation names.</summary>
/// <param name="Written">
/// The targets it reports when the slides or shape it addresses did not resolve: the shape or
/// slide as written, or the part of the presentation it changes.
/// </param>
internal sealed record SlidesOpTarget(IReadOnlyList<string> Written)
{
    /// <summary>
    /// Whether the shape or slides it resolved, while they remain in the presentation, come
    /// before <see cref="Written"/>.
    /// </summary>
    public bool ByResolution { get; init; } = true;
}
