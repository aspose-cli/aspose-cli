using Aspose.Cli.Product.Slides.Commands;
using Aspose.Cli.Sdk.Extensibility.Output;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>
/// Pins the exact table and text output of the Slides renderers for representative results, so
/// moving their shared paragraphs into the SDK keeps every byte.
/// </summary>
public sealed class SlidesRendererGoldenTests
{
    [Fact]
    public void Info_WithEveryDetail_Plain()
    {
        string text = RenderedText.Of(surface => InspectCommand.Table(InfoWithDetails(), surface), TableFormat.Plain);

        RenderedText.Equal(
            """
            C:\decks\deck.pptx (pptx, 50,000 bytes)
            slides: 3   size: 960 x 540 pt   orientation: landscape
            masters: 1   layouts: 11   sections: 2   comments: 1   media: 1   macros: no
            slide  id   title    layout       shapes  flags
            1      256  Welcome  Title Slide  2       notes, 1 comment(s)
            2      257  Slide 2  -            0       hidden
            3      258  -        -            5       -

            sections:
            name   id    start slide
            Intro  {A1}  1
            Body   {B2}  2

            masters:
            name          slides
            Office Theme  3

            layouts:
            name         master        slides
            Title Slide  Office Theme  1
            Orphan       -             0

            media:
            index  type   content type  size
            0      image  image/png     2,048 bytes
            1      audio  -             1,048,576 bytes

            notes:
            slide  present  characters
            1      yes      9

            comments:
            slide  author  text
            1      Ann     Check the date

            fonts:
            Calibri, Arial

            properties:
            name     value
            Author   -
            Company  Aspose
            Title    Deck
            """,
            text);
    }

    [Fact]
    public void Info_WithEveryDetail_Markdown()
    {
        string text = RenderedText.Of(surface => InspectCommand.Table(InfoWithDetails(), surface), TableFormat.Markdown);

        RenderedText.Equal(
            """
            C:\decks\deck.pptx (pptx, 50,000 bytes)
            slides: 3   size: 960 x 540 pt   orientation: landscape
            masters: 1   layouts: 11   sections: 2   comments: 1   media: 1   macros: no
            | slide | id | title | layout | shapes | flags |
            | --- | --- | --- | --- | --- | --- |
            | 1 | 256 | Welcome | Title Slide | 2 | notes, 1 comment(s) |
            | 2 | 257 | Slide 2 | - | 0 | hidden |
            | 3 | 258 | - | - | 5 | - |

            ### sections

            | name | id | start slide |
            | --- | --- | --- |
            | Intro | {A1} | 1 |
            | Body | {B2} | 2 |

            ### masters

            | name | slides |
            | --- | --- |
            | Office Theme | 3 |

            ### layouts

            | name | master | slides |
            | --- | --- | --- |
            | Title Slide | Office Theme | 1 |
            | Orphan | - | 0 |

            ### media

            | index | type | content type | size |
            | --- | --- | --- | --- |
            | 0 | image | image/png | 2,048 bytes |
            | 1 | audio | - | 1,048,576 bytes |

            ### notes

            | slide | present | characters |
            | --- | --- | --- |
            | 1 | yes | 9 |

            ### comments

            | slide | author | text |
            | --- | --- | --- |
            | 1 | Ann | Check the date |

            ### fonts

            Calibri, Arial

            ### properties

            | name | value |
            | --- | --- |
            | Author | - |
            | Company | Aspose |
            | Title | Deck |
            """,
            text);
    }

    [Fact]
    public void Info_WithEmptyDetails_SaysMissingUnderEachHeading()
    {
        SlidesInspectResult result = Info() with
        {
            Sections = [], Masters = [], Layouts = [], Media = [], Notes = [], Comments = [], Fonts = [],
            Properties = new Dictionary<string, string?>(),
        };

        string text = RenderedText.Of(surface => InspectCommand.Table(result, surface), TableFormat.Plain);

        RenderedText.Equal(
            """
            C:\decks\deck.pptx (pptx, 50,000 bytes)
            slides: 3   size: 960 x 540 pt   orientation: landscape
            masters: 1   layouts: 11   sections: 2   comments: 1   media: 1   macros: no
            slide  id   title    layout       shapes  flags
            1      256  Welcome  Title Slide  2       notes, 1 comment(s)
            2      257  Slide 2  -            0       hidden
            3      258  -        -            5       -

            sections:
            -

            masters:
            -

            layouts:
            -

            media:
            -

            notes:
            -

            comments:
            -

            fonts:
            -

            properties:
            -
            """,
            text);
    }

    [Fact]
    public void Info_WithoutDetails_PrintsOnlyTheSummary()
    {
        string text = RenderedText.Of(surface => InspectCommand.Table(Info(), surface), TableFormat.Plain);

        RenderedText.Equal(
            """
            C:\decks\deck.pptx (pptx, 50,000 bytes)
            slides: 3   size: 960 x 540 pt   orientation: landscape
            masters: 1   layouts: 11   sections: 2   comments: 1   media: 1   macros: no
            slide  id   title    layout       shapes  flags
            1      256  Welcome  Title Slide  2       notes, 1 comment(s)
            2      257  Slide 2  -            0       hidden
            3      258  -        -            5       -
            """,
            text);
    }

    [Fact]
    public void Read_PrintsEachSlideWithItsTextAndNotes()
    {
        var rect = new SlidesRect { X = 0, Y = 0, Width = 100, Height = 20 };
        var result = new SlidesReadResult
        {
            Input = new InputInfo { Path = "deck.pptx", Format = "pptx", SizeBytes = 50_000, Fingerprint = new FileFingerprint { Sha256 = new string('0', 64) }, Encrypted = false },
            Window = new ResultWindow { Unit = "slide", Returned = 2, Total = 2, Truncated = false },
            Scope = "text",
            SlideCount = 2,
            Slides =
            [
                new SlidesSlideData
                {
                    Slide = 1, SlideId = 256, Title = "Welcome", Text = ["Welcome", "Agenda"],
                    Shapes = [new SlidesShapeData { ShapeId = 2, Type = "autoShape", Text = "Shape text", Rect = rect }],
                    Notes = "Say hello", ContentTruncated = false,
                },
                new SlidesSlideData
                {
                    Slide = 2, SlideId = 257,
                    Shapes = [new SlidesShapeData { ShapeId = 3, Type = "picture", Rect = rect }],
                    ContentTruncated = false,
                },
            ],
        };

        string text = RenderedText.Of(surface => ReadCommand.Table(result, surface), TableFormat.Plain);

        RenderedText.Equal(
            """
            deck.pptx: 2 slide(s) (text)

            --- slide 1 [256] Welcome ---
            Welcome
            Agenda
            Shape text
            notes: Say hello

            --- slide 2 [257]  ---
            """,
            text);
    }

    [Fact]
    public void Edit_PrintsOutcomesAndTouchedSlides()
    {
        var result = new SlidesEditResult
        {
            Input = new InputInfo { Path = "deck.pptx", Format = "pptx", SizeBytes = 50_000, Fingerprint = new FileFingerprint { Sha256 = new string('0', 64) }, Encrypted = false },
            Output = new OutputInfo { Path = "out.pptx", Format = "pptx", SizeBytes = 52_000, Fingerprint = new FileFingerprint { Sha256 = new string('0', 64) } },
            DryRun = false,
            Applied = [new BoundedOperationOutcome { Id = "op-0001", Op = "set_text", Status = OpStatuses.Ok, ItemsAffected = 1 }],
            SlidesTouched = [256, 258],
        };

        string text = RenderedText.Of(surface => EditCommand.Table(result, surface), TableFormat.Plain);

        RenderedText.Equal(
            """
            wrote out.pptx (pptx, 52,000 bytes, 1 of 1 op(s) applied)
              [op-0001] set_text: ok (1 item(s))
            slide ids touched: 256, 258
            """,
            text);
    }

    private static SlidesInspectResult Info() => new()
    {
        Input = new InputInfo { Path = @"C:\decks\deck.pptx", Format = "pptx", SizeBytes = 50_000, Fingerprint = new FileFingerprint { Sha256 = new string('0', 64) }, Encrypted = false },
        Presentation = new SlidesPresentationSummary
        {
            SlideCount = 3, WidthPoints = 960, HeightPoints = 540.004, Orientation = "landscape",
            MasterCount = 1, LayoutCount = 11, SectionCount = 2, CommentCount = 1, MediaCount = 1, HasMacros = false,
        },
        Slides =
        [
            new SlidesSlideInfo { Slide = 1, SlideId = 256, Title = "Welcome", Layout = "Title Slide", ShapeCount = 2, Hidden = false, HasNotes = true, CommentCount = 1 },
            new SlidesSlideInfo { Slide = 2, SlideId = 257, Name = "Slide 2", ShapeCount = 0, Hidden = true, HasNotes = false, CommentCount = 0 },
            new SlidesSlideInfo { Slide = 3, SlideId = 258, ShapeCount = 5, Hidden = false, HasNotes = false, CommentCount = 0 },
        ],
    };

    private static SlidesInspectResult InfoWithDetails() => Info() with
    {
        Sections =
        [
            new SlidesSectionInfo { Name = "Intro", SectionId = "{A1}", StartSlide = 1 },
            new SlidesSectionInfo { Name = "Body", SectionId = "{B2}", StartSlide = 2 },
        ],
        Masters = [new SlidesMasterInfo { Name = "Office Theme", SlideCount = 3 }],
        Layouts =
        [
            new SlidesLayoutInfo { Name = "Title Slide", Master = "Office Theme", SlideCount = 1 },
            new SlidesLayoutInfo { Name = "Orphan", Master = null, SlideCount = 0 },
        ],
        Media =
        [
            new SlidesMediaInfo { Index = 0, Type = "image", ContentType = "image/png", SizeBytes = 2_048 },
            new SlidesMediaInfo { Index = 1, Type = "audio", SizeBytes = 1_048_576 },
        ],
        Notes = [new SlidesNotesInfo { Slide = 1, Present = true, CharacterCount = 9 }],
        Comments = [new SlidesCommentInfo { Slide = 1, Author = "Ann", Text = "Check the date" }],
        Fonts = ["Calibri", "Arial"],
        Properties = new Dictionary<string, string?> { ["Title"] = "Deck", ["Author"] = null, ["Company"] = "Aspose" },
    };
}
