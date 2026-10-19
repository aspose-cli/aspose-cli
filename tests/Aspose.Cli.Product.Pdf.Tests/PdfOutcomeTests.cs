using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>
/// What a PDF edit reports for each operation comes from the SDK's snapshots of the objects it
/// can change: the pages it changed, where they are in the output, and <c>OP_NO_EFFECT</c> when
/// it changed nothing.
/// </summary>
public sealed class PdfOutcomeTests
{
    private static PdfEditResult Edit(PdfEngineFixture fixture, string input, params PdfOp[] ops) =>
        PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch { Ops = ops },
            Output = TestOutput.At(fixture.File(Guid.NewGuid().ToString("N") + ".pdf")),
        });

    [Fact]
    public void MovePages_CountsTheMovedPageOnceWhereItLands()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument(pages: 3);

        PdfEditResult result = Edit(fixture, input, new MovePagesOp { Pages = "3", To = 1 });

        BoundedOperationOutcome outcome = Assert.Single(result.Applied);
        Assert.Equal(1, outcome.ItemsAffected);
        Assert.Equal(["page/1"], outcome.Targets);
        Assert.Equal([1], result.PagesTouched);
    }

    // Four pages, the most evaluation mode reads.
    [Theory]
    [InlineData("4", 3, new[] { "page/3" }, new[] { 3 })]
    [InlineData("3-4", 2, new[] { "page/2", "page/3" }, new[] { 2, 3 })]
    [InlineData("2", 5, new[] { "page/4" }, new[] { 4 })]
    public void MovePages_CountsEachMovedPageWhereItLandsWhenItPassesFewerPages(string pages, int to, string[] targets, int[] touched)
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument(pages: 4);

        PdfEditResult result = Edit(fixture, input, new MovePagesOp { Pages = pages, To = to });

        BoundedOperationOutcome outcome = Assert.Single(result.Applied);
        Assert.Equal(targets.Length, outcome.ItemsAffected);
        Assert.Equal(targets, outcome.Targets);
        Assert.Equal(touched, result.PagesTouched);
    }

    public static TheoryData<string> Stamps =>
        ["add_watermark_text", "add_watermark_image", "add_page_numbers", "add_header_text", "add_footer_text", "add_stamp_image", "add_hyperlink"];

    /// <summary>
    /// A page snapshot leaves out the content stream unless the operation redacts, so what each
    /// operation that draws on a page adds must show in the rest of its state.
    /// </summary>
    [Theory]
    [MemberData(nameof(Stamps))]
    public void AnOperationThatDrawsOnPages_CountsEachPageItDrawsOn(string op)
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument(pages: 2);
        string image = fixture.File("stamp.bmp");
        File.WriteAllBytes(image, PdfMutateTests.Bitmap(20, 20));
        var rect = new PdfRectInput { X = 72, Y = 72, Width = 144, Height = 72 };
        (PdfOp operation, string[] pages) = op switch
        {
            "add_watermark_text" => ((PdfOp)new AddWatermarkTextOp { Text = "DRAFT" }, new[] { "page/1", "page/2" }),
            "add_watermark_image" => (new AddWatermarkImageOp { Path = image }, ["page/1", "page/2"]),
            "add_page_numbers" => (new AddPageNumbersOp(), ["page/1", "page/2"]),
            "add_header_text" => (new AddHeaderTextOp { Text = "Head" }, ["page/1", "page/2"]),
            "add_footer_text" => (new AddFooterTextOp { Text = "Foot" }, ["page/1", "page/2"]),
            "add_stamp_image" => (new AddStampImageOp { Page = 2, Path = image, Rect = rect }, ["page/2"]),
            "add_hyperlink" => (new AddHyperlinkOp { Page = 2, Rect = rect, Url = "https://example.com/" }, ["page/2"]),
            _ => throw new ArgumentOutOfRangeException(nameof(op)),
        };

        PdfEditResult result = Edit(fixture, input, operation);

        Assert.Equal(pages.Length, result.Applied[0].ItemsAffected);
        Assert.Equal(pages, result.Applied[0].Targets);
    }

    [Fact]
    public void APageAnEarlierOperationRotated_IsNamedWhereALaterMoveLeavesIt()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument(pages: 4);

        // Page 1 moves to the end: the output is pages 2, 3, 4, 1 of the input.
        PdfEditResult result = Edit(
            fixture,
            input,
            new RotatePagesOp { Pages = "1", Rotation = 90 },
            new MovePagesOp { Pages = "1", To = 5 });

        Assert.Equal(["page/4"], result.Applied[0].Targets);
        Assert.Equal(["page/4"], result.Applied[1].Targets);
        Assert.Equal([4], result.PagesTouched);
    }

    [Fact]
    public void AMovedPage_IsNamedWhereALaterDeletionLeavesIt_AndTheDeletedOneByItsNumberInTheInput()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument(pages: 4);

        // Page 1 moves to the end (2, 3, 4, 1), then input page 2, now first, is deleted: 3, 4, 1.
        PdfEditResult result = Edit(
            fixture,
            input,
            new MovePagesOp { Pages = "1", To = 5 },
            new DeletePagesOp { Pages = "1" });

        Assert.Equal(["page/3"], result.Applied[0].Targets);
        Assert.Equal(["page/2"], result.Applied[1].Targets);
        Assert.Equal([3], result.PagesTouched);
    }

    [Fact]
    public void DeletePages_NamesTheDeletedPageAndTouchesNone()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument(pages: 3);

        PdfEditResult result = Edit(fixture, input, new DeletePagesOp { Pages = "2" });

        Assert.Equal(1, result.Applied[0].ItemsAffected);
        Assert.Equal(["page/2"], result.Applied[0].Targets);
        Assert.Null(result.PagesTouched);
    }

    [Fact]
    public void SetMetadata_CountsEachEntryItChanged()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument(pages: 1);

        PdfEditResult result = Edit(fixture, input,
            new SetPropertiesOp { Title = "Quarterly", Author = "Ann" },
            new SetPropertiesOp { Title = "Quarterly", Custom = new Dictionary<string, string> { ["Department"] = "Sales" } });

        Assert.Equal(["property/author", "property/title"], result.Applied[0].Targets.Order(StringComparer.Ordinal));
        // The title is already set; only the new entry changed.
        Assert.Equal(1, result.Applied[1].ItemsAffected);
        Assert.Equal(["property/Department"], result.Applied[1].Targets);
        Assert.Null(result.Warnings?.FirstOrDefault(static warning => warning.Code.Name == "OP_NO_EFFECT"));
    }

    /// <summary>Setting an entry the engine writes on every save changes nothing in the output.</summary>
    [Fact]
    public void SetProperties_CustomEntriesReadBackUnderTheNamesTheirTargetsGive()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument(pages: 1);

        PdfEditResult result = Edit(fixture, input, new SetPropertiesOp
        {
            Title = "Quarterly",
            Custom = new Dictionary<string, string> { ["Department"] = "Sales", ["Cost Centre"] = "42" },
        });

        IReadOnlyDictionary<string, string?> read = Metadata(fixture, result.Output!.Path);
        BoundedOperationOutcome outcome = Assert.Single(result.Applied);
        Assert.Equal(["property/Cost Centre", "property/Department", "property/title"], outcome.Targets.Order(StringComparer.Ordinal));
        Assert.All(outcome.Targets, target => Assert.NotNull(read[target["property/".Length..]]));
        Assert.Equal("Sales", read["Department"]);
        Assert.Equal("42", read["Cost Centre"]);
    }

    private static IReadOnlyDictionary<string, string?> Metadata(PdfEngineFixture fixture, string path) =>
        PdfInfo.Run(fixture.Session, new PdfInspectRequest { Input = path, Details = ["properties"] }).Properties!;

    public static TheoryData<string> Unchanged => ["rotate_pages", "decrypt_document", "flatten_forms", "set_properties"];

    /// <summary>An operation whose objects already have the state it sets reports 0 and OP_NO_EFFECT.</summary>
    [Theory]
    [MemberData(nameof(Unchanged))]
    public void AnOperationThatChangesNothing_WarnsOpNoEffect(string op)
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument(pages: 1);
        PdfOp[] ops = op switch
        {
            "rotate_pages" => [new RotatePagesOp { Pages = "1", Rotation = 0 }],
            "decrypt_document" => [new DecryptDocumentOp()],
            "flatten_forms" => [new FlattenFormsOp()],
            "set_properties" => [new SetPropertiesOp { Title = "Same" }, new SetPropertiesOp { Title = "Same" }],
            _ => throw new ArgumentOutOfRangeException(nameof(op)),
        };

        PdfEditResult result = Edit(fixture, input, ops);

        BoundedOperationOutcome outcome = result.Applied[^1];
        Assert.Equal(OpStatuses.Ok, outcome.Status);
        Assert.Equal(0, outcome.ItemsAffected);
        Assert.Empty(outcome.Targets);
        Warning warning = Assert.Single(result.Warnings!, static warning => warning.Code.Name == "OP_NO_EFFECT");
        Assert.Equal(outcome.Id, warning.Details!["id"]!.GetValue<string>());
        Assert.Equal(op, warning.Details["op"]!.GetValue<string>());
        Assert.Equal(op == "flatten_forms" ? "no-match" : "no-change", warning.Details["reason"]!.GetValue<string>());
    }

    /// <summary>
    /// A text redaction counts the pages it removed text from, and only those, on pages that also
    /// carry stamps: a header, page numbers and a watermark.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RedactText_OnStampedPages_CountsOnlyThePagesItRemovedTextFrom(bool regex)
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument(pages: 2);
        string stamped = Edit(fixture, input,
            new AddHeaderTextOp { Text = "Internal", Placement = PdfPlacements.TopRight },
            new AddPageNumbersOp { Text = "Page {n} of {N}", Placement = PdfPlacements.BottomCenter },
            new AddWatermarkTextOp { Text = "DRAFT", FontSize = 60, Rotation = -45 }).Output!.Path;

        PdfEditResult missed = Edit(fixture, stamped, new RedactTextOp { Find = regex ? @"ZZZ\d+" : "ZZZ", Regex = regex });
        BoundedOperationOutcome none = Assert.Single(missed.Applied);
        Assert.Equal(0, none.ItemsAffected);
        Assert.Empty(none.Targets);
        Assert.Single(missed.Warnings!, static warning => warning.Code.Name == "OP_NO_EFFECT");

        PdfEditResult found = Edit(fixture, stamped, new RedactTextOp { Find = regex ? @"page\s1" : "page 1", Regex = regex });
        BoundedOperationOutcome one = Assert.Single(found.Applied);
        Assert.Equal(["page/1"], one.Targets);
        Assert.Null(found.Warnings?.FirstOrDefault(static warning => warning.Code.Name == "OP_NO_EFFECT"));
    }

    [Fact]
    public void RedactArea_OverNoCharacterMiddle_SaysWhyNothingWasRemoved()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument(pages: 1);

        // A strip across the bottom of a line's characters, below their middles.
        PdfRect line = Assert.Single(PdfSearch.Run(fixture.Session, new PdfSearchRequest
        {
            Input = input,
            Query = new Aspose.Cli.Sdk.Text.SearchQuery(Aspose.Cli.Sdk.Text.TextSearch.Create("Portable", false, caseSensitive: true), 10, Scope: null),
        }).Hits).Rect;
        var strip = new PdfRectInput { X = line.X, Y = line.Y + (line.Height * 0.75), Width = line.Width, Height = line.Height / 8 };

        PdfEditResult result = Edit(fixture, input, new RedactAreaOp { Page = 1, Rect = strip });

        BoundedOperationOutcome outcome = Assert.Single(result.Applied);
        Assert.Equal(0, outcome.ItemsAffected);
        Warning warning = Assert.Single(result.Warnings!, static warning => warning.Code.Name == "OP_NO_EFFECT");
        Assert.Equal("no-match", warning.Details!["reason"]!.GetValue<string>());
        Assert.Contains("no character has its middle inside the area", warning.Message, StringComparison.Ordinal);
        Assert.Single(PdfSearch.Run(fixture.Session, new PdfSearchRequest
        {
            Input = result.Output!.Path,
            Query = new Aspose.Cli.Sdk.Text.SearchQuery(Aspose.Cli.Sdk.Text.TextSearch.Create("Portable", false, caseSensitive: true), 10, Scope: null),
        }).Hits);
    }

    [Fact]
    public void Encrypt_AgainWithTheSamePasswordsAndPermissionsChangesNothing()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument(pages: 1);
        var encrypt = new EncryptDocumentOp { UserPasswordEnv = "USER_PW", OwnerPasswordEnv = "OWNER_PW" };

        PdfEditResult result = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch { Ops = [encrypt, encrypt with { }] },
            Output = TestOutput.At(fixture.File("encrypted.pdf")),
            OpSecrets = new Dictionary<string, Secret> { ["USER_PW"] = new("user"), ["OWNER_PW"] = new("owner") },
        });

        Assert.Equal([1L, 0L], result.Applied.Select(static outcome => outcome.ItemsAffected));
        Assert.Equal(["security/encryption"], result.Applied[0].Targets);
    }

    [Fact]
    public void Encrypt_AgainWithOtherPermissionsChangesTheEncryption()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument(pages: 1);
        var encrypt = new EncryptDocumentOp { UserPasswordEnv = "USER_PW", OwnerPasswordEnv = "OWNER_PW" };

        PdfEditResult result = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch { Ops = [encrypt, encrypt with { Permissions = new PdfPermissionsInput { Print = true } }] },
            Output = TestOutput.At(fixture.File("permissions.pdf")),
            OpSecrets = new Dictionary<string, Secret> { ["USER_PW"] = new("user"), ["OWNER_PW"] = new("owner") },
        });

        Assert.Equal([1L, 1L], result.Applied.Select(static outcome => outcome.ItemsAffected));
        Assert.Null(result.Warnings?.FirstOrDefault(static warning => warning.Code.Name == "OP_NO_EFFECT"));
    }

    /// <summary>
    /// Recompressing a JPEG image at another quality keeps its size and compression; the image
    /// data it re-encodes is what changed.
    /// </summary>
    [Fact]
    public void Optimize_CountsAJpegImageItRecompresses()
    {
        using var fixture = new PdfEngineFixture();
        string bitmap = fixture.File("photo.bmp");
        File.WriteAllBytes(bitmap, PdfMutateTests.Bitmap(600, 600));
        string jpeg = fixture.File("photo.jpg");
        // A page the image fills, laid out by saving it, renders to a JPEG of the image alone.
        using (var drawn = new Aspose.Pdf.Document())
        {
            Aspose.Pdf.Page canvas = drawn.Pages.Add();
            canvas.SetPageSize(300, 300);
            canvas.PageInfo.Margin = new Aspose.Pdf.MarginInfo(0, 0, 0, 0);
            canvas.Paragraphs.Add(new Aspose.Pdf.Image { File = bitmap, FixWidth = 300, FixHeight = 300 });
            using var laid = new MemoryStream();
            drawn.Save(laid);
            using var rendered = new Aspose.Pdf.Document(laid);
            using FileStream stream = File.Create(jpeg);
            new Aspose.Pdf.Devices.JpegDevice(new Aspose.Pdf.Devices.Resolution(150), 95).Process(rendered.Pages[1], stream);
        }

        string input = fixture.File("photo.pdf");
        using (var document = new Aspose.Pdf.Document())
        {
            document.Pages.Add().Paragraphs.Add(new Aspose.Pdf.Image { File = jpeg, FixWidth = 500, FixHeight = 500 });
            document.Save(input);
        }

        PdfEditResult result = Edit(fixture, input, new OptimizeDocumentOp { ImageQuality = 20, CompressStreams = false, RemoveUnusedObjects = false });

        using (var output = new Aspose.Pdf.Document(result.Output!.Path))
        {
            // Still a JPEG of the same size: only its data tells the change.
            Aspose.Pdf.XImage image = Assert.Single(output.Pages[1].Resources.Images);
            Assert.Equal(Aspose.Pdf.ImageFilterType.Jpeg, image.FilterType);
        }

        BoundedOperationOutcome outcome = Assert.Single(result.Applied);
        Assert.True(outcome.ItemsAffected > 0, "recompressing the JPEG image changed no object");
        Assert.Contains("page/1/image/1", outcome.Targets);
        Assert.Null(result.Warnings?.FirstOrDefault(static warning => warning.Code.Name == "OP_NO_EFFECT"));
    }
}
