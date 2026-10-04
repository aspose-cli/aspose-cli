using Aspose.Cli.Product.Pdf.Engine.Editing;
using Aspose.Pdf;
using Aspose.Pdf.Annotations;
using Aspose.Pdf.Forms;
using Aspose.Pdf.Text;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>
/// <c>pdf edit --verify</c> reads the staged output back: an applied batch verifies, and an
/// output that lacks an operation's effect reports one issue for it.
/// </summary>
public sealed class PdfEditVerificationTests
{
    [Fact]
    public void Verify_PassesForEveryCheckedOperationThatTookEffect()
    {
        using var fixture = new PdfEngineFixture();
        string input = CreateDocument(fixture);
        string attachment = fixture.File("figures.csv");
        File.WriteAllText(attachment, "region,total\neast,1\n");

        PdfEditResult result = fixture.Engine.ApplyOps(input, new PdfOpsBatch
        {
            Ops =
            [
                new InsertBlankPageOp { At = 3 },
                new SetFormFieldOp { Name = "Customer", Value = "Contoso" },
                new RedactTextOp { Pattern = "Secret" },
                new AddBookmarkOp { Title = "Appendix", Page = 2 },
                new AddBookmarkOp { Title = "Detail", Page = 1, Parent = "1" },
                new SetMetadataOp { Title = "Quarterly", Author = "Ops", Custom = new Dictionary<string, string> { ["Reviewed"] = "yes" } },
                new AddAttachmentOp { Path = attachment },
                new RemoveAttachmentOp { Name = "old.txt" },
            ],
        }, Request(fixture, "verified.pdf"));

        PdfEditVerification verification = Assert.IsType<PdfEditVerification>(result.Verification);
        Assert.True(verification.Ok, string.Join("; ", verification.Issues.Select(static issue => issue.Message)));
        Assert.Equal(result.Applied.Select(static item => item.Id), verification.CheckedOps);
        Assert.False(result.HasFailures);
    }

    [Fact]
    public void Verify_ListsOnlyOperationsWhoseEveryEffectWasReadBack()
    {
        using var fixture = new PdfEngineFixture();
        string input = CreateDocument(fixture);

        PdfEditResult result = fixture.Engine.ApplyOps(input, new PdfOpsBatch
        {
            Ops =
            [
                new AddBookmarkOp { Title = "Appendix", Page = 2 },
                new DeleteBookmarksOp { Indexes = ["1"] },
                new AddBookmarkOp { Title = "Closing", Page = 1 },
            ],
        }, Request(fixture, "renumbered.pdf"));

        // The first bookmark's position was renumbered by the deletion, so it was not checked.
        Assert.True(result.Verification!.Ok, string.Join("; ", result.Verification.Issues.Select(static issue => issue.Message)));
        Assert.Equal(["op-0002", "op-0003"], result.Verification.CheckedOps);
    }

    [Fact]
    public void Verify_ReportsAnAttachmentAddedUnderAnExistingName()
    {
        using var fixture = new PdfEngineFixture();
        string input = CreateDocument(fixture);
        string attachment = fixture.File("old.txt");
        File.WriteAllText(attachment, "a longer replacement");

        PdfEditResult result = fixture.Engine.ApplyOps(input, new PdfOpsBatch
        {
            Ops = [new AddAttachmentOp { Path = attachment }],
        }, Request(fixture, "same-name.pdf"));

        // The engine keeps both attachments named old.txt; the added one is found by its length.
        Assert.True(result.Verification!.Ok, string.Join("; ", result.Verification.Issues.Select(static issue => issue.Message)));
        Assert.Equal(["op-0001"], result.Verification.CheckedOps);
    }

    [Fact]
    public void Verify_PassesAfterDeletions()
    {
        using var fixture = new PdfEngineFixture();
        string input = CreateDocument(fixture);

        PdfEditResult result = fixture.Engine.ApplyOps(input, new PdfOpsBatch
        {
            Ops =
            [
                new DeleteBookmarksOp { Indexes = ["1"] },
                new DeletePagesOp { Pages = "2" },
                new RemoveMetadataOp(),
                new SetMetadataOp { Title = "After removal" },
            ],
        }, Request(fixture, "deleted.pdf"));

        Assert.True(result.Verification!.Ok, string.Join("; ", result.Verification.Issues.Select(static issue => issue.Message)));
        // remove_metadata has no reliable read-back: saving writes the producer and dates again.
        Assert.Equal(["op-0001", "op-0002", "op-0004"], result.Verification.CheckedOps);
    }

    [Fact]
    public void Verify_ReportsTextThatALaterOperationAddedBackAfterItsRedaction()
    {
        using var fixture = new PdfEngineFixture();
        string input = CreateDocument(fixture);

        PdfEditResult result = fixture.Engine.ApplyOps(input, new PdfOpsBatch
        {
            Ops =
            [
                new RedactTextOp { Pattern = "Secret" },
                new AddHeaderTextOp { Text = "Secret draft", Pages = "1" },
            ],
        }, Request(fixture, "readded.pdf"));

        VerificationIssue issue = Assert.Single(result.Verification!.Issues);
        Assert.Equal("PDF_REDACTED_TEXT_FOUND", issue.Code);
        Assert.Equal("pdf/page/1", issue.Location);
        Assert.StartsWith("Operation 'op-0001' (redact_text): ", issue.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Secret", issue.Message, StringComparison.Ordinal);
        Assert.False(result.Verification.Ok);
        Assert.Empty(result.Verification.CheckedOps.Except(["op-0001"]));
        Assert.True(result.HasFailures);
        Assert.True(File.Exists(result.Output!.Path));

        using var writer = new StringWriter();
        Output.PdfRenderers.Render(result, new Sdk.Extensibility.Output.TableSurface(writer, Sdk.Extensibility.Output.TableFormat.Plain));
        string text = writer.ToString();
        Assert.Contains("verification: needs attention", text, StringComparison.Ordinal);
        Assert.Contains("PDF_REDACTED_TEXT_FOUND [pdf/page/1]: Operation 'op-0001' (redact_text)", text, StringComparison.Ordinal);
        Assert.Contains("hint: ", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_IgnoresTheEvaluationWatermarkWhenItMatchesARedaction()
    {
        using var fixture = new PdfEngineFixture();
        string input = CreateDocument(fixture);

        // The evaluation watermark reads "Evaluation Only. Created with Aspose.PDF. ...".
        PdfEditResult result = fixture.Engine.ApplyOps(input, new PdfOpsBatch
        {
            Ops = [new RedactTextOp { Pattern = "Created with" }],
        }, Request(fixture, "watermark.pdf"));

        Assert.True(result.Verification!.Ok, string.Join("; ", result.Verification.Issues.Select(static issue => issue.Message)));

        using var writer = new StringWriter();
        Output.PdfRenderers.Render(result, new Sdk.Extensibility.Output.TableSurface(writer, Sdk.Extensibility.Output.TableFormat.Plain));
        Assert.Contains("verification: ok (1 operation(s) read back: op-0001)", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_ReportsAHeaderThatRepeatsARedactedPhraseNextToTheEvaluationWatermark()
    {
        using var fixture = new PdfEngineFixture();
        string input = CreateDocument(fixture);

        // In evaluation mode the header sits at the top of the page, as the watermark does.
        PdfEditResult result = fixture.Engine.ApplyOps(input, new PdfOpsBatch
        {
            Ops =
            [
                new RedactTextOp { Pattern = "Created with" },
                new AddHeaderTextOp { Text = "Created with care", Pages = "1" },
            ],
        }, Request(fixture, "header.pdf"));

        VerificationIssue issue = Assert.Single(result.Verification!.Issues);
        Assert.Equal("PDF_REDACTED_TEXT_FOUND", issue.Code);
        Assert.Contains("page 1 of the output still has 1 match(es)", issue.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Record_LeavesAnEffectItCannotRecordUncheckedInsteadOfThrowing()
    {
        using var fixture = new PdfEngineFixture();
        using var document = new Document(CreateDocument(fixture));
        var verifier = new PdfEditVerifier(document);

        // Bookmark 9 does not exist, so the new bookmark's index cannot be computed.
        verifier.Record(new AddBookmarkOp { Title = "Orphan", Page = 1, Parent = "9" }, "op-0001", 1, document);
        PdfEditVerification verification = verifier.Verify(document, fixture.LicenseState, deadline: null);

        Assert.Empty(verification.CheckedOps);
    }

    [Fact]
    public void Verify_ReportsAPatternThatTimesOutAsAnUncheckedPage()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("backtracking.pdf");
        using (var created = new Document())
        {
            created.Pages.Add().Paragraphs.Add(new TextFragment(new string('a', 40) + "!"));
            created.Save(input);
        }

        using var document = new Document(input);
        var verifier = new PdfEditVerifier(document);
        verifier.Record(new RedactTextOp { Pattern = "(a+)+$", Regex = true }, "op-0001", 0, document);

        PdfEditVerification verification = verifier.Verify(document, fixture.LicenseState, deadline: null);

        VerificationIssue issue = Assert.Single(verification.Issues);
        Assert.Equal("PDF_VERIFICATION_INCOMPLETE", issue.Code);
        Assert.Equal("pdf/page/1", issue.Location);
        Assert.Empty(verification.CheckedOps);
    }

    public static TheoryData<string> Unapplied =>
    [
        "set_form_field",
        "flatten_forms",
        "redact_text",
        "add_bookmark",
        "delete_bookmarks",
        "set_metadata",
        "add_attachment",
        "remove_attachment",
        "insert_blank_page",
        "delete_pages",
    ];

    /// <summary>
    /// The verifier records an operation without the operation running, then reads the
    /// unchanged document: the effect is missing, so the check must report it.
    /// </summary>
    [Theory]
    [MemberData(nameof(Unapplied))]
    public void Verify_ReportsAnOperationWhoseEffectIsMissing(string operation)
    {
        using var fixture = new PdfEngineFixture();
        string input = CreateDocument(fixture);
        string attachment = fixture.File("figures.csv");
        File.WriteAllText(attachment, "a,b\n");
        using var document = new Document(input);
        var verifier = new PdfEditVerifier(document);
        (PdfOp op, long affected, string code) = operation switch
        {
            "set_form_field" => ((PdfOp)new SetFormFieldOp { Name = "Customer", Value = "Contoso" }, 1L, "PDF_FIELD_VALUE_MISMATCH"),
            "flatten_forms" => (new FlattenFormsOp { Fields = ["Customer"] }, 1L, "PDF_FIELD_NOT_FLATTENED"),
            "redact_text" => (new RedactTextOp { Pattern = "Secret" }, 1L, "PDF_REDACTED_TEXT_FOUND"),
            "add_bookmark" => (new AddBookmarkOp { Title = "Appendix", Page = 2 }, 1L, "PDF_BOOKMARK_MISMATCH"),
            "delete_bookmarks" => (new DeleteBookmarksOp { Indexes = ["1"] }, 1L, "PDF_BOOKMARK_MISMATCH"),
            "set_metadata" => (new SetMetadataOp { Title = "Quarterly" }, 1L, "PDF_METADATA_MISMATCH"),
            "add_attachment" => (new AddAttachmentOp { Path = attachment }, 1L, "PDF_ATTACHMENT_MISMATCH"),
            "remove_attachment" => (new RemoveAttachmentOp { Name = "old.txt" }, 1L, "PDF_ATTACHMENT_MISMATCH"),
            "insert_blank_page" => (new InsertBlankPageOp { At = 3 }, 1L, "PDF_PAGE_COUNT_MISMATCH"),
            "delete_pages" => (new DeletePagesOp { Pages = "2" }, 1L, "PDF_PAGE_COUNT_MISMATCH"),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };

        verifier.Record(op, "op-0001", affected, document);
        PdfEditVerification verification = verifier.Verify(document, fixture.LicenseState, deadline: null);

        Assert.False(verification.Ok);
        Assert.Contains(verification.Issues, issue => issue.Code == code);
        Assert.Equal(["op-0001"], verification.CheckedOps);
    }

    [Fact]
    public void Verify_ChecksTheLastValueABatchSetsAndSkipsAFlattenedField()
    {
        using var fixture = new PdfEngineFixture();
        string input = CreateDocument(fixture);

        PdfEditResult result = fixture.Engine.ApplyOps(input, new PdfOpsBatch
        {
            Ops =
            [
                new SetMetadataOp { Title = "First" },
                new SetMetadataOp { Title = "Second" },
                new SetFormFieldOp { Name = "Customer", Value = "Contoso" },
                new FlattenFormsOp(),
            ],
        }, Request(fixture, "superseded.pdf"));

        Assert.True(result.Verification!.Ok, string.Join("; ", result.Verification.Issues.Select(static issue => issue.Message)));
        Assert.Equal(["op-0002", "op-0004"], result.Verification.CheckedOps);
    }

    [Fact]
    public void Verify_SkipsAFlattenThatInsertedPagesBringFieldsAfter()
    {
        using var fixture = new PdfEngineFixture();
        string input = CreateDocument(fixture);
        string source = fixture.File("source.pdf");
        File.Copy(input, source);

        PdfEditResult result = fixture.Engine.ApplyOps(input, new PdfOpsBatch
        {
            Ops = [new FlattenFormsOp(), new InsertPagesFromOp { Path = source, At = 3 }],
        }, Request(fixture, "inserted.pdf"));

        Assert.True(result.Verification!.Ok, string.Join("; ", result.Verification.Issues.Select(static issue => issue.Message)));
        Assert.Equal(["op-0002"], result.Verification.CheckedOps);
    }

    [Fact]
    public void Edit_WithoutVerify_ReportsNoVerification()
    {
        using var fixture = new PdfEngineFixture();
        string input = CreateDocument(fixture);

        PdfEditResult result = fixture.Engine.ApplyOps(input, new PdfOpsBatch
        {
            Ops = [new SetMetadataOp { Title = "Plain" }],
        }, new PdfEditRequest { OutputPath = fixture.File("plain.pdf") });

        Assert.Null(result.Verification);
    }

    private static PdfEditRequest Request(PdfEngineFixture fixture, string output) =>
        new() { OutputPath = fixture.File(output), Verify = true };

    /// <summary>
    /// Two pages ("Customer Secret 42", "Public text"), a text field Customer on page 1, one
    /// bookmark and an attachment old.txt: within what evaluation mode reads.
    /// </summary>
    private static string CreateDocument(PdfEngineFixture fixture)
    {
        string path = fixture.File("input.pdf");
        using var document = new Document();
        Page first = document.Pages.Add();
        first.Paragraphs.Add(new TextFragment("Customer Secret 42"));
        Page second = document.Pages.Add();
        second.Paragraphs.Add(new TextFragment("Public text"));
        document.Form.Add(new TextBoxField(first, new Rectangle(72, 600, 280, 630)) { PartialName = "Customer", Value = "Draft" });
        document.Outlines.Add(new OutlineItemCollection(document.Outlines)
        {
            Title = "Summary",
            Destination = new FitExplicitDestination(first),
        });
        document.EmbeddedFiles.Add("old.txt", new FileSpecification(new MemoryStream("old"u8.ToArray()), "old.txt", "Old")
        {
            Name = "old.txt",
            UnicodeName = "old.txt",
        });
        document.Save(path);
        return path;
    }
}
