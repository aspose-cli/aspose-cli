using System.IO.Compression;
using System.Text.Json.Nodes;
using Aspose.Cli.Product.Slides.Contracts;
using Aspose.Cli.Product.Slides.Engine.Mapping;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Tables;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Tables;
using Aspose.Slides;
using Aspose.Slides.Charts;
using Aspose.Slides.Export;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>
/// The Slides table's round trip, one case per row: a settable row is set through an operation
/// document to a value other than the one it holds, the edit saves and reopens the file, and the
/// value its report reads back equals the one written; a read-only row is read. The deck is made by
/// the engine with one object of every kind; a row the engine does not keep is listed with its
/// known issue, and the case checks that the engine still does not keep it.
/// </summary>
public sealed class SlidesTableRoundTripTests
{
    public static TheoryData<string, string> Settable => TableRoundTrip.Cases(SlidesTable.Definition, settable: true);

    public static TheoryData<string, string> ReadOnly => TableRoundTrip.Cases(SlidesTable.Definition, settable: false);

    /// <summary>
    /// The rows whose sample is the deck's, not the value kind's: a layout id, an image file. The
    /// others take the value kind's sample.
    /// </summary>
    private static readonly Dictionary<(string Kind, string Row), Func<Presentation, (JsonNode Write, Func<JsonNode?, bool> Reads)>> Samples = new()
    {
        [("slide", "layout")] = static p => Equal(p.LayoutSlides.First(layout => layout.SlideId != p.Slides[0].LayoutSlide.SlideId).SlideId),
        [("slide", "background.picture")] = static _ => (Picture(), static now => now is JsonValue value && value.TryGetValue(out int _)),
    };

    /// <summary>
    /// The rows the same operation sets before the row under test, so it is written where the
    /// engine keeps it: a color in a solid fill, a slide's own background, a bullet of its kind.
    /// </summary>
    private static readonly Dictionary<(string Kind, string Row), JsonObject> Setup = new()
    {
        [("shape", "fill.color")] = new() { ["fill.fillType"] = "Solid" },
        [("shape", "line.color")] = new() { ["line.fillType"] = "Solid" },
        [("run", "fill.color")] = new() { ["fill.fillType"] = "Solid" },
        [("textFormat", "fill.color")] = new() { ["fill.fillType"] = "Solid" },
        [("cell", "fill.color")] = new() { ["fill.fillType"] = "Solid" },
        [("cell", "borderTop.color")] = new() { ["borderTop.fillType"] = "Solid" },
        [("cell", "borderBottom.color")] = new() { ["borderBottom.fillType"] = "Solid" },
        [("cell", "borderLeft.color")] = new() { ["borderLeft.fillType"] = "Solid" },
        [("cell", "borderRight.color")] = new() { ["borderRight.fillType"] = "Solid" },
        [("slide", "background.fillType")] = new() { ["background.type"] = "OwnBackground" },
        [("slide", "background.color")] = new() { ["background.type"] = "OwnBackground", ["background.fillType"] = "Solid" },
        [("slide", "background.pictureFillMode")] = new() { ["background.type"] = "OwnBackground", ["background.fillType"] = "Picture" },
        [("slide", "background.picture")] = new() { ["background.type"] = "OwnBackground", ["background.fillType"] = "Picture" },
        [("paragraph", "bullet.char")] = new() { ["bullet.type"] = "Symbol" },
        [("paragraph", "bullet.numberedStyle")] = new() { ["bullet.type"] = "Numbered" },
        [("axis", "minValue")] = new() { ["isAutomaticMinValue"] = false },
    };

    /// <summary>The rows whose object is not the first one that has the row: a value axis, a point of a chart type that uses the member.</summary>
    private static readonly Dictionary<(string Kind, string Row), string> Charts = new()
    {
        [("axis", "minValue")] = "vertical",
        [("point", "xValue")] = "ScatterWithMarkers",
        [("point", "yValue")] = "ScatterWithMarkers",
        [("point", "bubbleSize")] = "Bubble",
        [("point", "sizeValue")] = "Treemap",
    };
    /// <summary>The rows the engine does not keep as written, each with the known issue that says so.</summary>
    private static readonly Dictionary<(string Kind, string Row), string> EngineDifferences = new()
    {
        [("axis", "categoryAxisType")] = "SLIDES-CATEGORY-AXIS-DATE",
    };

    [Theory]
    [MemberData(nameof(Settable))]
    public void SettableRow_ReadsBackFromTheSavedFile(string kind, string row)
    {
        using var fixture = new SlidesEngineFixture();
        string input = Deck(fixture);
        string at;
        JsonNode? current;
        (JsonNode Write, Func<JsonNode?, bool> Reads) sample;
        using (var presentation = new Presentation(input))
        {
            at = Address(presentation, kind, row);
            current = TableRoundTrip.Read(SlidesTable.Definition, presentation, at)[row]?.DeepClone();
            sample = Samples.TryGetValue((kind, row), out var special)
                ? special(presentation)
                : Equal(TableRoundTrip.Sample(TableRoundTrip.ValueOf(SlidesTable.Definition, kind, row), current));
        }

        string ops = new JsonObject
        {
            ["ops"] = new JsonArray(new JsonObject { ["set"] = at, ["props"] = Props(kind, row, sample.Write) }),
        }.ToJsonString();
        JsonNode? now = null;
        Exception? failed = null;
        try
        {
            SlidesEditResult result = SlidesEdit.Run(fixture.Session, Request(input, fixture.File("out.pptx"), ops));
            now = Assert.Single(result.Ops).Now![row];
        }
        catch (Exception exception) when (EngineDifferences.ContainsKey((kind, row)))
        {
            failed = exception;
        }

        bool kept = failed is null && sample.Reads(now);
        if (EngineDifferences.TryGetValue((kind, row), out string? issue))
        {
            Assert.False(kept, $"{kind}.{row} now reads back as written; {issue} may be fixed.");
            return;
        }

        Assert.True(kept, $"{kind}.{row} at {at}: wrote {sample.Write.ToJsonString()}, read back {now?.ToJsonString() ?? "null"} (was {current?.ToJsonString() ?? "null"}).");
    }

    [Theory]
    [MemberData(nameof(ReadOnly))]
    public void ReadOnlyRow_IsRead(string kind, string row)
    {
        using var fixture = new SlidesEngineFixture();
        using var presentation = new Presentation(Deck(fixture));

        Assert.True(TableRoundTrip.Read(SlidesTable.Definition, presentation, Address(presentation, kind, row)).ContainsKey(row));
    }

    /// <summary>
    /// An edit changes only what it names: the inventory it counts before the batch, and the reads
    /// around it, leave the saved package as the engine alone saves it for the same change. A deck
    /// with a chart and no animation is given a title once by the engine alone (open, set
    /// DocumentProperties.Title, save) and once by slides edit; the packages hold the same parts
    /// with the same bytes, but for the time of the save in the core properties, which the engine
    /// writes as the moment of each save (<see cref="SavedPackage"/>).
    /// </summary>
    [Fact]
    public void Edit_SavesThePackageTheEngineAloneSavesForTheSameChange()
    {
        TestLicense.Require("Evaluation mode adds its own marks to every save.");
        using var fixture = new SlidesEngineFixture();
        string input = fixture.File("chart.pptx");
        using (var presentation = new Presentation())
        {
            presentation.Slides[0].Shapes.AddChart(ChartType.ClusteredColumn, 10, 10, 300, 200);
            presentation.Slides[0].Shapes.AddAutoShape(ShapeType.Rectangle, 10, 220, 200, 50).TextFrame.Text = "Hi";
            presentation.Save(input, SaveFormat.Pptx);
        }

        string engine = fixture.File("engine.pptx");
        using (var presentation = new Presentation(input))
        {
            presentation.DocumentProperties.Title = "Same";
            presentation.Save(engine, SaveFormat.Pptx);
        }

        SlidesEdit.Run(fixture.Session, Request(input, fixture.File("cli.pptx"), """{"ops":[{"set":"presentation","props":{"title":"Same"}}]}"""));

        string[] differ = SavedPackage.Differences(engine, fixture.File("cli.pptx"));
        Assert.True(differ.Length == 0, "Parts that differ: " + string.Join(", ", differ));
    }

    /// <summary>
    /// A placeholder index is the engine's unsigned number: a placeholder whose idx is 4294967295,
    /// common in files PowerPoint writes, is addressed by it and listed with it.
    /// </summary>
    [Fact]
    public void PlaceholderOfTheLargestIndex_IsAddressedAndListedByIt()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.File("placeholder-index.pptx");
        uint slide;
        using (var presentation = new Presentation())
        {
            slide = presentation.Slides.AddEmptySlide(presentation.LayoutSlides.First(static layout => layout.LayoutType == SlideLayoutType.TitleAndObject)).SlideId;
            presentation.Save(input, SaveFormat.Pptx);
        }

        using (ZipArchive package = ZipFile.Open(input, ZipArchiveMode.Update))
        {
            ZipArchiveEntry part = package.GetEntry("ppt/slides/slide2.xml")!;
            string xml;
            using (var reader = new StreamReader(part.Open()))
            {
                xml = reader.ReadToEnd();
            }

            Assert.Contains("idx=\"1\"", xml, StringComparison.Ordinal);
            part.Delete();
            using var writer = new StreamWriter(package.CreateEntry("ppt/slides/slide2.xml").Open());
            writer.Write(xml.Replace("idx=\"1\"", "idx=\"4294967295\"", StringComparison.Ordinal));
        }

        SlidesReadResult read = SlidesRead.Run(fixture.Session, new SlidesReadRequest { Input = input, At = $"slide/{slide}/placeholder/Object/4294967295" });
        CliException missing = Assert.Throws<CliException>(() => SlidesRead.Run(fixture.Session, new SlidesReadRequest { Input = input, At = $"slide/{slide}/placeholder/Object/7" }));

        Assert.Equal("4294967295", read.Objects[0].Props["placeholder.index"]!.ToJsonString());
        Assert.Contains("Object/4294967295", missing.Details!["available"]!.AsArray().Select(static key => key!.GetValue<string>()));
    }

    /// <summary>
    /// A notes slide whose NotesTextFrame is null, its body placeholder removed, has no text to set
    /// and no paragraphs to add to: a set of its text and an add of a paragraph are refused with
    /// the engine member that is null, and the CLI makes no frame. Nothing is written.
    /// </summary>
    [Theory]
    [InlineData("""{"ops":[{"set":"slide/256/notes","props":{"text":"Hi"}}]}""", "props.text")]
    [InlineData("""{"ops":[{"add":"paragraph","in":"slide/256/notes","args":{"index":0}}]}""", null)]
    public void NotesWithoutATextFrame_AreRefusedNamingTheNullMember(string ops, string? field)
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.File("bare-notes.pptx");
        using (var presentation = new Presentation())
        {
            INotesSlide notes = presentation.Slides[0].NotesSlideManager.AddNotesSlide();
            notes.Shapes.Remove(notes.Shapes.First(static shape => shape.Placeholder?.Type == PlaceholderType.Body));
            presentation.Save(input, SaveFormat.Pptx);
        }

        CliException refused = Assert.Throws<CliException>(() => SlidesEdit.Run(fixture.Session, Request(input, fixture.File("out.pptx"), ops)));

        Assert.Equal(ErrorCodes.OpsInvalid, refused.Code);
        Assert.Contains("INotesSlide.NotesTextFrame is null", refused.Message, StringComparison.Ordinal);
        Assert.Equal(field, refused.Details!["field"]?.GetValue<string>());
        Assert.False(File.Exists(fixture.File("out.pptx")));
    }

    /// <summary>
    /// A read gives each shape the rows its engine type has: a group shape has no line, as the
    /// engine's <c>IGroupShape.LineFormat</c> is null, so a deck with a group, SmartArt and a
    /// connector reads whole, and only the group lacks the <c>line</c> rows.
    /// </summary>
    [Fact]
    public void Read_GivesEachShapeTheRowsOfItsEngineType()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.File("shapes.pptx");
        using (var presentation = new Presentation())
        {
            IShapeCollection shapes = presentation.Slides[0].Shapes;
            shapes.AddAutoShape(ShapeType.Rectangle, 10, 10, 50, 50);
            shapes.AddGroupShape().Shapes.AddAutoShape(ShapeType.Ellipse, 70, 10, 50, 50);
            shapes.AddSmartArt(10, 80, 200, 150, Aspose.Slides.SmartArt.SmartArtLayoutType.BasicBlockList);
            shapes.AddConnector(ShapeType.StraightConnector1, 220, 80, 50, 50);
            presentation.Save(input, SaveFormat.Pptx);
        }

        SlidesReadResult read = SlidesRead.Run(fixture.Session, new SlidesReadRequest { Input = input, Kinds = ["shape"] });

        // Evaluation mode adds a watermark text box of its own; every shape is checked by its type.
        (string Type, bool Line)[] found = [.. read.Objects.Select(static shape => (shape.Props["type"]!.GetValue<string>(), shape.Props.ContainsKey("line.fillType")))];
        Assert.Superset(new HashSet<string>(["IAutoShape", "IGroupShape", "ISmartArt", "IConnector"]), found.Select(static shape => shape.Type).ToHashSet());
        Assert.All(found, static shape => Assert.Equal(shape.Type != "IGroupShape", shape.Line));
    }

    /// <summary>
    /// A deck with one object of every kind: two slides, a section, notes, a text box, a picture,
    /// a 2x2 table, a column chart with a title, and scatter, bubble and treemap charts, each with
    /// the engine's sample data.
    /// </summary>
    internal static string Deck(SlidesEngineFixture fixture)
    {
        string path = fixture.File("round-trip.pptx");
        using var presentation = new Presentation();
        ISlide slide = presentation.Slides[0];
        presentation.Slides.AddEmptySlide(presentation.LayoutSlides.First(layout => layout.LayoutType == SlideLayoutType.TitleAndObject));
        presentation.Sections.AddSection("Intro", slide);
        slide.NotesSlideManager.AddNotesSlide().NotesTextFrame.Text = "Notes";
        slide.Shapes.AddAutoShape(ShapeType.Rectangle, 10, 10, 200, 50).TextFrame.Text = "Hello";
        slide.Shapes.AddPictureFrame(ShapeType.Rectangle, 10, 80, 40, 40, presentation.Images.AddImage(SlidesEngineFixture.Png(8)));
        ITable table = slide.Shapes.AddTable(10, 150, [60, 60], [20, 20]);
        table[0, 0].TextFrame.Text = "Cell";
        slide.Shapes.AddChart(ChartType.ClusteredColumn, 300, 10, 300, 200).HasTitle = true;
        slide.Shapes.AddChart(ChartType.ScatterWithMarkers, 300, 220, 150, 100);
        slide.Shapes.AddChart(ChartType.Bubble, 460, 220, 150, 100);
        slide.Shapes.AddChart(ChartType.Treemap, 300, 330, 150, 100);
        presentation.Save(path, SaveFormat.Pptx);
        return path;
    }

    /// <summary>The setup rows, then the row under test.</summary>
    private static JsonObject Props(string kind, string row, JsonNode value)
    {
        JsonObject props = Setup.TryGetValue((kind, row), out JsonObject? setup) ? setup.DeepClone().AsObject() : [];
        props[row] = value.DeepClone();
        return props;
    }

    /// <summary>The address of an object of the kind that has the row; a workbook cell is the column chart's first value cell.</summary>
    private static string Address(Presentation presentation, string kind, string row)
    {
        string Chart(ChartType type) => $"slide/{presentation.Slides[0].SlideId}/shape/{presentation.Slides[0].Shapes.OfType<IChart>().First(chart => chart.Type == type).OfficeInteropShapeId}";
        return (kind, row) switch
        {
            ("data", _) => $"{Chart(ChartType.ClusteredColumn)}/data/0/1/1",
            ("axis", "minValue") => $"{Chart(ChartType.ClusteredColumn)}/axis/vertical",
            ("point", _) when Charts.TryGetValue((kind, row), out string? type) => $"{Chart(Enum.Parse<ChartType>(type))}/series/0/point/0",
            _ => TableRoundTrip.AddressOf(SlidesTable.Definition, presentation, kind, row)
                ?? throw new InvalidOperationException($"The round-trip deck has no {kind} with the row {row}."),
        };
    }

    private static SlidesEditRequest Request(string input, string output, string ops) => new()
    {
        Input = input,
        Batch = TableBatch.Parse(ops, SlidesTable.Definition, new Inputs()),
        Output = TestOutput.At(output),
    };

    private static (JsonNode Write, Func<JsonNode?, bool> Reads) Equal(JsonNode value) =>
        (value, now => JsonNode.DeepEquals(now, value));

    private static (JsonNode Write, Func<JsonNode?, bool> Reads) Equal(uint value) => Equal(JsonValue.Create(value));

    private static JsonNode Picture()
    {
        string path = Path.Combine(Path.GetTempPath(), $"aspose-cli-round-trip-{Environment.ProcessId}.png");
        File.WriteAllBytes(path, SlidesEngineFixture.Png(12));
        return path;
    }

    /// <summary>The files and secrets of a batch, as the command line reads them.</summary>
    private sealed class Inputs : ITableInputs
    {
        public string ResolvePath(string path) => Path.GetFullPath(path);

        public byte[] ReadImage(string path) => File.ReadAllBytes(path);

        public Secret? ReadSecret(string variable) => null;
    }
}
