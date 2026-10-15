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

        PdfEditResult result = PdfEdit.Run(fixture.Session, Request(fixture, input, new PdfOpsBatch
        {
            Ops =
            [
                new InsertPageOp { At = 3 },
                new SetFormFieldOp { Name = "Customer", Value = "Contoso" },
                new RedactTextOp { Find = "Secret" },
                new AddBookmarkOp { Title = "Appendix", Page = 2 },
                new AddBookmarkOp { Title = "Detail", Page = 1, Parent = "1" },
                new SetPropertiesOp { Title = "Quarterly", Author = "Ops", Custom = new Dictionary<string, string> { ["Reviewed"] = "yes" } },
                new AddAttachmentOp { Path = attachment },
                new DeleteAttachmentOp { Name = "old.txt" },
            ],
        }, "verified.pdf"));

        PdfEditVerification verification = Assert.IsType<PdfEditVerification>(result.Verification);
        Assert.True(verification.Ok, string.Join("; ", verification.Issues.Select(static issue => issue.Message)));
        Assert.Equal(result.Applied.Select(static item => item.Id), verification.CheckedOps);
        Assert.False(result.HasFailures);
    }

    [Fact]
    public void RedactText_MatchesAnyCaseOnlyWhenCaseSensitiveIsFalse()
    {
        using var fixture = new PdfEngineFixture();
        string input = CreateDocument(fixture);

        PdfEditResult exact = PdfEdit.Run(fixture.Session, Request(fixture, input, new PdfOpsBatch
        {
            Ops = [new RedactTextOp { Find = "SECRET" }],
        }, "exact.pdf"));
        PdfEditResult anyCase = PdfEdit.Run(fixture.Session, Request(fixture, input, new PdfOpsBatch
        {
            Ops = [new RedactTextOp { Find = "SECRET", CaseSensitive = false }],
        }, "any-case.pdf"));

        Assert.Equal(0, Assert.Single(exact.Applied).ItemsAffected);
        Assert.Equal(["page/1"], Assert.Single(anyCase.Applied).Targets);
        Assert.True(anyCase.Verification!.Ok, string.Join("; ", anyCase.Verification.Issues.Select(static issue => issue.Message)));
        using var redacted = new Document(fixture.File("any-case.pdf"));
        var absorber = new TextAbsorber();
        redacted.Pages[1].Accept(absorber);
        Assert.DoesNotContain("Secret", absorber.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_ListsOnlyOperationsWhoseEveryEffectWasReadBack()
    {
        using var fixture = new PdfEngineFixture();
        string input = CreateDocument(fixture);

        PdfEditResult result = PdfEdit.Run(fixture.Session, Request(fixture, input, new PdfOpsBatch
        {
            Ops =
            [
                new AddBookmarkOp { Title = "Appendix", Page = 2 },
                new DeleteBookmarksOp { Bookmarks = ["1"] },
                new AddBookmarkOp { Title = "Closing", Page = 1 },
            ],
        }, "renumbered.pdf"));

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

        PdfEditResult result = PdfEdit.Run(fixture.Session, Request(fixture, input, new PdfOpsBatch
        {
            Ops = [new AddAttachmentOp { Path = attachment }],
        }, "same-name.pdf"));

        // The engine keeps both attachments named old.txt; the added one is found by its length.
        Assert.True(result.Verification!.Ok, string.Join("; ", result.Verification.Issues.Select(static issue => issue.Message)));
        Assert.Equal(["op-0001"], result.Verification.CheckedOps);
    }

    [Fact]
    public void Verify_PassesAfterDeletions()
    {
        using var fixture = new PdfEngineFixture();
        string input = CreateDocument(fixture);

        PdfEditResult result = PdfEdit.Run(fixture.Session, Request(fixture, input, new PdfOpsBatch
        {
            Ops =
            [
                new DeleteBookmarksOp { Bookmarks = ["1"] },
                new DeletePagesOp { Pages = "2" },
                new ClearPropertiesOp(),
                new SetPropertiesOp { Title = "After removal" },
            ],
        }, "deleted.pdf"));

        Assert.True(result.Verification!.Ok, string.Join("; ", result.Verification.Issues.Select(static issue => issue.Message)));
        // clear_properties has no reliable read-back: saving writes the producer and dates again.
        Assert.Equal(["op-0001", "op-0002", "op-0004"], result.Verification.CheckedOps);
    }

    [Fact]
    public void Verify_ReportsTextThatALaterOperationAddedBackAfterItsRedaction()
    {
        using var fixture = new PdfEngineFixture();
        string input = CreateDocument(fixture);

        PdfEditResult result = PdfEdit.Run(fixture.Session, Request(fixture, input, new PdfOpsBatch
        {
            Ops =
            [
                new RedactTextOp { Find = "Secret" },
                new AddHeaderTextOp { Text = "Secret draft", Pages = "1" },
            ],
        }, "readded.pdf"));

        VerificationIssue issue = Assert.Single(result.Verification!.Issues);
        Assert.Equal("REDACTED_TEXT_FOUND", issue.Code);
        Assert.Equal("page/1", issue.Location);
        Assert.StartsWith("Operation 'op-0001' (redact_text): ", issue.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Secret", issue.Message, StringComparison.Ordinal);
        Assert.False(result.Verification.Ok);
        Assert.Empty(result.Verification.CheckedOps.Except(["op-0001"]));
        Assert.True(result.HasFailures);
        Assert.True(File.Exists(result.Output!.Path));

        using var writer = new StringWriter();
        Commands.EditCommand.Table(result, new Sdk.Extensibility.Output.TableSurface(writer, Sdk.Extensibility.Output.TableFormat.Plain));
        string text = writer.ToString();
        Assert.Contains("verification: needs attention", text, StringComparison.Ordinal);
        Assert.Contains("REDACTED_TEXT_FOUND [page/1]: Operation 'op-0001' (redact_text)", text, StringComparison.Ordinal);
        Assert.Contains("hint: ", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_IgnoresTheEvaluationWatermarkWhenItMatchesARedaction()
    {
        using var fixture = new PdfEngineFixture();
        string input = CreateDocument(fixture);

        // The evaluation watermark reads "Evaluation Only. Created with Aspose.PDF. ...".
        PdfEditResult result = PdfEdit.Run(fixture.Session, Request(fixture, input, new PdfOpsBatch
        {
            Ops = [new RedactTextOp { Find = "Created with" }],
        }, "watermark.pdf"));

        Assert.True(result.Verification!.Ok, string.Join("; ", result.Verification.Issues.Select(static issue => issue.Message)));

        using var writer = new StringWriter();
        Commands.EditCommand.Table(result, new Sdk.Extensibility.Output.TableSurface(writer, Sdk.Extensibility.Output.TableFormat.Plain));
        Assert.Contains("verification: ok; checked ops: op-0001", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_ReportsAHeaderThatRepeatsARedactedPhraseNextToTheEvaluationWatermark()
    {
        using var fixture = new PdfEngineFixture();
        string input = CreateDocument(fixture);

        // In evaluation mode the header sits at the top of the page, as the watermark does.
        PdfEditResult result = PdfEdit.Run(fixture.Session, Request(fixture, input, new PdfOpsBatch
        {
            Ops =
            [
                new RedactTextOp { Find = "Created with" },
                new AddHeaderTextOp { Text = "Created with care", Pages = "1" },
            ],
        }, "header.pdf"));

        VerificationIssue issue = Assert.Single(result.Verification!.Issues);
        Assert.Equal("REDACTED_TEXT_FOUND", issue.Code);
        Assert.Contains("page 1 of the output still has 1 match(es)", issue.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Record_ThrowsForAnEffectItCannotRecord()
    {
        using var fixture = new PdfEngineFixture();
        using var document = new Document(CreateDocument(fixture));
        var verifier = new PdfEditVerifier(document);

        // Bookmark 9 does not exist, so the new bookmark's index cannot be computed; the SDK
        // runner then leaves the operation unchecked.
        var orphan = new AddBookmarkOp { Id = "op-0001", Title = "Orphan", Page = 1, Parent = "9" };
        AppliedOperation applied = AppliedOperations.Changed(PdfOp.Catalog, orphan, 1);
        Assert.Throws<InvalidOperationException>(() => verifier.Record(orphan, applied));
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
        var redact = new RedactTextOp { Id = "op-0001", Find = "(a+)+$", Regex = true };
        verifier.Record(redact, AppliedOperations.Changed(PdfOp.Catalog, redact, 0));

        VerificationFacts verification = verifier.Verify(document, fixture.LicenseState, deadline: null);

        VerificationIssue issue = Assert.Single(verification.Issues);
        Assert.Equal("VERIFICATION_INCOMPLETE", issue.Code);
        Assert.Equal("page/1", issue.Location);
        Assert.Empty(verification.Checked);
    }

    public static TheoryData<string> Unapplied =>
    [
        "set_form_field",
        "flatten_forms",
        "redact_text",
        "add_bookmark",
        "delete_bookmarks",
        "set_properties",
        "add_attachment",
        "delete_attachment",
        "insert_page",
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
            "set_form_field" => ((PdfOp)new SetFormFieldOp { Name = "Customer", Value = "Contoso" }, 1L, "FIELD_VALUE_MISMATCH"),
            "flatten_forms" => (new FlattenFormsOp { Fields = ["Customer"] }, 1L, "FIELD_NOT_FLATTENED"),
            "redact_text" => (new RedactTextOp { Find = "Secret" }, 1L, "REDACTED_TEXT_FOUND"),
            "add_bookmark" => (new AddBookmarkOp { Title = "Appendix", Page = 2 }, 1L, "BOOKMARK_MISMATCH"),
            "delete_bookmarks" => (new DeleteBookmarksOp { Bookmarks = ["1"] }, 1L, "BOOKMARK_MISMATCH"),
            "set_properties" => (new SetPropertiesOp { Title = "Quarterly" }, 1L, "PROPERTIES_MISMATCH"),
            "add_attachment" => (new AddAttachmentOp { Path = attachment }, 1L, "ATTACHMENT_MISMATCH"),
            "delete_attachment" => (new DeleteAttachmentOp { Name = "old.txt" }, 1L, "ATTACHMENT_MISMATCH"),
            "insert_page" => (new InsertPageOp { At = 3 }, 1L, "PAGE_COUNT_MISMATCH"),
            "delete_pages" => (new DeletePagesOp { Pages = "2" }, 1L, "PAGE_COUNT_MISMATCH"),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };

        PdfOp identified = op with { Id = "op-0001" };
        verifier.Record(identified, AppliedOperations.Changed(PdfOp.Catalog, identified, affected));
        VerificationFacts verification = verifier.Verify(document, fixture.LicenseState, deadline: null);

        Assert.Contains(verification.Issues, issue => issue.Code == code);
        Assert.Equal(["op-0001"], verification.Checked);
    }

    /// <summary>A missing property is located under the name its target and its read back give it.</summary>
    [Fact]
    public void Verify_LocatesAMissingPropertyAsItsTargetNamesIt()
    {
        using var fixture = new PdfEngineFixture();
        string input = CreateDocument(fixture);
        using var document = new Document(input);
        var verifier = new PdfEditVerifier(document);

        var properties = new SetPropertiesOp { Id = "op-0001", Title = "Quarterly", Custom = new Dictionary<string, string> { ["Department"] = "Sales" } };
        verifier.Record(properties, AppliedOperations.Changed(PdfOp.Catalog, properties, 2));
        VerificationFacts verification = verifier.Verify(document, fixture.LicenseState, deadline: null);

        Assert.Equal(
            ["property/Department", "property/title"],
            verification.Issues.Where(static issue => issue.Code == "PROPERTIES_MISMATCH").Select(static issue => issue.Location).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Verify_ChecksTheLastValueABatchSetsAndSkipsAFlattenedField()
    {
        using var fixture = new PdfEngineFixture();
        string input = CreateDocument(fixture);

        PdfEditResult result = PdfEdit.Run(fixture.Session, Request(fixture, input, new PdfOpsBatch
        {
            Ops =
            [
                new SetPropertiesOp { Title = "First" },
                new SetPropertiesOp { Title = "Second" },
                new SetFormFieldOp { Name = "Customer", Value = "Contoso" },
                new FlattenFormsOp(),
            ],
        }, "superseded.pdf"));

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

        PdfEditResult result = PdfEdit.Run(fixture.Session, Request(fixture, input, new PdfOpsBatch
        {
            Ops = [new FlattenFormsOp(), new ImportPagesOp { Path = source, At = 3 }],
        }, "inserted.pdf"));

        Assert.True(result.Verification!.Ok, string.Join("; ", result.Verification.Issues.Select(static issue => issue.Message)));
        Assert.Equal(["op-0002"], result.Verification.CheckedOps);
    }

    [Fact]
    public void Edit_WithoutVerify_ReportsNoVerification()
    {
        using var fixture = new PdfEngineFixture();
        string input = CreateDocument(fixture);

        PdfEditResult result = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch
            {
                Ops = [new SetPropertiesOp { Title = "Plain" }],
            },
            Output = TestOutput.At(fixture.File("plain.pdf")),
        });

        Assert.Null(result.Verification);
    }

    private static PdfEditRequest Request(PdfEngineFixture fixture, string input, PdfOpsBatch batch, string output) =>
        new() { Input = input, Batch = batch, Output = TestOutput.At(fixture.File(output)), Verify = true };

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
