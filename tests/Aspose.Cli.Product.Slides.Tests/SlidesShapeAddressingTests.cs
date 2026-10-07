using Aspose.Cli.Sdk.Errors;
using Aspose.Slides;
using Aspose.Slides.Export;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

public sealed class SlidesShapeAddressingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DeleteShapeByName_CapturesTheTargetBeforeDetaching(bool dryRun)
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation();
        string output = fixture.File("deleted.pptx");
        byte[] original = File.ReadAllBytes(input);

        SlidesEditResult result = fixture.Engine.ApplyOps(
            input,
            new SlidesOpsBatch
            {
                Ops = [new DeleteShapeOp { Slide = 2, ShapeName = "Title 2" }],
            },
            new PresentationEditRequest
            {
                Output = TestOutput.At(output),
                Options = new EditCommandOptions { DryRun = dryRun },
            });

        BoundedOperationOutcome operation = Assert.Single(result.Applied);
        Assert.Equal("ok", operation.Status);
        Assert.Matches("^slide/[1-9][0-9]*/shape/[1-9][0-9]*$", Assert.Single(operation.Targets));
        Assert.Equal(original, File.ReadAllBytes(input));
        Assert.Equal(!dryRun, File.Exists(output));
        if (!dryRun)
        {
            PresentationReadResult read = fixture.Engine.Read(output, new PresentationReadRequest
            {
                Slides = PageRange.Parse("2"),
                Scope = PresentationReadScopes.Shapes,
            });
            Assert.DoesNotContain(read.Slides[0].Shapes!, static shape => shape.ShapeName == "Title 2");
        }
    }

    [Fact]
    public void DeleteMissingName_ListsShapeNamesAndSuggestsTheCasingSlip()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation();
        string output = fixture.File("missing.pptx");
        byte[] original = File.ReadAllBytes(input);

        CliException error = Assert.Throws<CliException>(() => fixture.Engine.ApplyOps(
            input,
            new SlidesOpsBatch
            {
                Ops = [new DeleteShapeOp { Slide = 2, ShapeName = "title 2" }],
            },
            new PresentationEditRequest { Output = TestOutput.At(output) }));

        Assert.Equal(SlidesDiagnostics.ShapeNotFound, error.Code);
        Assert.Contains("'shapeId'", error.Hint!, StringComparison.Ordinal);
        Assert.Equal(0, (int)error.Details!["index"]!);
        Assert.Equal("title 2", (string?)error.Details["requested"]);
        Assert.Contains("Title 2", error.Details["available"]!.AsArray().Select(static name => (string?)name));
        Assert.Equal("Title 2", (string?)error.Details["suggestions"]![0]);
        Assert.Equal(original, File.ReadAllBytes(input));
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void SharedShapeName_IsRefusedInsteadOfPickingTheFirst()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.File("shared-names.pptx");
        using (var presentation = new Presentation())
        {
            presentation.Slides[0].Shapes.AddAutoShape(ShapeType.Rectangle, 10, 10, 100, 40).Name = "Box";
            presentation.Slides[0].Shapes.AddAutoShape(ShapeType.Rectangle, 10, 80, 100, 40).Name = "Box";
            presentation.Save(input, SaveFormat.Pptx);
        }

        CliException error = Assert.Throws<CliException>(() => fixture.Engine.ApplyOps(
            input,
            new SlidesOpsBatch { Ops = [new SetTextOp { Slide = 1, ShapeName = "Box", Text = "Which?" }] },
            new PresentationEditRequest { Output = TestOutput.At(fixture.File("shared-names-out.pptx")) }));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Contains("matches 2 shapes", error.Message, StringComparison.Ordinal);
        Assert.Contains("'shapeId'", error.Hint!, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingSlideTargets_ReportTheCountOrTheSlideIds()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation();

        CliException byNumber = Assert.Throws<CliException>(() => fixture.Engine.ApplyOps(
            input,
            new SlidesOpsBatch { Ops = [new SetNotesOp { Slide = 9, Text = "Late" }] },
            new PresentationEditRequest { Output = TestOutput.At(fixture.File("number.pptx")) }));
        CliException byId = Assert.Throws<CliException>(() => fixture.Engine.ApplyOps(
            input,
            new SlidesOpsBatch { Ops = [new SetNotesOp { SlideId = 99999, Text = "Late" }] },
            new PresentationEditRequest { Output = TestOutput.At(fixture.File("id.pptx")) }));

        Assert.Equal(SlidesDiagnostics.SlideNotFound, byNumber.Code);
        Assert.Equal("9", (string?)byNumber.Details!["requested"]);
        Assert.Equal(3, (int)byNumber.Details["availableCount"]!);
        Assert.Equal(SlidesDiagnostics.SlideNotFound, byId.Code);
        Assert.Equal("99999", (string?)byId.Details!["requested"]);
        Assert.Equal(3, byId.Details["available"]!.AsArray().Count);
    }

    [Fact]
    public void MissingLayout_FailsOnlyItsOperationInABestEffortBatch()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation();

        SlidesEditResult result = fixture.Engine.ApplyOps(
            input,
            new SlidesOpsBatch
            {
                Ops = [new AddSlideOp { Layout = "Title Onyl" }, new SetNotesOp { Slide = 1, Text = "Kept" }],
            },
            new PresentationEditRequest
            {
                Output = TestOutput.At(fixture.File("layout.pptx")),
                Options = new EditCommandOptions { BestEffort = true },
            });

        Assert.Equal(["failed", "ok"], result.Applied.Select(static operation => operation.Status));
        OpError error = result.Applied[0].Error!;
        Assert.Equal(SlidesDiagnostics.LayoutNotFound.Name, error.Code);
        Assert.Equal("Title Onyl", (string?)error.Details!["requested"]);
        Assert.Equal("Title Only", (string?)error.Details["suggestions"]![0]);
    }

    [Fact]
    public void ShapeIds_AreStableAcrossWindowsSearchAndFreshMutations()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation();
        PresentationReadResult all = fixture.Engine.Read(input, new PresentationReadRequest
        {
            Slides = PageRange.Parse("1-3"), Scope = PresentationReadScopes.Shapes,
        });
        PresentationReadResult window = fixture.Engine.Read(input, new PresentationReadRequest
        {
            Slides = PageRange.Parse("3"), Scope = PresentationReadScopes.Shapes,
        });
        SlideShapeData shape = Assert.Single(all.Slides[2].Shapes!, static shape => shape.ShapeName == "Title 3");
        SlideShapeData selected = Assert.Single(window.Slides[0].Shapes!, static shape => shape.ShapeName == "Title 3");
        Assert.True(shape.ShapeId > 0);
        Assert.Equal(shape.ShapeId, selected.ShapeId);

        SlidesSearchResult search = fixture.Engine.Search(input, new PresentationSearchRequest
        {
            Query = new SearchQuery(TextSearch.Create("Slide", regex: false, caseSensitive: false), 100, PresentationSearchScopes.Shapes),
        });
        Assert.Contains(search.Hits, hit => hit.Slide == 3 && hit.ShapeId == shape.ShapeId);
        string output = fixture.File("addressed.pptx");
        SlidesEditResult edit = fixture.Engine.ApplyOps(input, new SlidesOpsBatch
        {
            Ops = [new SetTextOp { Slide = 3, ShapeId = shape.ShapeId, Text = "Saved" }],
        }, new PresentationEditRequest { Output = TestOutput.At(output) });
        Assert.Equal([$"slide/{all.Slides[2].SlideId}/shape/{shape.ShapeId}"], edit.Applied[0].Targets);
        PresentationReadResult changed = fixture.Engine.Read(output, new PresentationReadRequest
        {
            Slides = PageRange.Parse("3"), Scope = PresentationReadScopes.Shapes,
        });
        Assert.Contains(changed.Slides[0].Shapes!, item => item.ShapeId == shape.ShapeId && item.Text == "Saved");
    }

    [Fact]
    public void OfficeInteropIds_PersistForGroupsAndChildrenAcrossSaveAndReopen()
    {
        using var fixture = new SlidesEngineFixture();
        string original = fixture.CreatePresentation();
        string grouped = fixture.File("grouped.pptx");
        string saved = fixture.File("grouped-saved.pptx");
        uint groupId;
        uint childId;
        using (var presentation = new Presentation(original))
        {
            IGroupShape group = presentation.Slides[1].Shapes.AddGroupShape();
            group.Name = "Grouped metrics";
            IAutoShape child = group.Shapes.AddAutoShape(ShapeType.Rectangle, 20, 120, 160, 40);
            child.Name = "Metric child";
            child.TextFrame.Text = "Metric";
            groupId = group.OfficeInteropShapeId;
            childId = child.OfficeInteropShapeId;
            Assert.True(groupId > 0);
            Assert.True(childId > 0);
            Assert.NotEqual(groupId, childId);
            presentation.Save(grouped, SaveFormat.Pptx);
        }
        using (var reopened = new Presentation(grouped))
        {
            IGroupShape group = Assert.IsAssignableFrom<IGroupShape>(
                Assert.Single(reopened.Slides[1].Shapes, static shape => shape.Name == "Grouped metrics"));
            Assert.Equal(groupId, group.OfficeInteropShapeId);
            Assert.Equal(childId, Assert.Single(group.Shapes).OfficeInteropShapeId);
            reopened.Slides[1].NotesSlideManager.AddNotesSlide().NotesTextFrame!.Text = "Updated notes";
            reopened.Save(saved, SaveFormat.Pptx);
        }
        using (var reopened = new Presentation(saved))
        {
            IGroupShape group = Assert.IsAssignableFrom<IGroupShape>(
                Assert.Single(reopened.Slides[1].Shapes, static shape => shape.Name == "Grouped metrics"));
            Assert.Equal(groupId, group.OfficeInteropShapeId);
            Assert.Equal(childId, Assert.Single(group.Shapes).OfficeInteropShapeId);
        }

        PresentationReadResult read = fixture.Engine.Read(saved, new PresentationReadRequest
        {
            Slides = PageRange.Parse("2"), Scope = PresentationReadScopes.Shapes,
        });
        SlideShapeData projectedGroup = Assert.Single(read.Slides[0].Shapes!, static shape => shape.Type == "group");
        Assert.Equal(groupId, projectedGroup.ShapeId);
        string output = fixture.File("group-removed.pptx");
        SlidesEditResult removed = fixture.Engine.ApplyOps(saved, new SlidesOpsBatch
        {
            Ops = [new DeleteShapeOp { Slide = 2, ShapeId = projectedGroup.ShapeId }],
        }, new PresentationEditRequest { Output = TestOutput.At(output) });
        Assert.Equal([$"slide/{read.Slides[0].SlideId}/shape/{groupId}"], removed.Applied[0].Targets);
        PresentationReadResult final = fixture.Engine.Read(output, new PresentationReadRequest
        {
            Slides = PageRange.Parse("2"), Scope = PresentationReadScopes.Shapes,
        });
        Assert.DoesNotContain(final.Slides[0].Shapes!, static shape => shape.Type == "group");
        Assert.Contains(final.Slides[0].Shapes!, static shape => shape.ShapeName == "Title 2");
    }

    [Fact]
    public void InsertedShapes_AreNamedByKindAndShapeId()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation(slides: 1);
        string image = fixture.File("pixel.png");
        File.WriteAllBytes(image, SlidesEngineFixture.Png(8));
        var rect = new SlidesRectInput { X = 40, Y = 120, Width = 200, Height = 100 };
        string output = fixture.File("named.pptx");

        fixture.Engine.ApplyOps(
            input,
            new SlidesOpsBatch
            {
                Ops =
                [
                    new InsertShapeOp { Slide = 1, Kind = "rectangle", Rect = rect },
                    new InsertShapeOp { Slide = 1, Kind = "rectangle", Rect = rect },
                    new SlidesInsertImageOp { Slide = 1, Path = image, Rect = rect },
                    new SlidesInsertTableOp { Slide = 1, Rect = rect, RowCount = 1, ColumnCount = 1 },
                    new InsertChartOp
                    {
                        Slide = 1,
                        Kind = "column",
                        Rect = rect,
                        Categories = ["A"],
                        Series = [new SlidesChartSeriesInput { Name = "S", Values = [1] }],
                    },
                ],
            },
            new PresentationEditRequest { Output = TestOutput.At(output) });

        PresentationReadResult read = fixture.Engine.Read(output, new PresentationReadRequest
        {
            Slides = PageRange.Parse("1"), Scope = PresentationReadScopes.Shapes,
        });
        Assert.Equal(
            ["rectangle", "rectangle", "picture", "table", "chart"],
            read.Slides[0].Shapes!
                .Where(static shape => shape.ShapeName != "Title 1" && !shape.EvaluationWatermark)
                .Select(static shape =>
                {
                    string name = Assert.IsType<string>(shape.ShapeName);
                    Assert.EndsWith($" {shape.ShapeId}", name, StringComparison.Ordinal);
                    return name[..name.IndexOf(' ', StringComparison.Ordinal)];
                }));
    }

    [Fact]
    public void SetShapeBounds_MovesAndResizesAShapeAndKeepsOmittedSides()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation(slides: 2);
        using (var presentation = new Presentation(input))
        {
            presentation.Slides[1].Shapes.AddTable(40, 120, [100, 100], [30, 30]).Name = "Figures";
            presentation.Save(input, SaveFormat.Pptx);
        }
        string output = fixture.File("bounds.pptx");

        SlidesEditResult result = fixture.Engine.ApplyOps(
            input,
            new SlidesOpsBatch
            {
                Ops =
                [
                    new SetShapeBoundsOp { Slide = 1, ShapeName = "Title 1", X = 100, Width = 300 },
                    new SetShapeBoundsOp { Slide = 2, ShapeName = "Figures", Y = 200, Width = 400, Height = 100 },
                ],
            },
            new PresentationEditRequest { Output = TestOutput.At(output) });

        Assert.All(result.Applied, static operation => Assert.Equal("ok", operation.Status));
        PresentationReadResult read = fixture.Engine.Read(output, new PresentationReadRequest
        {
            Slides = PageRange.Parse("1-2"), Scope = PresentationReadScopes.Shapes,
        });
        SlideRect title = Assert.Single(read.Slides[0].Shapes!, static shape => shape.ShapeName == "Title 1").Rect;
        Assert.Equal((100d, 30d, 300d, 70d), (title.X, title.Y, title.Width, title.Height));
        SlideRect table = Assert.Single(read.Slides[1].Shapes!, static shape => shape.ShapeName == "Figures").Rect;
        Assert.Equal((40d, 200d, 400d, 100d), (table.X, table.Y, table.Width, table.Height));
        using var reopened = new Presentation(output);
        ITable saved = reopened.Slides[1].Shapes.OfType<ITable>().Single();
        Assert.Equal(400d, saved.Columns.Sum(static column => column.Width));
        Assert.Equal(100d, saved.Rows.Sum(static row => row.Height));
    }
}
