using Aspose.Words;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>
/// The SDK runner derives each Words outcome from snapshots of what the operation can change: an
/// operation that changed nothing reports 0 with <c>OP_NO_EFFECT</c>, targets are blocks and
/// sections where the output holds them, more than the cap shorten to one covering range, and a
/// conflict with an earlier operation is judged by what that operation did, not by what it asked.
/// </summary>
public sealed class WordsSnapshotOutcomeTests
{
    private const string Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";

    [Fact]
    public void DeleteWatermark_OnADocumentWithoutOne_ChangesNothingAndWarnsOpNoEffect()
    {
        using var fixture = new WordsFixture();

        WordsEditResult result = Edit(fixture, fixture.CreateReport(), new DeleteWatermarkOp());

        BoundedOperationOutcome outcome = Assert.Single(result.Applied);
        Assert.Equal((OpStatuses.Ok, 0L), (outcome.Status, outcome.ItemsAffected));
        Warning warning = Assert.Single(result.Warnings!, static warning => warning.Code.Name == "OP_NO_EFFECT");
        Assert.Equal("no-change", warning.Details!["reason"]!.GetValue<string>());
        Assert.Contains("no watermark", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReplaceText_WithTheTextItFinds_ChangesNothingButMatched()
    {
        using var fixture = new WordsFixture();

        WordsEditResult result = Edit(fixture, fixture.CreateReport(), new ReplaceTextOp { Find = "Revenue", Replace = "Revenue" });

        Assert.Equal(0L, Assert.Single(result.Applied).ItemsAffected);
        Warning warning = Assert.Single(result.Warnings!, static warning => warning.Code.Name == "OP_NO_EFFECT");
        Assert.Equal("no-change", warning.Details!["reason"]!.GetValue<string>());
    }

    [Fact]
    public void FormatText_OverManyBlocks_ListsOneRangeThatCoversThem()
    {
        using var fixture = new WordsFixture();
        string input = fixture.Temp.File("many.docx");
        var builder = new DocumentBuilder();
        for (int index = 1; index <= 150; index++)
        {
            builder.Writeln($"Paragraph {index}");
        }

        builder.Document.Save(input);

        WordsEditResult result = Edit(fixture, input, new FormatTextOp { Target = new WordsTarget { Blocks = "1-150" }, Bold = true });

        BoundedOperationOutcome outcome = Assert.Single(result.Applied);
        Assert.Equal(150L, outcome.ItemsAffected);
        Assert.Equal(["block/1-150"], outcome.Targets);
    }

    [Fact]
    public void SetHeader_OfOneSection_TargetsTheSection()
    {
        using var fixture = new WordsFixture();

        WordsEditResult result = Edit(fixture, fixture.CreateReport(), new SetHeaderOp { Section = 1, Paragraphs = [new WordsParagraphInput { Text = "Draft" }] });

        Assert.Equal(["section/1"], Assert.Single(result.Applied).Targets);
    }

    [Fact]
    public void BestEffort_ADeletionThatFailed_DoesNotRefuseALaterOperationOnWhatItWouldHaveDeleted()
    {
        using var fixture = new WordsFixture();

        // The document's only section cannot be deleted, so its blocks remain to be edited.
        WordsEditResult result = Edit(
            fixture,
            fixture.CreateReport(),
            bestEffort: true,
            new DeleteSectionOp { Section = 1 },
            new SetTextOp { Target = new WordsTarget { Block = 2 }, Text = "Still here" });

        Assert.Equal([OpStatuses.Failed, OpStatuses.Ok], result.Applied.Select(static outcome => outcome.Status));
        Assert.DoesNotContain("deleted by an earlier operation", result.Applied[0].Error!.Message, StringComparison.Ordinal);
        Assert.Contains("Still here", new Document(result.Output!.Path).GetText(), StringComparison.Ordinal);
    }

    [Fact]
    public void BestEffort_AnOperationWhoseAddressFailed_DoesNotRefuseALaterNumberedRevisionDecision()
    {
        using var fixture = new WordsFixture();
        string input = fixture.Temp.File("tracked.docx");
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.Writeln("Base text.");
        document.StartTrackRevisions("Reviewer");
        builder.Write("Added by the reviewer.");
        document.StopTrackRevisions();
        document.Save(input);

        WordsEditResult result = Edit(
            fixture,
            input,
            bestEffort: true,
            new SetTextOp { Target = new WordsTarget { Block = 99 }, Text = "Nowhere" },
            new AcceptRevisionsOp { Revisions = [1] });

        Assert.Equal([OpStatuses.Failed, OpStatuses.Ok], result.Applied.Select(static outcome => outcome.Status));
        Assert.Equal(0, new Document(result.Output!.Path).Revisions.Count);
    }

    [Fact]
    public void ProtectDocument_OnlyAddingAPassword_ChangesTheRestriction()
    {
        using var fixture = new WordsFixture();
        string input = Edit(fixture, fixture.CreateReport(), new ProtectDocumentOp { Protection = "read-only" }).Output!.Path;

        WordsEditResult result = Edit(fixture, input, new ProtectDocumentOp { Protection = "read-only", PasswordEnv = "PROTECT" });

        BoundedOperationOutcome outcome = Assert.Single(result.Applied);
        Assert.Equal((1L, "protection/document"), (outcome.ItemsAffected, Assert.Single(outcome.Targets)));
    }

    [Fact]
    public void SetWatermark_OnlyChangingFaded_ChangesTheWatermark()
    {
        using var fixture = new WordsFixture();
        string input = Edit(fixture, fixture.CreateReport(), new SetWatermarkOp { Text = "DRAFT" }).Output!.Path;

        WordsEditResult result = Edit(fixture, input, new SetWatermarkOp { Text = "DRAFT", Faded = false });

        Assert.Equal(["watermark/1"], Assert.Single(result.Applied).Targets);
    }

    [Fact]
    public void SetWatermark_ImageOnlyChangingFaded_ChangesTheWatermark()
    {
        using var fixture = new WordsFixture();
        string image = fixture.Temp.File("dot.png");
        File.WriteAllBytes(image, Convert.FromBase64String(Png));
        string input = Edit(fixture, fixture.CreateReport(), new SetWatermarkOp { Path = image }).Output!.Path;

        WordsEditResult result = Edit(fixture, input, new SetWatermarkOp { Path = image, Faded = false });

        Assert.Equal(["watermark/1"], Assert.Single(result.Applied).Targets);
    }

    [Fact]
    public void SetWatermark_AgainWithTheSameText_ChangesNothingAndWarnsOpNoEffect()
    {
        using var fixture = new WordsFixture();
        string input = Edit(fixture, fixture.CreateReport(), new SetWatermarkOp { Text = "DRAFT" }).Output!.Path;

        WordsEditResult result = Edit(fixture, input, new SetWatermarkOp { Text = "DRAFT" });

        Assert.Equal(0L, Assert.Single(result.Applied).ItemsAffected);
        Assert.Contains(result.Warnings!, static warning => warning.Code.Name == "OP_NO_EFFECT");
    }

    [Fact]
    public void SetProperties_OnlyACustomProperty_ChangesThatProperty()
    {
        using var fixture = new WordsFixture();

        WordsEditResult result = Edit(fixture, fixture.CreateReport(), new SetPropertiesOp { Custom = new Dictionary<string, string?> { ["Status"] = "Final" } });

        Assert.Equal(["property/Status"], Assert.Single(result.Applied).Targets);
    }

    [Fact]
    public void DefineStyle_OfAnExistingStyle_OnlyBold_ChangesTheStyle()
    {
        using var fixture = new WordsFixture();
        string input = Edit(fixture, fixture.CreateReport(), new DefineStyleOp { Name = "Quote", Font = "Arial" }).Output!.Path;

        WordsEditResult result = Edit(fixture, input, new DefineStyleOp { Name = "Quote", Bold = true });

        Assert.Equal(["style/Quote"], Assert.Single(result.Applied).Targets);
    }

    [Fact]
    public void DefineStyle_OfAnExistingTableStyle_OnlySpacing_ChangesTheStyle()
    {
        using var fixture = new WordsFixture();
        string input = fixture.Temp.File("table-style.docx");
        var document = new Document();
        document.Styles.Add(StyleType.Table, "Grid X");
        document.Save(input);

        WordsEditResult result = Edit(fixture, input, new DefineStyleOp { Name = "Grid X", SpaceBefore = 18, SpaceAfter = 9 });

        BoundedOperationOutcome outcome = Assert.Single(result.Applied);
        Assert.Equal(OpStatuses.Ok, outcome.Status);
        Assert.Equal(["style/Grid X"], outcome.Targets);
        Assert.True(outcome.ItemsAffected > 0);
        Assert.DoesNotContain(result.Warnings ?? [], static warning => warning.Code.Name == "OP_NO_EFFECT");
    }

    [Fact]
    public void SetPageSetup_OnlyTheColumnCount_ChangesTheSection()
    {
        using var fixture = new WordsFixture();

        WordsEditResult result = Edit(fixture, fixture.CreateReport(), new SetPageSetupOp { ColumnCount = 2 });

        Assert.Equal(["section/1"], Assert.Single(result.Applied).Targets);
    }

    [Fact]
    public void CustomPropertiesNamedLikeStandardOnesOrWithEdgeSpaces_SnapshotAndReadBackUnderTheirOwnKeys()
    {
        using var fixture = new WordsFixture();
        string input = fixture.Temp.File("custom.docx");
        var document = new Document();
        document.CustomDocumentProperties.Add("title", "Custom title");
        document.CustomDocumentProperties.Add(" lead/x ", "Spaced");
        document.Save(input);

        WordsEditResult result = Edit(fixture, input, new SetPropertiesOp { Author = "Tester" });
        IReadOnlyDictionary<string, string?> properties = WordsInspect.Run(fixture.Session, new WordsInspectRequest
        {
            Input = result.Output!.Path,
            Details = [WordsInspectDetails.Properties],
        }).Properties!;

        Assert.Equal(["property/author"], Assert.Single(result.Applied).Targets);
        Assert.Equal("Custom title", properties["custom:title"]);
        Assert.Equal("Spaced", properties[" lead/x "]);
        Assert.Equal("Tester", properties["author"]);
    }

    /// <summary>
    /// Accepting tracked changes removes a deleted paragraph and changes the one after it, which
    /// takes its block number: the removed paragraph and the changed one share that number, so the
    /// targets list it once while itemsAffected counts both, as the Words Skill says.
    /// </summary>
    [Fact]
    public void AcceptRevisions_OfARemovedParagraphAndTheNextOne_ListsASharedNumberOnceAndCountsBoth()
    {
        using var fixture = new WordsFixture();
        var source = new Document();
        var builder = new DocumentBuilder(source);
        builder.Writeln("Keep");
        builder.Writeln("Gone");
        builder.Write("Edited");
        source.StartTrackRevisions("Ann", DateTime.UnixEpoch);
        Paragraph[] paragraphs = [.. source.FirstSection.Body.Paragraphs.Cast<Paragraph>()];
        paragraphs[1].Remove();
        builder.MoveTo(paragraphs[2].Runs[0]);
        builder.Write("Newly ");
        source.StopTrackRevisions();
        string input = fixture.Temp.File("tracked.docx");
        source.Save(input, SaveFormat.Docx);

        BoundedOperationOutcome outcome = Assert.Single(Edit(fixture, input, new AcceptRevisionsOp()).Applied);

        Assert.Equal(["block/2"], outcome.Targets);
        Assert.True(outcome.ItemsAffected > outcome.Targets.Count, $"{outcome.ItemsAffected} items, targets {string.Join(", ", outcome.Targets)}");
    }

    [Theory]
    [InlineData("toc")]
    [InlineData("all")]
    public void RefreshFields_OfAnUpToDateTableOfContents_ChangesNothingAndWarnsOpNoEffect(string what)
    {
        using var fixture = new WordsFixture();
        string current = CurrentToc(fixture);

        WordsEditResult result = fixture.Disclosed(session => WordsEdit.Run(session, new WordsEditRequest
        {
            Input = current,
            Batch = new WordsOpsBatch { Ops = [new RefreshFieldsOp { What = what }] },
            Output = TestOutput.At(fixture.Temp.File($"refreshed-{Guid.NewGuid():N}.docx")),
            Verify = true,
        }));

        BoundedOperationOutcome outcome = Assert.Single(result.Applied);
        Assert.Equal((0L, 0), (outcome.ItemsAffected, outcome.Targets.Count));
        Assert.Contains(result.Warnings ?? [], static warning => warning.Code.Name == "OP_NO_EFFECT");
        Assert.False(result.Verification!.SemanticChangesDetected);
    }

    [Fact]
    public void RefreshFields_AfterAHeadingChanged_CountsOnlyTheEntryThatChanged()
    {
        using var fixture = new WordsFixture();
        string current = CurrentToc(fixture);
        var document = new Document(current);
        Paragraph heading = document.GetChildNodes(NodeType.Paragraph, true).Cast<Paragraph>()
            .Single(static paragraph => paragraph.ParagraphFormat.StyleIdentifier == StyleIdentifier.Heading1 && paragraph.GetText().Trim() == "Second");
        heading.Runs[0].Text = "Second part";
        string stale = fixture.Temp.File("stale-toc.docx");
        document.Save(stale, SaveFormat.Docx);

        WordsEditResult result = Edit(fixture, stale, new RefreshFieldsOp { What = "toc" });

        BoundedOperationOutcome outcome = Assert.Single(result.Applied);
        Assert.Equal(1L, outcome.ItemsAffected);
        string target = Assert.Single(outcome.Targets);
        Assert.StartsWith("block/", target, StringComparison.Ordinal);
        int block = int.Parse(target["block/".Length..], System.Globalization.CultureInfo.InvariantCulture);
        WordsReadResult read = WordsRead.Run(fixture.Session, new WordsReadRequest { Input = result.Output!.Path, Content = "text" });
        Assert.StartsWith("Second part", read.Blocks.Single(item => item.Block == block).Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Two headings and a table of contents that lists them as they are. Each update of a table of
    /// contents in a process names the hidden bookmarks it adds to the headings anew, so the table
    /// is updated once, by insert_toc.
    /// </summary>
    private static string CurrentToc(WordsFixture fixture)
    {
        var source = new Document();
        var builder = new DocumentBuilder(source);
        builder.ParagraphFormat.StyleIdentifier = StyleIdentifier.Heading1;
        builder.Writeln("First");
        builder.ParagraphFormat.StyleIdentifier = StyleIdentifier.Normal;
        builder.Writeln("Text");
        builder.ParagraphFormat.StyleIdentifier = StyleIdentifier.Heading1;
        builder.Writeln("Second");
        builder.ParagraphFormat.StyleIdentifier = StyleIdentifier.Normal;
        builder.Write("More");
        string input = fixture.Temp.File($"headings-{Guid.NewGuid():N}.docx");
        source.Save(input, SaveFormat.Docx);
        WordsEditResult inserted = Edit(fixture, input, new InsertTocOp { At = new WordsTarget { Block = 1 }, Position = "before" });
        return inserted.Output!.Path;
    }

    private static WordsEditResult Edit(WordsFixture fixture, string input, params WordsOp[] ops) =>
        Edit(fixture, input, bestEffort: false, ops);

    private static WordsEditResult Edit(WordsFixture fixture, string input, bool bestEffort, params WordsOp[] ops) =>
        fixture.Disclosed(session => WordsEdit.Run(session, new WordsEditRequest
        {
            Input = input,
            Batch = new WordsOpsBatch { Ops = ops },
            Output = TestOutput.At(fixture.Temp.File($"edited-{Guid.NewGuid():N}.docx")),
            Options = new EditCommandOptions { BestEffort = bestEffort },
            OpSecrets = new Dictionary<string, Secret> { ["PROTECT"] = new("protect-secret") },
        }));
}
