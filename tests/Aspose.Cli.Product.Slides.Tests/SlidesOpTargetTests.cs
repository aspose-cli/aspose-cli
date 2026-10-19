using Aspose.Cli.Product.Slides.Engine.Editing;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>
/// What each Slides operation reports it attempted when it fails, one row per sample, on a deck
/// of three slides whose shapes are named <c>Title 1</c> to <c>Title 3</c>: the shape or slides
/// it resolved, else the slide or shape it names as written, else the part of the presentation it
/// changes. Every operation of the vocabulary has a row, so a new operation fails here until it
/// is placed.
/// </summary>
public sealed class SlidesOpTargetTests
{
    private static readonly SlidesRectInput Rect = new() { X = 10, Y = 10, Width = 100, Height = 50 };

    /// <summary>
    /// One sample of every operation that resolves, then the variants whose address takes another
    /// branch: a slide, slide id, shape or range the deck lacks, and
    /// an operation that names no slide.
    /// </summary>
    private static readonly SlidesOp[] Samples =
    [
        new InsertSlideOp { At = 2 },
        new DeleteSlidesOp { Slides = "2" },
        new MoveSlideOp { Slide = 1, To = 2 },
        new CopySlideOp { Slide = 3 },
        new SetSlideHiddenOp { Slides = "1-2", Hidden = true },
        new SetLayoutOp { Slides = "1", Layout = "No such layout" },
        new SetBackgroundOp { Slides = "2" },
        new InsertSectionOp { Name = "Part", At = 2 },
        new ImportSlidesOp { Path = "other.pptx" },
        new SetTitleOp { Slide = 2, Text = "Title" },
        new SetBodyOp { Slide = 1, Paragraphs = [new SlidesParagraphInput { Text = "Body" }] },
        new SetTextOp { Slide = 1, ShapeName = "Title 1", Text = "Text" },
        new SlidesReplaceTextOp { Find = "Slide", Replace = "Page" },
        new SetNotesOp { Slide = 1, Text = "Note" },
        new SlidesInsertImageOp { Slide = 1, Path = "image.png" },
        new InsertShapeOp { Slide = 2, Geometry = SlidesShapeGeometries.Rectangle, Rect = Rect },
        new SlidesInsertTableOp { Slide = 2, Rect = Rect, RowCount = 2, ColumnCount = 2 },
        new SlidesSetTableCellOp { Slide = 2, ShapeName = "Title 2", Row = 1, Column = 1, Text = "Cell" },
        new InsertChartOp
        {
            Slide = 3,
            Kind = SlidesChartKinds.Column,
            Rect = Rect,
            Categories = ["Q1"],
            Series = [new SlidesChartSeriesInput { Name = "Sales", Values = [1] }],
        },
        new SetChartDataOp { Slide = 3, ShapeName = "Title 3" },
        new DeleteShapeOp { Slide = 1, ShapeName = "Title 1" },
        new FormatShapeOp { Slide = 1, ShapeName = "Title 1", Style = new SlidesShapeStyleInput() },
        new SetShapeBoundsOp { Slide = 1, ShapeName = "Title 1", X = 5 },
        new SetFooterOp { Slides = "3" },
        new SetTransitionOp { Slides = "2-3" },
        new SlidesSetPropertiesOp { Title = "Deck" },
        new SetSlideSizeOp { SlideSize = new SlidesSlideSizeInput { Preset = "widescreen" } },

        // Unresolved: what the operation names, as written, or the part it changes.
        new SetBackgroundOp(),
        new SetFooterOp(),
        new SetLayoutOp { Slides = "9", Layout = "No such layout" },
        new DeleteSlidesOp { Slides = "9" },
        new SetTransitionOp { Slides = "7-9" },
        new MoveSlideOp { Slide = 9, To = 1 },
        new CopySlideOp { SlideId = 99999 },
        new InsertSectionOp { Name = "Late", At = 9 },
        new SetTitleOp { Slide = 9, Text = "Title" },
        new SetTextOp { Slide = 1, ShapeId = 999, Text = "Text" },
        new SetTextOp { Slide = 9, ShapeId = 5, Text = "Text" },
        new FormatShapeOp { Slide = 2, ShapeName = "No such shape", Style = new SlidesShapeStyleInput() },
    ];

    /// <summary>Each row: the operation's name and the targets it reports when it fails.</summary>
    private static readonly string[] Expected =
    [
        "insert_slide | slide/all",
        "delete_slides | slide/2",
        "move_slide | slide/1",
        "copy_slide | slide/3",
        "set_slide_hidden | slide/1,slide/2",
        "set_layout | slide/all",
        "set_background | slide/2",
        "insert_section | section/Part",
        "import_slides | slide/all",
        "set_title | slide/2",
        "set_body | slide/1",
        "set_text | slide/1/shape/2",
        "replace_text | slide/all",
        "set_notes | slide/1",
        "insert_image | slide/1",
        "insert_shape | slide/2",
        "insert_table | slide/2",
        "set_table_cell | slide/2/shape/4",
        "insert_chart | slide/3",
        "set_chart_data | slide/3/shape/4",
        "delete_shape | slide/1/shape/2",
        "format_shape | slide/1/shape/2",
        "set_shape_bounds | slide/1/shape/2",
        "set_footer | slide/3",
        "set_transition | slide/2,slide/3",
        "set_properties | property/all",
        "set_slide_size | presentation/slide-size",
        "set_background | slide/1,slide/2,slide/3",
        "set_footer | slide/1,slide/2,slide/3",
        "set_layout | slide/all",
        "delete_slides | slide/all",
        "set_transition | slide/all",
        "move_slide | slide/9",
        "copy_slide | slide/all",
        "insert_section | section/Late",
        "set_title | slide/9",
        "set_text | slide/1/shape/999",
        "set_text | slide/9/shape/5",
        "format_shape | slide/2",
    ];

    [Fact]
    public void EveryOperationOfTheVocabularyHasARow()
    {
        Assert.Equal(
            SlidesOp.Catalog.Names.Order(StringComparer.Ordinal),
            Samples.Select(static op => SlidesOp.Catalog.NameOf(op)).Distinct().Order(StringComparer.Ordinal));
    }

    [Fact]
    public void AttemptedTargets_NameWhatEachOperationAddresses()
    {
        using var fixture = new SlidesEngineFixture();
        SlidesSession session = fixture.Session;
        using LoadedPresentation loaded = session.Loader.Open(fixture.CreatePresentation(), password: (Secret?)null);
        var model = new SlidesEditModel(session.Budgets.Inputs, session.Loader, loaded.Presentation, evaluation: false, secrets: null, readInput: static () => { });

        string[] actual = [.. Samples.Select(op => $"{SlidesOp.Catalog.NameOf(op)} | {string.Join(",", model.AttemptedTargets(op))}")];

        Assert.Equal(Expected, actual);
    }
}
