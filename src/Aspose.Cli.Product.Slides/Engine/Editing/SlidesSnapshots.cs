using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aspose.Slides;
using Aspose.Slides.Charts;
using static Aspose.Cli.Product.Slides.Engine.SlidesEngineSupport;

namespace Aspose.Cli.Product.Slides.Engine.Editing;

/// <summary>
/// The objects of a presentation one operation can change, as the SDK snapshots them around the
/// operation (<see cref="IOperationModel{TOp}.Snapshot"/>): the slide order (<c>slide/3</c>, by
/// slide id), a whole slide with its shapes and settings (<c>slide/3</c>), the top-level shapes
/// of a slide (<c>slide/3/shape/7</c>, by shape id), a slide's speaker notes
/// (<c>slide/3/notes</c>), the sections (<c>section/Intro</c>), the document properties
/// (<c>property/title</c>) and the slide size (<c>presentation/slide-size</c>). A slide is
/// addressed by its number in the presentation as the snapshot finds it.
/// </summary>
/// <param name="presentation">The presentation being edited.</param>
/// <param name="target">The operation's targets, resolved before the batch started.</param>
internal sealed class SlidesSnapshots(Presentation presentation, SlidesMutationHandlers.ResolvedSlidesOp target)
    : ISlidesOpHandler<OperationSnapshot>
{
    /// <summary>The unit of a slide's address, whose changed ones are the slides the batch touched.</summary>
    internal const string SlideUnit = "slide";

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
    public OperationSnapshot Apply(InsertSectionOp operation) =>
        new OperationSnapshot().AddSequence("section", presentation.Sections.Select(static section => (
            SectionAddress(section.Name),
            section.StartedFromSlide is { } first ? Invariant(first.SlideId) : "-")));

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
            snapshot.Add($"property/{name}", value ?? string.Empty);
        }

        return snapshot;
    }

    public OperationSnapshot Apply(SetSlideSizeOp operation) =>
        new OperationSnapshot().Add("presentation/slide-size", string.Create(
            CultureInfo.InvariantCulture,
            $"{presentation.SlideSize.Size.Width:R}x{presentation.SlideSize.Size.Height:R}"));

    private ISlide Slide => target.Slide!;

    /// <summary>The order of the slides, each by its id: inserting, deleting, copying or moving slides changes it.</summary>
    private OperationSnapshot SlideOrder() =>
        new OperationSnapshot().AddSequence(SlideUnit, presentation.Slides.Select(static slide => Invariant(slide.SlideId)));

    /// <summary>The slides a multi-slide operation selects, each by everything it shows.</summary>
    private OperationSnapshot WholeSlides()
    {
        var snapshot = new OperationSnapshot();
        foreach (ISlide slide in target.Slides!.Where(presentation.Slides.Contains))
        {
            int number = Number(slide);
            snapshot.Add(Address(number), SlideState(slide));
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

    internal static string Address(int number) => string.Create(CultureInfo.InvariantCulture, $"{SlideUnit}/{number}");

    internal static string ShapeAddress(int number, long shapeId) =>
        string.Create(CultureInfo.InvariantCulture, $"{SlideUnit}/{number}/shape/{shapeId}");

    /// <summary>
    /// A section's address by its name, with only the characters that would break the address
    /// escaped as %XX of their UTF-8 bytes: a slash, a percent sign, a control character, and
    /// white space at the start or end. Any other character, such as a CJK letter or an inner
    /// space, stays as it is.
    /// </summary>
    internal static string SectionAddress(string name)
    {
        var address = new StringBuilder("section/");
        for (int index = 0; index < name.Length; index++)
        {
            char character = name[index];
            bool edge = char.IsWhiteSpace(character)
                && (name[..index].All(char.IsWhiteSpace) || name[(index + 1)..].All(char.IsWhiteSpace));
            if (character is '/' or '%' || char.IsControl(character) || edge)
            {
                foreach (byte value in Encoding.UTF8.GetBytes(character.ToString()))
                {
                    address.Append(CultureInfo.InvariantCulture, $"%{value:X2}");
                }
            }
            else
            {
                address.Append(character);
            }
        }

        return address.ToString();
    }

    internal static string NotesAddress(int number) => string.Create(CultureInfo.InvariantCulture, $"{SlideUnit}/{number}/notes");

    /// <summary>
    /// A digest of everything a slide shows that an operation can change: its layout, visibility,
    /// own background, transition, notes and each of its shapes.
    /// </summary>
    internal static string SlideState(ISlide slide)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, $"{slide.LayoutSlide?.MasterSlide?.Name}|{slide.LayoutSlide?.Name ?? "-"}|{Identity(slide.LayoutSlide)}");
        Append(hash, slide.Hidden ? "hidden" : "shown");
        Append(hash, string.Create(CultureInfo.InvariantCulture, $"{slide.Background.Type}|{Fill(slide.Background.FillFormat)}"));
        Append(hash, string.Create(CultureInfo.InvariantCulture, $"{slide.SlideShowTransition.Type}|{slide.SlideShowTransition.Duration}"));
        Append(hash, Notes(slide) ?? "-");
        foreach (IShape shape in slide.Shapes)
        {
            Append(hash, string.Create(CultureInfo.InvariantCulture, $"{shape.OfficeInteropShapeId}|{ShapeState(shape)}"));
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    /// <summary>
    /// A digest of what an operation can change on a shape: its kind, name, alternative text,
    /// geometry, fill and line, its text with each paragraph's level and bullet and each run's
    /// fonts, size, weight and color, and a chart's data and title.
    /// </summary>
    internal static string ShapeState(IShape shape)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, string.Create(CultureInfo.InvariantCulture,
            $"{shape.GetType().Name}|{shape.Name}|{shape.AlternativeText}|{shape.X:R},{shape.Y:R},{shape.Width:R},{shape.Height:R}"));
        foreach (IFillFormat fill in SlidesMutationHandlers.Fills(shape))
        {
            Append(hash, "fill|" + Fill(fill));
        }

        foreach (ILineFillFormat line in SlidesMutationHandlers.Lines(shape))
        {
            Append(hash, string.Create(CultureInfo.InvariantCulture, $"line|{line.FillType}|{Color(line.FillType, line.SolidFillColor.Color)}"));
        }

        foreach (ITextFrame frame in TextFrames(shape))
        {
            foreach (IParagraph paragraph in frame.Paragraphs)
            {
                Append(hash, string.Create(CultureInfo.InvariantCulture,
                    $"paragraph|{paragraph.ParagraphFormat.Depth}|{paragraph.ParagraphFormat.Bullet.Type}"));
                foreach (IPortion portion in paragraph.Portions)
                {
                    Append(hash, "run|" + portion.Text);
                }
            }
        }

        foreach (IBasePortionFormat format in SlidesMutationHandlers.TextFormats(shape) ?? [])
        {
            Append(hash, string.Create(CultureInfo.InvariantCulture,
                $"format|{format.LatinFont?.FontName}|{format.EastAsianFont?.FontName}|{format.ComplexScriptFont?.FontName}|{format.FontHeight:R}|{format.FontBold}|{Fill(format.FillFormat)}"));
        }

        if (shape is IPictureFrame { PictureFormat.Picture.Image: { } image })
        {
            Append(hash, "image|" + Digest(image));
        }

        if (shape is IChart chart)
        {
            Append(hash, string.Create(CultureInfo.InvariantCulture, $"chart|{chart.Type}|{chart.HasTitle}|{chart.ChartTitle?.TextFrameForOverriding?.Text}"));
            foreach (IChartCategory category in chart.ChartData.Categories)
            {
                Append(hash, "category|" + category.Value);
            }

            foreach (IChartSeries series in chart.ChartData.Series)
            {
                Append(hash, "series|" + series.Name.AsCells.FirstOrDefault()?.Value);
                foreach (IChartDataPoint point in series.DataPoints)
                {
                    Append(hash, string.Create(CultureInfo.InvariantCulture, $"point|{point.XValue?.Data}|{point.YValue?.Data}|{point.Value?.Data}"));
                }
            }
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static string Fill(IFillFormat fill) =>
        fill.FillType == FillType.Picture
            ? "Picture|" + (fill.PictureFillFormat.Picture.Image is { } image ? Digest(image) : "-")
            : $"{fill.FillType}|{Color(fill.FillType, fill.SolidFillColor.Color)}";

    /// <summary>A digest of a picture's bytes: two pictures of the same size differ by their content.</summary>
    private static string Digest(IPPImage image) => Convert.ToHexStringLower(SHA256.HashData(image.BinaryData));

    /// <summary>
    /// Which layout a slide follows, among layouts of the same name under different masters: its
    /// position among the presentation's layouts.
    /// </summary>
    private static string Identity(ILayoutSlide? layout) =>
        layout?.Presentation is { } owner
            ? owner.LayoutSlides.TakeWhile(item => !ReferenceEquals(item, layout)).Count().ToString(CultureInfo.InvariantCulture)
            : "-";

    private static string Color(FillType type, System.Drawing.Color color) =>
        type == FillType.Solid ? color.ToArgb().ToString("X8", CultureInfo.InvariantCulture) : "-";

    private static string Invariant(uint value) => value.ToString(CultureInfo.InvariantCulture);

    private static void Append(IncrementalHash hash, string text)
    {
        hash.AppendData(Encoding.UTF8.GetBytes(text));
        hash.AppendData([0]);
    }
}
