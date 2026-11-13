using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.TestKit;
using Aspose.Pdf;
using Aspose.Pdf.Annotations;
using Aspose.Pdf.Text;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>
/// Page moves and merges that break bookmarks, links or named destinations publish their
/// output with a warning that counts what broke; nothing is silently lost. Navigation that led
/// to a page the edit deleted is what the caller asked for; navigation whose page is still in
/// the output leaves it incomplete, so a verified edit exits 8 on it.
/// </summary>
public sealed class PdfNavigationTests
{
    /// <summary>
    /// A table of contents' link and a bookmark lead to page 2, which the edit deletes as asked:
    /// the warning counts them and does not leave the output incomplete, so the verified edit
    /// exits 0, and the link to page 3, which the engine reads by the number it had before the
    /// deletion, is not counted. The hint names the operation that removes the bookmark, and
    /// ending the batch with it does.
    /// </summary>
    [Fact]
    public void DeletePages_ThatNavigationLeadsTo_CountsItWithoutFailingTheVerification()
    {
        using var fixture = new PdfEngineFixture();
        using var workspace = new TempWorkspace();
        using (var document = new Document())
        {
            foreach (string chapter in new[] { "Contents", "Results", "Appendix" })
            {
                document.Pages.Add().Paragraphs.Add(new TextFragment(chapter));
            }

            Page contents = document.Pages[1];
            contents.Annotations.Add(new LinkAnnotation(contents, new Rectangle(68, 700, 300, 714)) { Destination = new XYZExplicitDestination(document.Pages[2], 68, 750, 0) });
            contents.Annotations.Add(new LinkAnnotation(contents, new Rectangle(68, 680, 300, 694)) { Destination = new XYZExplicitDestination(document.Pages[3], 68, 750, 0) });
            document.Outlines.Add(new OutlineItemCollection(document.Outlines) { Title = "Results", Destination = new FitExplicitDestination(document.Pages[2]) });
            document.Save(workspace.File("chapters.pdf"));
        }

        CliResult deleted = workspace.Run("pdf", "edit", "chapters.pdf", "--out", "deleted.pdf", "--verify", "--output-format", "json", "--ops",
            """[{"op":"delete_pages","pages":"2"}]""");

        Assert.True(deleted.ExitCode == 0, deleted.StdOut + deleted.StdErr);
        JsonNode result = deleted.Json();
        Assert.True(result["verification"]!["ok"]!.GetValue<bool>());
        JsonNode warning = Assert.Single(result["warnings"]!.AsArray(), static item => item!["code"]!.GetValue<string>() == "NAVIGATION_DEGRADED")!;
        Assert.Null(warning["affectsCompleteness"]);
        Assert.Equal("1 bookmark(s), 1 link(s) and 0 named destination(s) lead to pages the edit deleted.", warning["message"]!.GetValue<string>());
        string hint = warning["hint"]!.GetValue<string>();
        // The hint names the bookmark by its address in the output, as pdf inspect reports it.
        Assert.Contains("""{"op":"delete_bookmarks","bookmarks":["1"]}""", hint, StringComparison.Ordinal);
        Assert.Contains("No operation deletes links or named destinations", hint, StringComparison.Ordinal);

        CliResult cleaned = workspace.Run("pdf", "edit", "chapters.pdf", "--out", "cleaned.pdf", "--verify", "--output-format", "json", "--ops",
            """[{"op":"delete_pages","pages":"2"},{"op":"delete_bookmarks","bookmarks":["1"]}]""");

        JsonNode left = Assert.Single(cleaned.Json()["warnings"]!.AsArray(), static item => item!["code"]!.GetValue<string>() == "NAVIGATION_DEGRADED")!;
        Assert.StartsWith("0 bookmark(s), 1 link(s) and 0 named destination(s)", left["message"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    /// <summary>
    /// PDF-MOVE-BOOKMARK: a bookmark with a top of 0 on a moved page no longer leads to the
    /// page, which is still in the output, so the verified edit exits 8. The hint names the two
    /// operations and gives the command that reads, run as written, the page the bookmark opened
    /// in the input; deleting it and adding it again in the same batch makes the edit exit 0.
    /// </summary>
    [Fact]
    public void MovePages_ThatStrandsABookmark_FailsTheVerificationUntilTheBatchAddsItAgain()
    {
        using var fixture = new PdfEngineFixture();
        using var workspace = new TempWorkspace();
        using (Document document = Chapters())
        {
            document.Save(workspace.File("chapters.pdf"));
        }

        const string Move = """{"op":"move_pages","pages":"3","to":1}""";
        CliResult moved = workspace.Run("pdf", "edit", "chapters.pdf", "--out", "moved.pdf", "--verify", "--output-format", "json", "--ops", $"[{Move}]");

        Assert.True(moved.ExitCode == 8, moved.StdOut + moved.StdErr);
        JsonNode result = JsonNode.Parse(moved.StdOut)!;
        Assert.Equal("NAVIGATION_DEGRADED", Assert.Single(result["verification"]!["issues"]!.AsArray())!["code"]!.GetValue<string>());
        JsonNode warning = Assert.Single(result["warnings"]!.AsArray(), static item => item!["code"]!.GetValue<string>() == "NAVIGATION_DEGRADED")!;
        Assert.True(warning["affectsCompleteness"]!.GetValue<bool>());
        string hint = warning["hint"]!.GetValue<string>();
        Assert.Contains("""{"op":"delete_bookmarks","bookmarks":["3"]}""", hint, StringComparison.Ordinal);
        Assert.Contains("add_bookmark", hint, StringComparison.Ordinal);
        Assert.Equal(3, OpenedPage(workspace.RunCommandLine(InspectionIn(hint)), "Chapter 3"));

        CliResult fixedUp = workspace.Run("pdf", "edit", "chapters.pdf", "--out", "fixed.pdf", "--verify", "--output-format", "json", "--ops",
            $$"""[{{Move}},{"op":"delete_bookmarks","bookmarks":["3"]},{"op":"add_bookmark","title":"Chapter 3","page":1}]""");

        Assert.True(fixedUp.ExitCode == 0, fixedUp.StdOut + fixedUp.StdErr);
        JsonNode repaired = fixedUp.Json();
        Assert.True(repaired["verification"]!["ok"]!.GetValue<bool>());
        Assert.DoesNotContain(repaired["warnings"]?.AsArray() ?? [], static item => item!["code"]!.GetValue<string>() == "NAVIGATION_DEGRADED");
        using var output = new Document(workspace.File("fixed.pdf"));
        OutlineItemCollection chapter = output.Outlines.Single(static item => item.Title == "Chapter 3");
        Assert.Contains("Chapter 3", Text(output.Pages[((ExplicitDestination)chapter.Destination).PageNumber]), StringComparison.Ordinal);
    }

    /// <summary>
    /// A title with a slash, no title and titles two bookmarks share change nothing in the hint,
    /// which names no bookmark: it still says how to repair them and gives the command to read them.
    /// </summary>
    [Fact]
    public void MovePages_OfBookmarksWithAwkwardTitles_GivesTheSameHint()
    {
        using var workspace = new TempWorkspace();
        const string Target = "/Dest [5 0 R /XYZ 72 0 0]";
        PdfEngineFixture.WriteRawPdf(workspace.File("titles.pdf"),
        [
            "<< /Type /Catalog /Pages 2 0 R /Outlines 6 0 R >>",
            "<< /Type /Pages /Kids [3 0 R 4 0 R 5 0 R] /Count 3 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>",
            "<< /Type /Outlines /First 7 0 R /Last 10 0 R /Count 4 >>",
            $"<< /Title (Part A/B) /Parent 6 0 R /Next 8 0 R {Target} >>",
            $"<< /Parent 6 0 R /Prev 7 0 R /Next 9 0 R {Target} >>",
            $"<< /Title (Same) /Parent 6 0 R /Prev 8 0 R /Next 10 0 R {Target} >>",
            $"<< /Title (Same) /Parent 6 0 R /Prev 9 0 R {Target} >>",
        ]);

        CliResult moved = workspace.Run("pdf", "edit", "titles.pdf", "--out", "moved.pdf", "--verify", "--output-format", "json", "--ops",
            """[{"op":"move_pages","pages":"3","to":1}]""");

        Assert.True(moved.ExitCode == 8, moved.StdOut + moved.StdErr);
        JsonNode warning = Assert.Single(JsonNode.Parse(moved.StdOut)!["warnings"]!.AsArray(), static item => item!["code"]!.GetValue<string>() == "NAVIGATION_DEGRADED")!;
        Assert.StartsWith("4 bookmark(s), 0 link(s)", warning["message"]!.GetValue<string>(), StringComparison.Ordinal);
        string hint = warning["hint"]!.GetValue<string>();
        Assert.Contains("""{"op":"delete_bookmarks","bookmarks":["1","2","3","4"]}""", hint, StringComparison.Ordinal);
        Assert.Contains("add_bookmark", hint, StringComparison.Ordinal);
        Assert.DoesNotContain("Part A/B", hint, StringComparison.Ordinal);
        Assert.Equal(3, OpenedPage(workspace.RunCommandLine(InspectionIn(hint)), "Part A/B"));
    }

    /// <summary>
    /// An in-place edit replaces its input, so the command of the hint reads the backup, which
    /// still holds the bookmarks' pages; without a backup no file holds them, and the hint says so
    /// without giving a command that would read the edited file.
    /// </summary>
    [Fact]
    public void MovePages_InPlace_PointsTheHintAtTheBackupOrAtACopyOfTheOriginal()
    {
        using var fixture = new PdfEngineFixture();
        using var workspace = new TempWorkspace();
        using (Document document = Chapters())
        {
            document.Save(workspace.File("backed.pdf"));
            document.Save(workspace.File("bare.pdf"));
        }

        const string Ops = """[{"op":"move_pages","pages":"3","to":1}]""";
        CliResult backed = workspace.Run("pdf", "edit", "backed.pdf", "--in-place", "--backup", "--verify", "--output-format", "json", "--ops", Ops);
        CliResult bare = workspace.Run("pdf", "edit", "bare.pdf", "--in-place", "--verify", "--output-format", "json", "--ops", Ops);

        Assert.True(backed.ExitCode == 8, backed.StdOut + backed.StdErr);
        JsonNode result = JsonNode.Parse(backed.StdOut)!;
        string command = InspectionIn(Assert.Single(result["warnings"]!.AsArray(), static item => item!["code"]!.GetValue<string>() == "NAVIGATION_DEGRADED")!["hint"]!.GetValue<string>());
        Assert.Contains(Path.GetFileName(result["backup"]!["path"]!.GetValue<string>()), command, StringComparison.Ordinal);
        Assert.Equal(3, OpenedPage(workspace.RunCommandLine(command), "Chapter 3"));

        Assert.True(bare.ExitCode == 8, bare.StdOut + bare.StdErr);
        string hint = Assert.Single(JsonNode.Parse(bare.StdOut)!["warnings"]!.AsArray(), static item => item!["code"]!.GetValue<string>() == "NAVIGATION_DEGRADED")!["hint"]!.GetValue<string>();
        Assert.Contains("'pdf inspect --detail outline' on a copy of the original", hint, StringComparison.Ordinal);
        Assert.DoesNotContain("aspose-cli", hint, StringComparison.Ordinal);
    }

    /// <summary>On an encrypted input the command of the hint carries its password variable, and run as written it reads the bookmarks.</summary>
    [Fact]
    public void MovePages_OfAnEncryptedPdf_GivesAnInspectionWithItsPasswordVariable()
    {
        using var fixture = new PdfEngineFixture();
        using var workspace = new TempWorkspace();
        using (Document document = Chapters())
        {
            document.Encrypt("user-secret", "owner-secret", Aspose.Pdf.Facades.DocumentPrivilege.ForbidAll, CryptoAlgorithm.AESx256, usePdf20: false);
            document.Save(workspace.File("locked chapters.pdf"));
        }

        var variables = new Dictionary<string, string?>(StringComparer.Ordinal) { ["PDF_USER"] = "user-secret" };
        CliResult moved = workspace.RunWithEnv(variables, "pdf", "edit", "locked chapters.pdf", "--password-env", "PDF_USER", "--out", "moved.pdf", "--verify", "--output-format", "json", "--ops",
            """[{"op":"move_pages","pages":"3","to":1}]""");

        Assert.True(moved.ExitCode == 8, moved.StdOut + moved.StdErr);
        string command = InspectionIn(Assert.Single(JsonNode.Parse(moved.StdOut)!["warnings"]!.AsArray(), static item => item!["code"]!.GetValue<string>() == "NAVIGATION_DEGRADED")!["hint"]!.GetValue<string>());
        Assert.Contains(" --password-env PDF_USER", command, StringComparison.Ordinal);
        Assert.Equal(3, OpenedPage(workspace.RunCommandLine(command, variables), "Chapter 3"));
    }

    // Three chapters, each with a bookmark to its page at the destination given, by default at a
    // top of 0, which a page move cannot rebuild.
    private static Document Chapters(Func<Page, IAppointment>? destination = null)
    {
        var document = new Document();
        for (int number = 1; number <= 3; number++)
        {
            document.Pages.Add().Paragraphs.Add(new TextFragment($"Chapter {number}"));
            document.Outlines.Add(new OutlineItemCollection(document.Outlines)
            {
                Title = $"Chapter {number}",
                Destination = destination?.Invoke(document.Pages[number]) ?? new XYZExplicitDestination(document.Pages[number], 72, 0, 0),
            });
        }

        return document;
    }

    // The one runnable command a warning's hint quotes.
    private static string InspectionIn(string hint) =>
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(hint, "'(aspose-cli [^']+ --detail outline --output-format json)'")).Groups[1].Value;

    // The page the outline a command read opens for the bookmark of a title.
    private static int OpenedPage(CliResult inspected, string title) =>
        inspected.Json()["outline"]!.AsArray().First(item => item!["title"]!.GetValue<string>() == title)!["page"]!.GetValue<int>();

    /// <summary>
    /// Destinations and link areas with long fractions, as other producers write them, are
    /// recognised in the saved output: their page is still there, so they count as stranded and
    /// the verified edit exits 8, rather than passing as navigation to a deleted page.
    /// </summary>
    [Fact]
    public void MovePages_WithCoordinatesThatAreNotWhole_CountsWhatItStranded()
    {
        using var workspace = new TempWorkspace();
        PdfEngineFixture.WriteRawPdf(workspace.File("fractions.pdf"),
        [
            "<< /Type /Catalog /Pages 2 0 R /Outlines 6 0 R >>",
            "<< /Type /Pages /Kids [3 0 R 4 0 R 5 0 R] /Count 3 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [9 0 R] >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [10 0 R] >>",
            "<< /Type /Outlines /First 7 0 R /Last 8 0 R /Count 2 >>",
            "<< /Title (Top) /Parent 6 0 R /Next 8 0 R /Dest [5 0 R /XYZ 72.123456789 0 1.5] >>",
            "<< /Title (Left) /Parent 6 0 R /Prev 7 0 R /Dest [5 0 R /XYZ 72.350000001 0 0] >>",
            "<< /Type /Annot /Subtype /Link /Rect [68.123456 700.5 300.75 714.25] /Border [0 0 0] /Dest [5 0 R /XYZ 68.987654321 0 0] >>",
            // A link on the moved page, which the page's copy carries.
            "<< /Type /Annot /Subtype /Link /Rect [72.111111 500.222222 310.333333 520.444444] /Border [0 0 0] /Dest [3 0 R /XYZ 72.555555555 0 0] >>",
        ]);

        CliResult moved = workspace.Run("pdf", "edit", "fractions.pdf", "--out", "moved.pdf", "--verify", "--output-format", "json", "--ops",
            """[{"op":"move_pages","pages":"3","to":1}]""");

        Assert.True(moved.ExitCode == 8, moved.StdOut + moved.StdErr);
        JsonNode warning = Assert.Single(JsonNode.Parse(moved.StdOut)!["warnings"]!.AsArray(), static item => item!["code"]!.GetValue<string>() == "NAVIGATION_DEGRADED")!;
        Assert.True(warning["affectsCompleteness"]!.GetValue<bool>());
        Assert.StartsWith("2 bookmark(s), 2 link(s) and 0 named destination(s) no longer lead to a page", warning["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Contains("No operation re-creates links or named destinations", warning["hint"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    /// <summary>
    /// A bookmark whose left reads 0 is rebuilt with a left of 0 and follows its page: whether the
    /// left was 0 or omitted, a reader opens the page at much the same place.
    /// </summary>
    [Fact]
    public void MovePages_RebuildsABookmarkWhoseLeftReadsZero()
    {
        using var fixture = new PdfEngineFixture();
        using var workspace = new TempWorkspace();
        using (Document document = Chapters(static page => new XYZExplicitDestination(page, 0, 800, 0)))
        {
            document.Save(workspace.File("chapters.pdf"));
        }

        CliResult moved = workspace.Run("pdf", "edit", "chapters.pdf", "--out", "moved.pdf", "--verify", "--output-format", "json", "--ops",
            """[{"op":"move_pages","pages":"3","to":1}]""");

        Assert.True(moved.ExitCode == 0, moved.StdOut + moved.StdErr);
        JsonNode result = moved.Json();
        Assert.True(result["verification"]!["ok"]!.GetValue<bool>());
        Assert.DoesNotContain(result["warnings"]?.AsArray() ?? [], static item => item!["code"]!.GetValue<string>() == "NAVIGATION_DEGRADED");
        using var output = new Document(workspace.File("moved.pdf"));
        Assert.Equal("1 XYZ 0 800 0", Describe(output.Outlines.Single(static item => item.Title == "Chapter 3").Destination));
        Assert.Contains("Chapter 3", Text(output.Pages[1]), StringComparison.Ordinal);
    }

    /// <summary>Links, named destinations and what leads through them go the same way as a bookmark, to and from the moved page.</summary>
    [Fact]
    public void MovePages_RebuildsLinksAndNamedDestinationsWhoseLeftReadsZero()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("links.pdf");
        using (var document = new Document())
        {
            for (int number = 1; number <= 3; number++)
            {
                document.Pages.Add().Paragraphs.Add(new TextFragment($"Page {number}"));
            }

            Page first = document.Pages[1], third = document.Pages[3];
            document.NamedDestinations.Add("appendix", new XYZExplicitDestination(third, 0, 600, 0));
            document.Outlines.Add(new OutlineItemCollection(document.Outlines) { Title = "Named", Destination = new NamedDestination(document, "appendix") });
            first.Annotations.Add(new LinkAnnotation(first, new Rectangle(100, 100, 200, 120)) { Destination = new XYZExplicitDestination(third, 0, 500, 0) });
            first.Annotations.Add(new LinkAnnotation(first, new Rectangle(100, 200, 200, 220)) { Action = new GoToAction(new XYZExplicitDestination(third, 0, 400, 1)) });
            first.Annotations.Add(new LinkAnnotation(first, new Rectangle(100, 300, 200, 320)) { Destination = new NamedDestination(document, "appendix") });
            third.Annotations.Add(new LinkAnnotation(third, new Rectangle(100, 100, 200, 120)) { Destination = new XYZExplicitDestination(first, 0, 300, 0) });
            document.Save(input);
        }

        PdfEditResult result = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch { Ops = [new MovePagesOp { Pages = "3", To = 1 }] },
            Output = TestOutput.At(fixture.File("links.out.pdf")),
            Verify = true,
        });

        Assert.DoesNotContain(result.Warnings ?? [], static item => item.Code.Name == "NAVIGATION_DEGRADED");
        Assert.True(result.Verification!.Ok, string.Join("; ", result.Verification.Issues.Select(static issue => issue.Message)));
        using var moved = new Document(result.Output!.Path);
        Assert.Equal("1 XYZ 0 600 0", Describe(moved.NamedDestinations["appendix"]));
        Assert.Equal("1 XYZ 0 500 0", Describe(((LinkAnnotation)moved.Pages[2].Annotations[1]).Destination));
        Assert.Equal("1 XYZ 0 400 1", Describe(((GoToAction)((LinkAnnotation)moved.Pages[2].Annotations[2]).Action).Destination));
        Assert.Equal("2 XYZ 0 300 0", Describe(((LinkAnnotation)moved.Pages[1].Annotations[1]).Destination));
    }

    /// <summary>
    /// A top that reads 0 may stand for an omitted one, and the two open different places, so the
    /// destination is not rebuilt, whatever its left: it is counted and fails the verification.
    /// </summary>
    [Theory]
    [InlineData(72d)]
    [InlineData(0d)]
    public void MovePages_LeavesADestinationWhoseTopReadsZero(double left)
    {
        using var fixture = new PdfEngineFixture();

        PdfEditResult result = EditChapters(fixture, page => new XYZExplicitDestination(page, left, 0, 0), new MovePagesOp { Pages = "3", To = 1 });

        Warning warning = Assert.Single(result.Warnings!, static item => item.Code.Name == "NAVIGATION_DEGRADED");
        Assert.True(warning.AffectsCompleteness);
        Assert.StartsWith("1 bookmark(s), 0 link(s) and 0 named destination(s) no longer lead to a page", warning.Message, StringComparison.Ordinal);
        Assert.False(result.Verification!.Ok);
        Assert.True(result.HasFailures);
    }

    /// <summary>A destination without a coordinate of 0 is rebuilt exactly, as it always was.</summary>
    [Fact]
    public void MovePages_RebuildsADestinationWithoutAZeroCoordinateExactly()
    {
        using var fixture = new PdfEngineFixture();

        PdfEditResult result = EditChapters(fixture, static page => new XYZExplicitDestination(page, 72.5, 700.25, 1.5), new MovePagesOp { Pages = "3", To = 1 });

        Assert.DoesNotContain(result.Warnings ?? [], static item => item.Code.Name == "NAVIGATION_DEGRADED");
        Assert.True(result.Verification!.Ok, string.Join("; ", result.Verification.Issues.Select(static issue => issue.Message)));
        using var moved = new Document(result.Output!.Path);
        Assert.Equal("1 XYZ 72.5 700.25 1.5", Describe(moved.Outlines.Single(static item => item.Title == "Chapter 3").Destination));
    }

    /// <summary>A destination with no coordinate to read is rebuilt as it always was.</summary>
    [Fact]
    public void MovePages_RebuildsAFitDestination()
    {
        using var fixture = new PdfEngineFixture();

        PdfEditResult result = EditChapters(fixture, static page => new FitExplicitDestination(page), new MovePagesOp { Pages = "3", To = 1 });

        Assert.DoesNotContain(result.Warnings ?? [], static item => item.Code.Name == "NAVIGATION_DEGRADED");
        Assert.True(result.Verification!.Ok, string.Join("; ", result.Verification.Issues.Select(static issue => issue.Message)));
        using var moved = new Document(result.Output!.Path);
        Assert.Equal("1 Fit", Describe(moved.Outlines.Single(static item => item.Title == "Chapter 3").Destination));
    }

    /// <summary>A left of 0 changes nothing for a page the batch deleted: the loss is what the caller asked for.</summary>
    [Fact]
    public void DeletePages_ThatABookmarkWithALeftOfZeroLeadsTo_StaysWhatTheEditDeleted()
    {
        using var fixture = new PdfEngineFixture();

        PdfEditResult result = EditChapters(fixture, static page => new XYZExplicitDestination(page, 0, 800, 0), new DeletePagesOp { Pages = "3" });

        Warning warning = Assert.Single(result.Warnings!, static item => item.Code.Name == "NAVIGATION_DEGRADED");
        Assert.False(warning.AffectsCompleteness);
        Assert.Equal("1 bookmark(s), 0 link(s) and 0 named destination(s) lead to pages the edit deleted.", warning.Message);
        Assert.True(result.Verification!.Ok, string.Join("; ", result.Verification.Issues.Select(static issue => issue.Message)));
        Assert.False(result.HasFailures);
    }

    // Applies one operation with --verify to the three chapters whose bookmarks open each page at the destination given.
    private static PdfEditResult EditChapters(PdfEngineFixture fixture, Func<Page, IAppointment> destination, PdfOp operation)
    {
        string input = fixture.File("chapters.pdf");
        using (Document document = Chapters(destination))
        {
            document.Save(input);
        }

        return PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch { Ops = [operation] },
            Output = TestOutput.At(fixture.File("chapters.out.pdf")),
            Verify = true,
        });
    }

    [Fact]
    public void MovePages_RetargetsExactDestinationsAndCountsOnesWithAZeroCoordinate()
    {
        using var fixture = new PdfEngineFixture();
        string input = CreateNavigationDocument(fixture, "move.pdf");

        PdfEditResult result = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch { Ops = [new MovePagesOp { Pages = "3", To = 1 }] },
            Output = TestOutput.At(fixture.File("move.out.pdf")),
        });

        // "Three" and the explicit link are retargeted; "appendix" has a top of 0, which may stand
        // for an omitted one, so it and the bookmark and link that use it are counted.
        Warning warning = Assert.Single(result.Warnings!, static item => item.Code.Name == "NAVIGATION_DEGRADED");
        Assert.True(warning.AffectsCompleteness);
        Assert.StartsWith("1 bookmark(s), 1 link(s) and 1 named destination(s)", warning.Message, StringComparison.Ordinal);
        using var moved = new Document(result.Output!.Path);
        var three = (XYZExplicitDestination)moved.Outlines.Single(static item => item.Title == "Three").Destination;
        Assert.Equal((1, 10d, 700d, 2d), (three.PageNumber, three.Left, three.Top, three.Zoom));
        Assert.Equal(0, ((ExplicitDestination)moved.NamedDestinations["appendix"]).PageNumber);
    }

    [LicensedFact]
    public void MovePages_KeepsEveryExactDestinationOnTheMovedPages()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("exact.pdf");
        using (var document = new Document())
        {
            for (int number = 1; number <= 3; number++)
            {
                document.Pages.Add().Paragraphs.Add(new TextFragment($"Page {number}"));
            }

            Page two = document.Pages[2];
            Page three = document.Pages[3];
            document.NamedDestinations.Add("appendix", new XYZExplicitDestination(three, 20, 600, 0));
            var parent = new OutlineItemCollection(document.Outlines) { Title = "Parent", Destination = new FitExplicitDestination(two) };
            parent.Add(new OutlineItemCollection(document.Outlines) { Title = "Child", Destination = new FitRExplicitDestination(three, 0, 10, 300, 400) });
            document.Outlines.Add(parent);
            document.Outlines.Add(new OutlineItemCollection(document.Outlines) { Title = "Action", Action = new GoToAction(new FitHExplicitDestination(three, 650)) });
            document.Outlines.Add(new OutlineItemCollection(document.Outlines) { Title = "Named", Destination = new NamedDestination(document, "appendix") });
            document.Outlines.Add(new OutlineItemCollection(document.Outlines) { Title = "Bounded", Destination = new FitBVExplicitDestination(two, 30) });
            document.Pages[1].Annotations.Add(new LinkAnnotation(document.Pages[1], new Rectangle(100, 100, 200, 120))
            {
                Destination = new FitBExplicitDestination(three),
            });
            three.Annotations.Add(new LinkAnnotation(three, new Rectangle(100, 100, 200, 120))
            {
                Action = new GoToAction(new XYZExplicitDestination(two, 5, 500, 1)),
            });
            three.Annotations.Add(new LinkAnnotation(three, new Rectangle(100, 200, 200, 220))
            {
                Destination = new FitVExplicitDestination(three, 40),
            });
            three.Annotations.Add(new LinkAnnotation(three, new Rectangle(100, 300, 200, 320))
            {
                Destination = new FitHExplicitDestination(document.Pages[1], 70),
            });
            document.Save(input);
        }

        // Pages 2 and 3 move to the front: 2 becomes 1 and 3 becomes 2.
        PdfEditResult result = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch { Ops = [new MovePagesOp { Pages = "2-3", To = 1 }] },
            Output = TestOutput.At(fixture.File("exact.out.pdf")),
        });

        Assert.DoesNotContain(result.Warnings ?? [], static item => item.Code.Name == "NAVIGATION_DEGRADED");
        using var moved = new Document(result.Output!.Path);
        Assert.Equal(["Page 2", "Page 3", "Page 1"], Enumerable.Range(1, 3).Select(number => Text(moved.Pages[number])));
        OutlineItemCollection parentItem = moved.Outlines.Single(static item => item.Title == "Parent");
        Assert.Equal("1 Fit", Describe(parentItem.Destination));
        Assert.Equal("2 FitR 0 10 300 400", Describe(parentItem.Single().Destination));
        Assert.Equal("2 FitH 650", Describe(((GoToAction)moved.Outlines.Single(static item => item.Title == "Action").Action).Destination));
        Assert.Equal("1 FitBV 30", Describe(moved.Outlines.Single(static item => item.Title == "Bounded").Destination));
        Assert.Equal("2 XYZ 20 600 0", Describe(moved.NamedDestinations["appendix"]));
        Assert.Equal("2 FitB", Describe(((LinkAnnotation)moved.Pages[3].Annotations[1]).Destination));
        Assert.Equal("1 XYZ 5 500 1", Describe(((GoToAction)((LinkAnnotation)moved.Pages[2].Annotations[1]).Action).Destination));
        Assert.Equal("2 FitV 40", Describe(((LinkAnnotation)moved.Pages[2].Annotations[2]).Destination));
        Assert.Equal("3 FitH 70", Describe(((LinkAnnotation)moved.Pages[2].Annotations[3]).Destination));
    }

    /// <summary>The page number, type and coordinates of an explicit destination.</summary>
    private static string Describe(IAppointment destination) => destination switch
    {
        FitExplicitDestination fit => $"{fit.PageNumber} Fit",
        FitBExplicitDestination fit => $"{fit.PageNumber} FitB",
        FitRExplicitDestination fit => $"{fit.PageNumber} FitR {fit.Left} {fit.Bottom} {fit.Right} {fit.Top}",
        FitHExplicitDestination fit => $"{fit.PageNumber} FitH {fit.Top}",
        FitVExplicitDestination fit => $"{fit.PageNumber} FitV {fit.Left}",
        FitBVExplicitDestination fit => $"{fit.PageNumber} FitBV {fit.Left}",
        XYZExplicitDestination xyz => $"{xyz.PageNumber} XYZ {xyz.Left} {xyz.Top} {xyz.Zoom}",
        _ => destination.GetType().Name,
    };

    private static string Text(Page page)
    {
        var absorber = new TextAbsorber();
        page.Accept(absorber);
        return absorber.Text.Trim();
    }

    [Fact]
    public void AnEditThatKeepsEveryPage_DisclosesNoNavigationLoss()
    {
        using var fixture = new PdfEngineFixture();
        string input = CreateNavigationDocument(fixture, "metadata.pdf");

        PdfEditResult result = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch { Ops = [new SetPropertiesOp { Title = "Kept" }, new RotatePagesOp { Pages = "3", Rotation = 90 }] },
            Output = TestOutput.At(fixture.File("metadata.out.pdf")),
        });

        Assert.DoesNotContain(result.Warnings ?? [], static item => item.Code.Name == "NAVIGATION_DEGRADED");
    }

    [Fact]
    public void AnEdit_OfADocumentWhoseNameTreeHoldsOnlyAttachments_Succeeds()
    {
        using var fixture = new PdfEngineFixture();
        string input = CreateAttachmentOnlyNameTree(fixture, "attachments.pdf");

        PdfEditResult result = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch { Ops = [new SetPropertiesOp { Title = "Kept" }] },
            Output = TestOutput.At(fixture.File("attachments.out.pdf")),
        });

        Assert.DoesNotContain(result.Warnings ?? [], static item => item.Code.Name == "NAVIGATION_DEGRADED");
        using var edited = new Document(result.Output!.Path);
        Assert.Single(edited.EmbeddedFiles);
    }

    /// <summary>One page and one attachment: the name tree has EmbeddedFiles and no Dests.</summary>
    internal static string CreateAttachmentOnlyNameTree(PdfEngineFixture fixture, string name)
    {
        string path = fixture.File(name);
        using var document = new Document();
        document.Pages.Add().Paragraphs.Add(new TextFragment("Attachments only"));
        document.EmbeddedFiles.Add("figures.csv", new FileSpecification(new MemoryStream("a,b\n"u8.ToArray()), "figures.csv", "Figures")
        {
            Name = "figures.csv",
            UnicodeName = "figures.csv",
        });
        document.Save(path);
        return path;
    }

    /// <summary>
    /// A bookmark that opens a web address or a page of another file keeps its action when merge
    /// and split copy the outline. The command line reads no bookmark's action, so the scenario
    /// pdf-bookmarks-that-lead-outside-the-document-have-no-page shows only that it has no page.
    /// </summary>
    [Fact]
    public void MergeAndSplit_KeepTheActionOfABookmarkThatLeadsOutsideTheDocument()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("outside.pdf");
        PdfEngineFixture.WriteRawPdf(input,
        [
            "<< /Type /Catalog /Pages 2 0 R /Outlines 5 0 R >>",
            "<< /Type /Pages /Kids [3 0 R 4 0 R] /Count 2 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>",
            "<< /Type /Outlines /First 6 0 R /Last 7 0 R /Count 2 >>",
            "<< /Title (Web) /Parent 5 0 R /Next 7 0 R /A << /S /URI /URI (https://example.com/) >> >>",
            "<< /Title (Remote) /Parent 5 0 R /Prev 6 0 R /A << /S /GoToR /F (other.pdf) /D [1 /Fit] >> >>",
        ]);

        PdfMergeResult merged = PdfMerge.Run(fixture.Session, new PdfMergeRequest
        {
            Output = TestOutput.At(fixture.File("merged.pdf")),
            InputPaths = [fixture.CreateDocument("first.pdf", pages: 1), input],
        });
        PdfSplitResult split = PdfSplit.Run(fixture.Session, new PdfSplitRequest
        {
            Input = input,
            Output = new ResolvedDirectory(fixture.File("parts")),
            Plan = new Sdk.Addressing.SplitPlan(Sdk.Addressing.SplitBy.Pages, 1, null, null),
        });

        foreach (string path in (string[])[merged.Output.Path, .. split.Outputs.Select(static part => part.Path)])
        {
            using var document = new Document(path);
            Assert.Equal(2, document.Outlines.Count);
            Assert.Equal("https://example.com/", Assert.IsType<GoToURIAction>(document.Outlines[1].Action).URI);
            Assert.Equal("other.pdf", Assert.IsType<GoToRemoteAction>(document.Outlines[2].Action).File.Name);
        }
    }

    [Fact]
    public void Merge_CountsBookmarksReducedToFitAndDroppedNamedDestinations()
    {
        using var fixture = new PdfEngineFixture();
        string first = CreateNavigationDocument(fixture, "first.pdf");
        string second = fixture.CreateDocument("second.pdf", pages: 1);

        PdfMergeResult result = PdfMerge.Run(fixture.Session, new PdfMergeRequest
        {
            Output = TestOutput.At(fixture.File("merged.pdf")),
            InputPaths = [first, second],
        });

        // "Two" was Fit and survives; the named link and "appendix" lead nowhere although the
        // merged document holds their page; "Three" (XYZ) and "Named", which opened its page
        // through "appendix", still open their page, as a whole.
        AssertMergedNavigation(result);
        using var merged = new Document(result.Output.Path);
        Assert.Equal(3, merged.Outlines.Count);
    }

    /// <summary>
    /// The attachments the merged document carries give it a name tree without Dests, which a
    /// named link of another input is looked up in (PDF-NAMES-WITHOUT-DESTS).
    /// </summary>
    [Fact]
    public void Merge_OfNamedLinksAndAttachments_CountsTheLinksThatLostTheirName()
    {
        using var fixture = new PdfEngineFixture();
        string first = CreateNavigationDocument(fixture, "first.pdf");
        string second = CreateAttachmentOnlyNameTree(fixture, "second.pdf");

        PdfMergeResult result = PdfMerge.Run(fixture.Session, new PdfMergeRequest
        {
            Output = TestOutput.At(fixture.File("merged.pdf")),
            InputPaths = [first, second],
        });

        AssertMergedNavigation(result);
        using var merged = new Document(result.Output.Path);
        Assert.Single(merged.EmbeddedFiles);
    }

    private static void AssertMergedNavigation(PdfMergeResult result)
    {
        Warning[] warnings = [.. result.Warnings!.Where(static item => item.Code.Name == "NAVIGATION_DEGRADED")];
        Assert.Equal(2, warnings.Length);
        Assert.StartsWith("0 bookmark(s), 1 link(s) and 1 named destination(s) no longer lead to their page", warnings[0].Message, StringComparison.Ordinal);
        Assert.True(warnings[0].AffectsCompleteness);
        Assert.DoesNotContain("add_bookmark", warnings[0].Hint, StringComparison.Ordinal);
        Assert.StartsWith("2 bookmark(s) of the merged document open their page as a whole", warnings[1].Message, StringComparison.Ordinal);
        Assert.False(warnings[1].AffectsCompleteness);
        Assert.DoesNotContain("add_bookmark", warnings[1].Hint, StringComparison.Ordinal);
    }

    /// <summary>
    /// Three pages: bookmarks Two (Fit, page 2), Three (XYZ, page 3) and Named (via the
    /// named destination "appendix" on page 3); on page 1 an explicit link and a named
    /// link, both to page 3.
    /// </summary>
    private static string CreateNavigationDocument(PdfEngineFixture fixture, string name)
    {
        string path = fixture.File(name);
        using var document = new Document();
        for (int number = 1; number <= 3; number++)
        {
            document.Pages.Add().Paragraphs.Add(new TextFragment($"Page {number}"));
        }

        document.NamedDestinations.Add("appendix", new XYZExplicitDestination(document.Pages[3], 20, 0, 0));
        document.Outlines.Add(new OutlineItemCollection(document.Outlines) { Title = "Two", Destination = new FitExplicitDestination(document.Pages[2]) });
        document.Outlines.Add(new OutlineItemCollection(document.Outlines) { Title = "Three", Destination = new XYZExplicitDestination(document.Pages[3], 10, 700, 2) });
        document.Outlines.Add(new OutlineItemCollection(document.Outlines) { Title = "Named", Destination = new NamedDestination(document, "appendix") });
        document.Pages[1].Annotations.Add(new LinkAnnotation(document.Pages[1], new Rectangle(100, 100, 200, 120))
        {
            Action = new GoToAction(new XYZExplicitDestination(document.Pages[3], 5, 500, 1)),
        });
        document.Pages[1].Annotations.Add(new LinkAnnotation(document.Pages[1], new Rectangle(100, 200, 200, 220))
        {
            Destination = new NamedDestination(document, "appendix"),
        });
        document.Save(path);
        return path;
    }
}
