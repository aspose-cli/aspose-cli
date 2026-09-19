using System.Security.Cryptography;
using System.Text.Json.Nodes;
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
                OutputPath = output,
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
            Assert.DoesNotContain(read.Slides[0].Shapes!, static shape => shape.Name == "Title 2");
        }
    }

    [Fact]
    public void DeleteMissingName_ReportsAvailableTargetsAndPreservesFiles()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation();
        string output = fixture.File("missing.pptx");
        byte[] original = File.ReadAllBytes(input);

        CliException error = Assert.Throws<CliException>(() => fixture.Engine.ApplyOps(
            input,
            new SlidesOpsBatch
            {
                Ops = [new DeleteShapeOp { Slide = 2, ShapeName = "Missing target" }],
            },
            new PresentationEditRequest { OutputPath = output }));

        Assert.Equal(ErrorCodes.ShapeNotFound, error.Code);
        Assert.Contains("slides query slides", error.Hint!, StringComparison.Ordinal);
        Assert.Contains("Title 2", error.Details!.ToJsonString(), StringComparison.Ordinal);
        Assert.Equal(original, File.ReadAllBytes(input));
        Assert.False(File.Exists(output));
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
        SlideShapeData shape = Assert.Single(all.Slides[2].Shapes!, static shape => shape.Name == "Title 3");
        SlideShapeData selected = Assert.Single(window.Slides[0].Shapes!, static shape => shape.Name == "Title 3");
        Assert.True(shape.ShapeId > 0);
        Assert.Equal(shape.ShapeId, selected.ShapeId);

        SlidesSearchResult search = fixture.Engine.Search(input, new PresentationSearchRequest
        {
            Pattern = "Slide", Scope = "shapes",
        });
        Assert.Contains(search.Hits, hit => hit.Slide == 3 && hit.ShapeId == shape.ShapeId);
        string output = fixture.File("addressed.pptx");
        SlidesEditResult edit = fixture.Engine.ApplyOps(input, new SlidesOpsBatch
        {
            Ops = [new SetTextOp { Slide = 3, Shape = shape.ShapeId, Text = "Saved" }],
        }, new PresentationEditRequest { OutputPath = output });
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
            Ops = [new DeleteShapeOp { Slide = 2, Shape = projectedGroup.ShapeId }],
        }, new PresentationEditRequest { OutputPath = output });
        Assert.Equal([$"slide/{read.Slides[0].SlideId}/shape/{groupId}"], removed.Applied[0].Targets);
        PresentationReadResult final = fixture.Engine.Read(output, new PresentationReadRequest
        {
            Slides = PageRange.Parse("2"), Scope = PresentationReadScopes.Shapes,
        });
        Assert.DoesNotContain(final.Slides[0].Shapes!, static shape => shape.Type == "group");
        Assert.Contains(final.Slides[0].Shapes!, static shape => shape.Name == "Title 2");
    }
    [Fact]
    public void NamedDeletionAndMissingName_AreActionableThroughRealCli()
    {
        using var workspace = new TempWorkspace();
        File.WriteAllText(workspace.File("outline.md"), "# Quarter one\n\nPerformance\n\n# Quarter two\n\nDecision");
        CliResult create = workspace.Run("slides", "create", "deck.pptx", "--from-markdown", "outline.md", "--size", "16x9", "--output", "json");
        Assert.True(create.ExitCode == 0, create.StdErr);
        byte[] original = SHA256.HashData(File.ReadAllBytes(workspace.File("deck.pptx")));
        File.WriteAllText(workspace.File("delete.json"), """{"ops":[{"op":"delete_shape","slide":2,"shapeName":"Title"}]}""");
        CliResult deleted = workspace.Run("slides", "edit", "deck.pptx", "--ops", "delete.json", "--out", "deleted.pptx", "--output", "json");
        Assert.True(deleted.ExitCode == 0, deleted.StdErr);
        Assert.True(File.Exists(workspace.File("deleted.pptx")));
        Assert.Equal(original, SHA256.HashData(File.ReadAllBytes(workspace.File("deck.pptx"))));

        File.WriteAllText(workspace.File("missing.json"), """{"ops":[{"op":"delete_shape","slide":2,"shapeName":"Missing target"}]}""");
        CliResult missing = workspace.Run("slides", "edit", "deck.pptx", "--ops", "missing.json", "--out", "missing.pptx", "--output", "json");
        Assert.Equal(4, missing.ExitCode);
        JsonNode error = JsonNode.Parse(missing.StdErr)!["error"]!;
        Assert.Equal("SHAPE_NOT_FOUND", error["code"]!.GetValue<string>());
        Assert.Contains("Title", error["details"]!.ToJsonString(), StringComparison.Ordinal);
        Assert.Contains("slides query slides", error["hint"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.False(File.Exists(workspace.File("missing.pptx")));
    }
}

