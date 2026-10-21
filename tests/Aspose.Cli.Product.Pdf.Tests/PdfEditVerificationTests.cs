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
    public void Edit_InPlaceBackupTwice_WarnsBackupPredatesEdit()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("backup-twice.pdf");
        string backup = fixture.File("backup-twice.backup.pdf");
        PdfEditResult Edit(string title) => PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch { Ops = [new SetPropertiesOp { Title = title }] },
            Output = TestOutput.At(input, overwrite: true, backup: backup),
        });

        Assert.DoesNotContain(Edit("First").Warnings ?? [], static warning => warning.Code == WarningCodes.BackupPredatesEdit);
        Assert.Contains(Edit("Second").Warnings ?? [], static warning => warning.Code == WarningCodes.BackupPredatesEdit);
    }

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
    public void RedactText_MatchesOnlyTheSameCaseWhenCaseSensitiveIsTrue()
    {
        using var fixture = new PdfEngineFixture();
        string input = CreateDocument(fixture);

        PdfEditResult exact = PdfEdit.Run(fixture.Session, Request(fixture, input, new PdfOpsBatch
        {
            Ops = [new RedactTextOp { Find = "SECRET", CaseSensitive = true }],
        }, "exact.pdf"));
        PdfEditResult anyCase = PdfEdit.Run(fixture.Session, Request(fixture, input, new PdfOpsBatch
        {
            Ops = [new RedactTextOp { Find = "SECRET" }],
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
        // Redacted text left in the output is never published.
        Assert.Null(result.Output);
        Assert.False(File.Exists(fixture.File("readded.pdf")));

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

    public static TheoryData<string> Unapplied =>
    [
        "set_form_field",
        "flatten_forms",
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
        VerificationFacts verification = verifier.Verify(document);

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
        VerificationFacts verification = verifier.Verify(document);

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

    /// <summary>Deleting pages may delete a field, so a value set before it is left unchecked.</summary>
    [Fact]
    public void Verify_LeavesAFieldValueUncheckedAfterDeletedPages()
    {
        using var fixture = new PdfEngineFixture();
        string input = CreateDocument(fixture);

        PdfEditResult result = PdfEdit.Run(fixture.Session, Request(fixture, input, new PdfOpsBatch
        {
            Ops = [new SetFormFieldOp { Name = "Customer", Value = "Contoso" }, new DeletePagesOp { Pages = "2" }],
        }, "deleted.pdf"));

        Assert.True(result.Verification!.Ok, string.Join("; ", result.Verification.Issues.Select(static issue => issue.Message)));
        Assert.Equal(["op-0002"], result.Verification.CheckedOps);
    }

    /// <summary>Clearing the document information supersedes the properties set before it.</summary>
    [Fact]
    public void Verify_DropsPropertiesThatClearPropertiesRemoved()
    {
        using var fixture = new PdfEngineFixture();
        string input = CreateDocument(fixture);

        PdfEditResult result = PdfEdit.Run(fixture.Session, Request(fixture, input, new PdfOpsBatch
        {
            Ops = [new SetPropertiesOp { Title = "Quarterly" }, new ClearPropertiesOp()],
        }, "cleared.pdf"));

        Assert.True(result.Verification!.Ok, string.Join("; ", result.Verification.Issues.Select(static issue => issue.Message)));
        Assert.Empty(result.Verification.CheckedOps);
    }

    /// <summary>An attachment added again under the name a batch deleted supersedes the deletion.</summary>
    [Fact]
    public void Verify_ChecksAnAttachmentAddedAgainAfterItsDeletion()
    {
        using var fixture = new PdfEngineFixture();
        string input = CreateDocument(fixture);
        string attachment = fixture.File("old.txt");
        File.WriteAllText(attachment, "new");

        PdfEditResult result = PdfEdit.Run(fixture.Session, Request(fixture, input, new PdfOpsBatch
        {
            Ops = [new DeleteAttachmentOp { Name = "old.txt" }, new AddAttachmentOp { Path = attachment }],
        }, "readded.pdf"));

        Assert.True(result.Verification!.Ok, string.Join("; ", result.Verification.Issues.Select(static issue => issue.Message)));
        Assert.Equal(["op-0002"], result.Verification.CheckedOps);
    }

    private static readonly PdfRectInput Area = new() { X = 10, Y = 10, Width = 20, Height = 20 };

    /// <summary>The operations whose effect has no reliable read-back.</summary>
    public static TheoryData<string> Unread =>
    [
        "rotate_pages",
        "crop_pages",
        "set_page_size",
        "add_watermark_text",
        "add_watermark_image",
        "add_page_numbers",
        "add_header_text",
        "add_footer_text",
        "add_stamp_image",
        "add_hyperlink",
        "redact_area",
        "set_page_labels",
        "encrypt_document",
        "decrypt_document",
        "optimize_document",
    ];

    /// <summary>Every operation is either read back or listed as unread, and none is both.</summary>
    [Fact]
    public void EveryOperationIsReadBackOrListedAsUnread()
    {
        // redact_text is searched for before publishing, so a published one is checked.
        string[] readBack = [.. Unapplied.Select(static row => row.Data), "import_pages", "move_pages", "clear_properties", "redact_text"];
        string[] unread = [.. Unread.Select(static row => row.Data)];

        Assert.Empty(readBack.Intersect(unread));
        Assert.Equal(PdfOp.Catalog.Names.Order(StringComparer.Ordinal), readBack.Concat(unread).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// An operation without a reliable read-back records nothing: it is not checked, and the
    /// expectations recorded before it are still read back.
    /// </summary>
    [Theory]
    [MemberData(nameof(Unread))]
    public void Verify_ChecksNothingOfAnUnreadOperationAndKeepsEarlierExpectations(string operation)
    {
        using var fixture = new PdfEngineFixture();
        using var document = new Document(CreateDocument(fixture));
        PdfOp unread = (operation switch
        {
            "rotate_pages" => (PdfOp)new RotatePagesOp { Pages = "1", Rotation = 90 },
            "crop_pages" => new CropPagesOp { Pages = "1", Rect = Area },
            "set_page_size" => new SetPageSizeOp { Pages = "1", PageSize = new PageSizeInput { Preset = PageSizePresets.A4 } },
            "add_watermark_text" => new AddWatermarkTextOp { Text = "DRAFT" },
            "add_watermark_image" => new AddWatermarkImageOp { Path = "logo.png" },
            "add_page_numbers" => new AddPageNumbersOp(),
            "add_header_text" => new AddHeaderTextOp { Text = "Header" },
            "add_footer_text" => new AddFooterTextOp { Text = "Footer" },
            "add_stamp_image" => new AddStampImageOp { Page = 1, Path = "logo.png", Rect = Area },
            "add_hyperlink" => new AddHyperlinkOp { Page = 1, Rect = Area, Url = "https://example.com" },
            "redact_area" => new RedactAreaOp { Page = 1, Rect = Area },
            "set_page_labels" => new SetPageLabelsOp { Ranges = [new PdfPageLabelRange { StartPage = 1, Prefix = "A-" }] },
            "encrypt_document" => new EncryptDocumentOp { OwnerPasswordEnv = "OWNER_PASSWORD" },
            "decrypt_document" => new DecryptDocumentOp(),
            "optimize_document" => new OptimizeDocumentOp(),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        }) with { Id = "op-0003" };

        // Both hold in the unchanged document, so each is read back as met.
        PdfOp[] earlier =
        [
            new SetFormFieldOp { Id = "op-0001", Name = "Customer", Value = "Draft" },
            new DeleteAttachmentOp { Id = "op-0002", Name = "missing.txt" },
        ];
        var verifier = new PdfEditVerifier(document);
        foreach (PdfOp op in earlier.Append(unread))
        {
            verifier.Record(op, AppliedOperations.Changed(PdfOp.Catalog, op, 1));
        }

        VerificationFacts verification = verifier.Verify(document);

        Assert.Empty(verification.Issues);
        Assert.Equal(["op-0001", "op-0002"], verification.Checked.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// A page operation renumbers the pages, so a bookmark's page recorded before it is left
    /// unchecked; a redaction limited to pages is checked, because before publishing the output
    /// was searched on the pages it covered, wherever they moved.
    /// </summary>
    [Theory]
    [InlineData("insert_page")]
    [InlineData("import_pages")]
    [InlineData("delete_pages")]
    [InlineData("move_pages")]
    public void Verify_LeavesBookmarksUncheckedAfterAPageOperationAndChecksPagedRedactions(string operation)
    {
        using var fixture = new PdfEngineFixture();
        string input = CreateDocument(fixture);
        string source = fixture.File("source.pdf");
        File.Copy(input, source);
        PdfOp pages = operation switch
        {
            "insert_page" => new InsertPageOp { At = 3 },
            "import_pages" => new ImportPagesOp { Path = source, Pages = "2", At = 3 },
            "delete_pages" => new DeletePagesOp { Pages = "2" },
            "move_pages" => new MovePagesOp { Pages = "2", To = 1 },
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };

        PdfEditResult result = PdfEdit.Run(fixture.Session, Request(fixture, input, new PdfOpsBatch
        {
            Ops = [new RedactTextOp { Find = "Secret", Pages = "1" }, new AddBookmarkOp { Title = "Appendix", Page = 1 }, pages],
        }, "renumbered.pdf"));

        Assert.True(result.Verification!.Ok, string.Join("; ", result.Verification.Issues.Select(static issue => issue.Message)));
        Assert.Equal(["op-0001", "op-0003"], result.Verification.CheckedOps);
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
