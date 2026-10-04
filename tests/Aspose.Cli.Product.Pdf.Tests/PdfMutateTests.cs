using System.Text;
using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Pdf;
using Aspose.Pdf.Annotations;
using Aspose.Pdf.Forms;
using Aspose.Pdf.Text;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

public sealed class PdfMutateTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(180)]
    [InlineData(270)]
    public void CroppedPageCoordinates_SearchAndRedactionAgreeAfterRotation(int angle)
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File($"coordinates-{angle}.pdf");
        using (var document = new Document())
        {
            Page page = document.Pages.Add();
            page.CropBox = new Rectangle(20, 30, 400, 600);
            page.Rotate = angle switch { 90 => Rotation.on90, 180 => Rotation.on180, 270 => Rotation.on270, _ => Rotation.None };
            var builder = new TextBuilder(page);
            builder.AppendText(new TextFragment("SECRET") { Position = new Position(80, 500) });
            builder.AppendText(new TextFragment("PUBLIC") { Position = new Position(80, 300) });
            document.Save(input);
        }
        PdfRect rect = Assert.Single(fixture.Engine.Search(input,
            Find("SECRET")).Hits).Rect;
        double width = angle is 90 or 270 ? 570 : 380;
        double height = angle is 90 or 270 ? 380 : 570;
        PdfPageInfo geometry = Assert.Single(fixture.Engine.GetInfo(input, new PdfInfoRequest { IncludePreview = true }).Pages!);
        Assert.Equal(width, geometry.WidthPoints);
        Assert.Equal(height, geometry.HeightPoints);
        Assert.InRange(rect.X, 0, width - rect.Width);
        Assert.InRange(rect.Y, 0, height - rect.Height);
        string output = fixture.File($"coordinates-{angle}.out.pdf");
        fixture.Engine.ApplyOps(input, new PdfOpsBatch
        {
            Ops = [new RedactAreaOp
            {
                Page = 1,
                Rect = new PdfRectInput { X = rect.X - 1, Y = rect.Y - 1, Width = rect.Width + 2, Height = rect.Height + 2 },
            }],
        }, new PdfEditRequest { OutputPath = output });
        string text = fixture.Engine.Read(output, new PdfReadRequest()).Pages[0].Text;
        Assert.DoesNotContain("SECRET", text, StringComparison.Ordinal);
        Assert.Contains("PUBLIC", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("(?<=SECRET: )1234", true)]
    [InlineData("SECRET: 1234", false)]
    public void SearchAndRedaction_PreserveContextAcrossTextSegments(string pattern, bool regex)
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("context.pdf");
        using (var document = new Document())
        {
            var text = new TextFragment();
            text.Segments.Add(new TextSegment("SECRET: "));
            text.Segments.Add(new TextSegment("1234") { TextState = { FontStyle = FontStyles.Bold } });
            text.Segments.Add(new TextSegment(" PUBLIC: 1234"));
            document.Pages.Add().Paragraphs.Add(text);
            document.Save(input);
        }
        Assert.Single(fixture.Engine.Search(input, Find(pattern, regex)).Hits);
        string output = fixture.File("context.out.pdf");
        PdfEditResult edited = fixture.Engine.ApplyOps(input,
            new PdfOpsBatch { Ops = [new RedactTextOp { Pattern = pattern, Regex = regex }] },
            new PdfEditRequest { OutputPath = output });
        Assert.Equal(1, Assert.Single(edited.Applied).ItemsAffected);
        Assert.Contains("PUBLIC: 1234", fixture.Engine.Read(output, new PdfReadRequest()).Pages[0].Text, StringComparison.Ordinal);
        Assert.Empty(fixture.Engine.Search(output, Find(pattern, regex)).Hits);
    }
    [Fact]
    public void Edit_VisualContentAndRedactionPersist()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("visual.pdf", pages: 2);
        string image = fixture.File("pixel.png");
        File.WriteAllBytes(image, PixelPng());
        string output = fixture.File("visual.out.pdf");

        PdfEditResult result = fixture.Engine.ApplyOps(input, new PdfOpsBatch
        {
            Ops =
            [
                new AddWatermarkTextOp { Text = "DRAFT", Pages = "1" },
                new AddWatermarkImageOp { Path = image, Pages = "2", Scale = 0.1 },
                new AddPageNumbersOp { Format = "{n}/{N}" },
                new AddHeaderTextOp { Text = "Header" },
                new AddFooterTextOp { Text = "Footer" },
                new AddStampImageOp
                {
                    Page = 1,
                    Path = image,
                    Rect = new PdfRectInput { X = 20, Y = 20, Width = 20, Height = 20 },
                },
                new AddLinkOp
                {
                    Page = 1,
                    Url = "https://example.com",
                    Rect = new PdfRectInput { X = 20, Y = 60, Width = 120, Height = 20 },
                },
                new RedactTextOp { Pattern = "Portable", Pages = "1" },
                new RedactAreaOp
                {
                    Page = 2,
                    Rect = new PdfRectInput { X = 20, Y = 100, Width = 40, Height = 20 },
                },
            ],
        }, new PdfEditRequest { OutputPath = output });

        Assert.NotNull(result.Input.Fingerprint);
        Assert.NotNull(result.Output?.Fingerprint);
        Assert.All(result.Applied, static operation =>
            Assert.Equal(OpStatuses.Ok, operation.Status));
        Assert.All(result.Applied, static operation => Assert.NotEmpty(operation.Targets));
        Assert.Equal(9, result.Applied.Count);
        using var reopened = new Document(output);
        Assert.Contains(reopened.Pages[1].Annotations, static annotation => annotation is LinkAnnotation);
        string firstPageText = PageText(reopened.Pages[1]);
        Assert.DoesNotContain("Portable", firstPageText, StringComparison.Ordinal);
        Assert.Contains("DRAFT", firstPageText, StringComparison.Ordinal);
        Assert.Contains("Header", firstPageText, StringComparison.Ordinal);
        Assert.Contains("Footer", firstPageText, StringComparison.Ordinal);
        Assert.Contains("1/2", firstPageText, StringComparison.Ordinal);
        Assert.NotEmpty(reopened.Pages[1].Resources.Images);
        Assert.NotEmpty(reopened.Pages[2].Resources.Images);
        Assert.DoesNotContain(
            "Portable",
            Encoding.ASCII.GetString(File.ReadAllBytes(output)),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void PageLabels_UseTheRequestedOneBasedStartPage(int startPage)
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("labels.pdf", pages: 3);
        string output = fixture.File("labels.out.pdf");
        fixture.Engine.ApplyOps(input, new PdfOpsBatch
        {
            Ops = [new SetPageLabelsOp
            {
                Ranges = [new PdfPageLabelRange { StartPage = startPage, Prefix = "R-", StartingValue = 7 }],
            }],
        }, new PdfEditRequest { OutputPath = output });

        PdfInfoResult info = fixture.Engine.GetInfo(output, new PdfInfoRequest());
        PdfPageLabelInfo label = Assert.Single(info.PageLabels!, item => item.Prefix == "R-");
        Assert.Equal(startPage, label.StartPage);
        Assert.Equal(7, label.StartingValue);
    }

    [Fact]
    public void Edit_DocumentOperationsAndRemovalRoundTrip()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("document.pdf", pages: 2);
        string namedAttachment = fixture.File("source.txt");
        File.WriteAllText(namedAttachment, "source bytes");
        string defaultNamedAttachment = fixture.File("supporting-notes.txt");
        File.WriteAllText(defaultNamedAttachment, "supporting notes");
        const string logicalName = "customer-evidence.txt";
        string first = fixture.File("document.first.pdf");
        string final = fixture.File("document.final.pdf");

        PdfEditResult firstEdit = fixture.Engine.ApplyOps(input, new PdfOpsBatch
        {
            Ops =
            [
                new SetMetadataOp
                {
                    Title = "Report",
                    Author = "CLI",
                    Custom = new Dictionary<string, string> { ["Department"] = "Finance" },
                },
                new AddBookmarkOp { Title = "Overview", Page = 1 },
                new AddAttachmentOp { Path = namedAttachment, Name = logicalName },
                new AddAttachmentOp { Path = defaultNamedAttachment },
                new SetPageLabelsOp
                {
                    Ranges = [new PdfPageLabelRange { StartPage = 1, Style = "roman-lower", Prefix = "A-" }],
                },
            ],
        }, new PdfEditRequest { OutputPath = first });

        using (var reopened = new Document(first))
        {
            Assert.Equal("Report", reopened.Info.Title);
            Assert.Single(reopened.Outlines);
            FileSpecification namedSpecification = Assert.IsType<FileSpecification>(
                reopened.EmbeddedFiles.FindByName(logicalName));
            Assert.Equal(logicalName, namedSpecification.Name);
            Assert.Equal(logicalName, namedSpecification.UnicodeName);

            FileSpecification defaultNamedSpecification = Assert.IsType<FileSpecification>(
                reopened.EmbeddedFiles.FindByName(Path.GetFileName(defaultNamedAttachment)));
            Assert.Equal(Path.GetFileName(defaultNamedAttachment), defaultNamedSpecification.Name);
            Assert.Equal(Path.GetFileName(defaultNamedAttachment), defaultNamedSpecification.UnicodeName);
            Assert.Equal("A-", reopened.PageLabels.GetLabel(0).Prefix);
            Assert.All(firstEdit.Applied, outcome => Assert.Equal(OpStatuses.Ok, outcome.Status));
        }

        PdfInfoResult info = fixture.Engine.GetInfo(first, new PdfInfoRequest { Details = ["attachments"] });
        string[] attachmentNames = info.Attachments!.Select(static item => item.Name).ToArray();
        Assert.Equal([logicalName, Path.GetFileName(defaultNamedAttachment)], attachmentNames);
        Assert.DoesNotContain(namedAttachment, attachmentNames);
        Assert.DoesNotContain(defaultNamedAttachment, attachmentNames);
        Assert.All(attachmentNames, static name => Assert.False(Path.IsPathFullyQualified(name)));

        fixture.Engine.ApplyOps(first, new PdfOpsBatch
        {
            Ops =
            [
                new RemoveMetadataOp(),
                new DeleteBookmarksOp { All = true },
                new RemoveAttachmentOp { Name = logicalName },
                new RemoveAttachmentOp { Name = Path.GetFileName(defaultNamedAttachment) },
            ],
        }, new PdfEditRequest { OutputPath = final });

        using var cleaned = new Document(final);
        Assert.Empty(cleaned.Outlines);
        Assert.Empty(cleaned.EmbeddedFiles);
        Assert.True(string.IsNullOrEmpty(cleaned.Info.Title));
    }

    [Fact]
    public void Forms_ReadSetExportAndFlatten()
    {
        using var fixture = new PdfEngineFixture();
        string input = FormDocument(fixture);
        PdfFormResult read = fixture.Engine.ReadForm(input, new PdfFormReadRequest());
        Assert.Equal("acro", read.Type);
        Assert.Contains(read.Fields, static field => field.Name == "Customer");

        string filled = fixture.File("form.filled.pdf");
        PdfEditResult filledResult = fixture.Engine.ApplyOps(input, new PdfOpsBatch
        {
            Ops = [new SetFormFieldOp { Name = "Customer", Value = "Contoso" }],
        }, new PdfEditRequest { OutputPath = filled });
        Assert.Equal(["pdf/form"], Assert.Single(filledResult.Applied).Targets);
        Assert.NotNull(filledResult.Input.Fingerprint);
        Assert.NotNull(filledResult.Output?.Fingerprint);
        Assert.Equal("Contoso", fixture.Engine.ReadForm(filled, new PdfFormReadRequest()).Fields.Single().Value);

        PdfFormExportResult exported = fixture.Engine.ExportForm(filled, new PdfFormExportRequest
        {
            TargetFormatId = "json",
            OutputPath = fixture.File("form.json"),
        });
        Assert.True(exported.Output.SizeBytes > 0);

        string flattened = fixture.File("form.flattened.pdf");
        PdfEditResult flattenResult = fixture.Engine.ApplyOps(filled, new PdfOpsBatch
        {
            Ops =
            [
                new SetFormFieldOp { Name = "Customer", Value = "Northwind" },
                new FlattenFormsOp(),
            ],
        }, new PdfEditRequest { OutputPath = flattened });
        Assert.All(flattenResult.Applied, static item => Assert.Equal(["pdf/form"], item.Targets));
        using var reopened = new Document(flattened);
        Assert.Empty(reopened.Form.Fields);
        Assert.Contains("Northwind", PageText(reopened.Pages[1]), StringComparison.Ordinal);
    }

    [Fact]
    public void Forms_XfaMutationFailsWithTheDedicatedPublicError()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("xfa.pdf");
        using (var document = new Document())
        {
            Page page = document.Pages.Add();
            document.Form.Add(new TextBoxField(page, new Rectangle(72, 700, 200, 730))
            {
                PartialName = "seed",
            });
            var xfa = new System.Xml.XmlDocument();
            xfa.LoadXml(
                """
                <xdp:xdp xmlns:xdp="http://ns.adobe.com/xdp/">
                  <template xmlns="http://www.xfa.org/schema/xfa-template/3.3/"><subform name="form1"/></template>
                  <datasets xmlns="http://www.xfa.org/schema/xfa-data/1.0/">
                    <xfa:data xmlns:xfa="http://www.xfa.org/schema/xfa-data/1.0/"/>
                  </datasets>
                </xdp:xdp>
                """);
            document.Form.AssignXfa(xfa);
            document.Save(input);
        }

        string output = fixture.File("xfa.out.pdf");
        CliException exception = Assert.Throws<CliException>(() => fixture.Engine.ApplyOps(
            input,
            new PdfOpsBatch { Ops = [new SetFormFieldOp { Name = "seed", Value = "value" }] },
            new PdfEditRequest { OutputPath = output }));

        Assert.Equal("FORM_XFA_UNSUPPORTED", exception.Code.Name);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void Edit_EncryptAndDecryptUseOutOfBandSecrets()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("plain.pdf", pages: 1);
        string encrypted = fixture.File("encrypted.pdf");
        var secrets = new Dictionary<string, string>
        {
            ["PDF_USER"] = "reader",
            ["PDF_OWNER"] = "owner",
        };

        fixture.Engine.ApplyOps(input, new PdfOpsBatch
        {
            Ops =
            [
                new EncryptPdfOp
                {
                    UserPasswordEnv = "PDF_USER",
                    OwnerPasswordEnv = "PDF_OWNER",
                    Permissions = new PdfPermissionsInput { Print = true },
                },
            ],
        }, new PdfEditRequest { OutputPath = encrypted, OpSecrets = secrets });

        Assert.Throws<InvalidPasswordException>(() => new Document(encrypted));
        using (var opened = new Document(encrypted, "reader"))
        {
            Assert.True(opened.IsEncrypted);
        }
        PdfInfoResult encryptedInfo = fixture.Engine.GetInfo(encrypted, new PdfInfoRequest
        {
            Password = "reader",
            Details = ["permissions"],
        });
        Assert.True(encryptedInfo.Permissions?.Print);
        Assert.False(encryptedInfo.Permissions?.Copy);

        string plain = fixture.File("decrypted.pdf");
        fixture.Engine.ApplyOps(encrypted, new PdfOpsBatch { Ops = [new DecryptPdfOp()] }, new PdfEditRequest
        {
            OutputPath = plain,
            Password = "owner",
        });
        using var decrypted = new Document(plain);
        Assert.False(decrypted.IsEncrypted);
    }

    [Theory]
    [InlineData("reader", Permissions.PrintDocument | Permissions.FillForm, "metadata", "keeps the input's encryption")]
    [InlineData("reader", Permissions.PrintDocument | Permissions.FillForm, "field", null)]
    [InlineData("reader", Permissions.PrintDocument | Permissions.ModifyTextAnnotations, "field", null)]
    [InlineData("reader", Permissions.PrintDocument, "field", "keeps the input's encryption")]
    [InlineData("reader", Permissions.PrintDocument | Permissions.AssembleDocument, "rotate", null)]
    [InlineData("reader", Permissions.PrintDocument | Permissions.AssembleDocument, "metadata", "keeps the input's encryption")]
    [InlineData("reader", Permissions.PrintDocument | Permissions.ModifyTextAnnotations, "rotate", "keeps the input's encryption")]
    [InlineData("reader", Permissions.PrintDocument | Permissions.FillForm, "decrypt", "is not encrypted")]
    [InlineData("reader", Permissions.PrintDocument | Permissions.FillForm, "encrypt", "encrypt operation set")]
    [InlineData("reader", Permissions.PrintDocument | Permissions.ModifyContent, "metadata", null)]
    [InlineData("reader", Permissions.PrintDocument | Permissions.ModifyContent, "decrypt", "is not encrypted")]
    [InlineData("owner", Permissions.PrintDocument | Permissions.FillForm, "metadata", null)]
    [InlineData("owner", Permissions.PrintDocument, "decrypt", null)]
    public void Edit_DisclosesAModificationItsUserPermissionsForbid(
        string password, Permissions granted, string change, string? hint)
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("restricted.pdf");
        using (var document = new Document())
        {
            Page page = document.Pages.Add();
            document.Form.Add(new TextBoxField(page, new Rectangle(72, 650, 280, 680)) { PartialName = "Customer" });
            document.Encrypt("reader", "owner", granted, CryptoAlgorithm.AESx256);
            document.Save(input);
        }

        string output = fixture.File("restricted.out.pdf");
        PdfEditResult result = fixture.Engine.ApplyOps(input, new PdfOpsBatch
        {
            Ops =
            [
                change switch
                {
                    "metadata" => new SetMetadataOp { Title = "Changed" },
                    "field" => new SetFormFieldOp { Name = "Customer", Value = "Contoso" },
                    "rotate" => new RotatePagesOp { Pages = "1", Angle = 90 },
                    "decrypt" => new DecryptPdfOp(),
                    _ => new EncryptPdfOp
                    {
                        UserPasswordEnv = "PDF_USER",
                        OwnerPasswordEnv = "PDF_OWNER",
                        Permissions = new PdfPermissionsInput { Print = true, Modify = true },
                    },
                },
            ],
        }, new PdfEditRequest
        {
            OutputPath = output,
            Password = password,
            OpSecrets = new Dictionary<string, string> { ["PDF_USER"] = "new-reader", ["PDF_OWNER"] = "new-owner" },
        });

        Warning? warning = result.Warnings?.SingleOrDefault(static item => item.Code == WarningCodes.ProtectionNotEnforced);
        Assert.Equal(hint is not null, warning is not null);
        if (warning is not null)
        {
            Assert.Contains("user password", warning.Message, StringComparison.Ordinal);
            Assert.Contains("changed it", warning.Message, StringComparison.Ordinal);
            Assert.Contains(hint!, warning.Hint, StringComparison.Ordinal);
        }

        if (change == "decrypt")
        {
            using var decrypted = new Document(output);
            Assert.False(decrypted.IsEncrypted);
        }
    }

    [Fact]
    public void Edit_DryRunDisclosesTheUnpermittedChangeItWouldMake()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("restricted.pdf");
        using (var document = new Document())
        {
            document.Pages.Add();
            document.Encrypt("reader", "owner", Permissions.PrintDocument, CryptoAlgorithm.AESx256);
            document.Save(input);
        }

        string output = fixture.File("restricted.out.pdf");
        PdfEditResult result = fixture.Engine.ApplyOps(input, new PdfOpsBatch
        {
            Ops = [new SetMetadataOp { Title = "Changed" }, new DecryptPdfOp()],
        }, new PdfEditRequest
        {
            OutputPath = output,
            Password = "reader",
            Options = new EditCommandOptions { DryRun = true },
        });

        Warning warning = Assert.Single(result.Warnings!, static item => item.Code == WarningCodes.ProtectionNotEnforced);
        Assert.Contains("do not allow set_metadata, decrypt;", warning.Message, StringComparison.Ordinal);
        Assert.Contains("would change it", warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("changed it", warning.Message, StringComparison.Ordinal);
        Assert.Contains("would not be encrypted", warning.Hint, StringComparison.Ordinal);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void Edit_DryRunAtomicFailureAndContinueHaveExplicitOutcomes()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("atomic.pdf", pages: 1);
        string output = fixture.File("atomic.out.pdf");
        var batch = new PdfOpsBatch
        {
            Ops =
            [
                new SetMetadataOp { Title = "Changed" },
                new RemoveAttachmentOp { Name = "missing.bin" },
            ],
        };

        Assert.ThrowsAny<Exception>(() => fixture.Engine.ApplyOps(
            input,
            batch,
            new PdfEditRequest { OutputPath = output }));
        Assert.False(File.Exists(output));

        PdfEditResult partial = fixture.Engine.ApplyOps(input, batch, new PdfEditRequest
        {
            OutputPath = output,
            Options = new EditCommandOptions { BestEffort = true },
        });
        Assert.True(partial.HasFailures);
        Assert.Equal([OpStatuses.Ok, OpStatuses.Failed], partial.Applied.Select(static item => item.Status));
        Assert.Equal(["pdf/metadata"], partial.Applied[0].Targets);
        Assert.Equal(["pdf/attachment"], partial.Applied[1].Targets);
        Assert.Equal("ATTACHMENT_NOT_FOUND", partial.Applied[1].Error!.Code);
        Assert.Equal("missing.bin", partial.Applied[1].Error!.Details!["requested"]!.GetValue<string>());
        Assert.Equal(0, partial.Applied[1].Error!.Details!["availableCount"]!.GetValue<int>());
        using (var reopened = new Document(output))
        {
            Assert.Equal("Changed", reopened.Info.Title);
        }

        string dryOutput = fixture.File("dry.pdf");
        PdfEditResult dry = fixture.Engine.ApplyOps(input, new PdfOpsBatch
        {
            Ops = [new RotatePagesOp { Pages = "1", Angle = 90 }],
        }, new PdfEditRequest
        {
            OutputPath = dryOutput,
            Options = new EditCommandOptions { DryRun = true },
        });
        Assert.True(dry.DryRun);
        Assert.False(File.Exists(dryOutput));
    }

    [Fact]
    public void Edit_OptimizeReducesAHighResolutionImageDocument()
    {
        using var fixture = new PdfEngineFixture();
        string bitmap = fixture.File("large.bmp");
        File.WriteAllBytes(bitmap, Bitmap(900, 900));
        string input = fixture.File("large.pdf");
        using (var document = new Document())
        {
            Page page = document.Pages.Add();
            page.Paragraphs.Add(new Aspose.Pdf.Image
            {
                File = bitmap,
                FixWidth = 500,
                FixHeight = 500,
            });
            document.Save(input);
        }

        string output = fixture.File("optimized.pdf");
        fixture.Engine.ApplyOps(input, new PdfOpsBatch
        {
            Ops =
            [
                new OptimizePdfOp
                {
                    DownsampleImagesDpi = 72,
                    ImageQuality = 30,
                    CompressStreams = true,
                    RemoveUnusedObjects = true,
                },
            ],
        }, new PdfEditRequest { OutputPath = output });

        Assert.True(new FileInfo(output).Length < new FileInfo(input).Length);
    }

    [Fact]
    public void SearchValidateAndTableExtractionReturnBoundedSemanticResults()
    {
        using var fixture = new PdfEngineFixture();
        string input = TableDocument(fixture);
        PdfSearchResult search = fixture.Engine.Search(input, Find("Revenue", maxHits: 1));
        Assert.Single(search.Hits);
        Assert.True(search.Hits[0].Rect.Width > 0);

        PdfValidateResult validation = fixture.Engine.Validate(input, new PdfValidateRequest { Profile = "pdfa-2b" });
        Assert.Equal("pdfa-2b", validation.Profile);
        Assert.False(validation.Valid);
        Assert.NotEmpty(validation.Issues);

        PdfExtractResult tables = fixture.Engine.Extract(input, new PdfExtractRequest
        {
            What = "tables",
            OutputDirectory = fixture.File("tables"),
        });
        Assert.NotEmpty(tables.Items);
        Assert.All(tables.Items, static table =>
        {
            Assert.Equal("table", table.Kind);
            Assert.NotNull(table.Rect);
        });
        Assert.Contains(
            tables.Items,
            table => File.ReadAllText(table.Path).Contains("Revenue", StringComparison.Ordinal));
    }

    [Fact]
    public void SetFormField_RefusesACheckBoxStateThatWouldNotDisplay()
    {
        using var fixture = new PdfEngineFixture();
        string input = CheckBoxDocument(fixture);

        CliException error = Assert.Throws<CliException>(() => fixture.Engine.ApplyOps(
            input,
            new PdfOpsBatch { Ops = [new SetFormFieldOp { Name = "Approved", Value = "true" }] },
            new PdfEditRequest { OutputPath = fixture.File("checkbox.invalid.pdf") }));

        // Storing /true leaves the box drawn empty while a query reads back "true".
        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Contains("Off, Yes", error.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(fixture.File("checkbox.invalid.pdf")));
    }

    [Fact]
    public void SetFormField_UnknownNameListsTheFieldsAndSuggestsTheClosest()
    {
        using var fixture = new PdfEngineFixture();
        string input = FormDocument(fixture);

        CliException error = Assert.Throws<CliException>(() => fixture.Engine.ApplyOps(
            input,
            new PdfOpsBatch { Ops = [new SetFormFieldOp { Name = "customer", Value = "Contoso" }] },
            new PdfEditRequest { OutputPath = fixture.File("form.missing.pdf") }));

        Assert.Equal("FIELD_NOT_FOUND", error.Code.Name);
        Assert.Equal("customer", error.Details!["requested"]!.GetValue<string>());
        Assert.Equal(["Customer"], error.Details["available"]!.AsArray().Select(static name => name!.GetValue<string>()));
        Assert.Equal("Customer", error.Details["suggestions"]![0]!.GetValue<string>());
        Assert.Equal(0, error.Details["index"]!.GetValue<int>());
    }

    [Fact]
    public void Bookmarks_IndexesPastALevelReportThatLevel()
    {
        using var fixture = new PdfEngineFixture();
        string input = Outlined(fixture, "levels.pdf", static document =>
        {
            OutlineItemCollection intro = Bookmark(document, "Intro", 1);
            intro.Add(Bookmark(document, "Scope", 1));
            intro.Add(Bookmark(document, "Terms", 2));
            document.Outlines.Add(intro);
            document.Outlines.Add(Bookmark(document, "Results", 2));
            document.Outlines.Add(Bookmark(document, "Appendix", 3));
        });

        PdfEditResult result = fixture.Engine.ApplyOps(input, new PdfOpsBatch
        {
            Ops =
            [
                new DeleteBookmarksOp { Indexes = ["9"] },
                new DeleteBookmarksOp { Indexes = ["1/5"] },
                new DeleteBookmarksOp { Indexes = ["9/1"] },
                new AddBookmarkOp { Title = "Detail", Page = 1, Parent = "2/1" },
            ],
        }, new PdfEditRequest
        {
            OutputPath = fixture.File("levels.failed.pdf"),
            Options = new EditCommandOptions { BestEffort = true },
        });

        Assert.Equal(
            [
                "9 3 Use a top-level bookmark from 1 through 3.",
                "1/5 2 Bookmark 1 has 2 child bookmarks; use 1/1 through 1/2.",
                "9/1 3 Use a top-level bookmark from 1 through 3.",
                "2/1 0 Bookmark 2 has no child bookmarks.",
            ],
            result.Applied.Select(static applied =>
            {
                OpError error = applied.Error!;
                Assert.Equal("BOOKMARK_NOT_FOUND", error.Code);
                return $"{error.Details!["requested"]!.GetValue<string>()} "
                    + $"{error.Details["availableCount"]!.GetValue<int>()} {error.Hint}";
            }));
    }

    [Fact]
    public void Bookmarks_ReadIndexesAndPagesAddressEdits()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("indexes.pdf", pages: 2);
        string outlined = fixture.File("indexes.out.pdf");
        fixture.Engine.ApplyOps(input, new PdfOpsBatch
        {
            Ops =
            [
                new AddBookmarkOp { Title = "Intro", Page = 1 },
                new AddBookmarkOp { Title = "Scope", Page = 2, Parent = "1" },
            ],
        }, new PdfEditRequest { OutputPath = outlined });

        PdfOutlineItem[] outline = OutlineOf(fixture, outlined);
        Assert.Equal(["1 Intro 1", "1/1 Scope 2"], outline.Select(static item => $"{item.Index} {item.Title} {item.Page}"));

        string edited = fixture.File("indexes.edited.pdf");
        fixture.Engine.ApplyOps(outlined, new PdfOpsBatch
        {
            Ops =
            [
                new DeleteBookmarksOp { Indexes = [outline[1].Index] },
                new AddBookmarkOp { Title = "Detail", Page = outline[1].Page!.Value, Parent = outline[0].Index },
            ],
        }, new PdfEditRequest { OutputPath = edited });

        Assert.Equal(
            ["1 Intro 1", "1/1 Detail 2"],
            OutlineOf(fixture, edited).Select(static item => $"{item.Index} {item.Title} {item.Page}"));
    }

    [Fact]
    public void Bookmarks_ATitleWithASlashIsDeletedWithoutTouchingTheNestedLookalike()
    {
        using var fixture = new PdfEngineFixture();
        string input = Outlined(fixture, "slash.pdf", static document =>
        {
            OutlineItemCollection a = Bookmark(document, "A", 1);
            a.Add(Bookmark(document, "B", 2));
            document.Outlines.Add(a);
            document.Outlines.Add(Bookmark(document, "A/B", 3));
        });
        PdfOutlineItem slashed = Assert.Single(OutlineOf(fixture, input), static item => item.Title == "A/B");
        Assert.Equal("2", slashed.Index);

        string edited = fixture.File("slash.edited.pdf");
        fixture.Engine.ApplyOps(input, new PdfOpsBatch
        {
            Ops = [new DeleteBookmarksOp { Indexes = [slashed.Index] }],
        }, new PdfEditRequest { OutputPath = edited });

        Assert.Equal(
            ["1 A 1", "1/1 B 2"],
            OutlineOf(fixture, edited).Select(static item => $"{item.Index} {item.Title} {item.Page}"));
    }

    [Fact]
    public void Bookmarks_SameTitledSiblingsAreEditedOneAtATime()
    {
        using var fixture = new PdfEngineFixture();
        string input = Outlined(fixture, "twins.pdf", static document =>
        {
            document.Outlines.Add(Bookmark(document, "Results", 1));
            document.Outlines.Add(Bookmark(document, "Results", 2));
        });
        PdfOutlineItem[] outline = OutlineOf(fixture, input);

        string edited = fixture.File("twins.edited.pdf");
        fixture.Engine.ApplyOps(input, new PdfOpsBatch
        {
            Ops =
            [
                new DeleteBookmarksOp { Indexes = [outline[1].Index] },
                new AddBookmarkOp { Title = "Detail", Page = 3, Parent = outline[0].Index },
            ],
        }, new PdfEditRequest { OutputPath = edited });

        Assert.Equal(
            ["1 Results 1", "1/1 Detail 3"],
            OutlineOf(fixture, edited).Select(static item => $"{item.Index} {item.Title} {item.Page}"));
    }

    [Fact]
    public void Bookmarks_AnEmptyTitleIsAddressable()
    {
        using var fixture = new PdfEngineFixture();
        string input = Outlined(fixture, "untitled.pdf", static document =>
        {
            document.Outlines.Add(Bookmark(document, string.Empty, 1));
            document.Outlines.Add(Bookmark(document, "Removed", 2));
        });
        PdfOutlineItem[] outline = OutlineOf(fixture, input);
        Assert.Equal(string.Empty, outline[0].Title);

        string edited = fixture.File("untitled.edited.pdf");
        fixture.Engine.ApplyOps(input, new PdfOpsBatch
        {
            Ops =
            [
                new AddBookmarkOp { Title = "Child", Page = 3, Parent = outline[0].Index },
                new DeleteBookmarksOp { Indexes = [outline[1].Index] },
            ],
        }, new PdfEditRequest { OutputPath = edited });

        Assert.Equal(
            ["1  1", "1/1 Child 3"],
            OutlineOf(fixture, edited).Select(static item => $"{item.Index} {item.Title} {item.Page}"));
    }

    [Fact]
    public void Bookmarks_ADeletionRenumbersLaterSiblingsForTheRestOfTheBatch()
    {
        using var fixture = new PdfEngineFixture();
        string input = Outlined(fixture, "renumber.pdf", static document =>
        {
            document.Outlines.Add(Bookmark(document, "First", 1));
            document.Outlines.Add(Bookmark(document, "Second", 2));
            document.Outlines.Add(Bookmark(document, "Third", 3));
        });

        string edited = fixture.File("renumber.edited.pdf");
        fixture.Engine.ApplyOps(input, new PdfOpsBatch
        {
            Ops = [new DeleteBookmarksOp { Indexes = ["1"] }, new DeleteBookmarksOp { Indexes = ["1"] }],
        }, new PdfEditRequest { OutputPath = edited });

        Assert.Equal(
            ["1 Third 3"],
            OutlineOf(fixture, edited).Select(static item => $"{item.Index} {item.Title} {item.Page}"));
    }

    [Fact]
    public void Bookmarks_DeleteRemovesOnlyTheSelectedBookmarkOfASharedTitle()
    {
        using var fixture = new PdfEngineFixture();
        string input = Outlined(fixture, "shared.pdf", static document =>
        {
            OutlineItemCollection parent = Bookmark(document, "Parent", 1);
            parent.Add(Bookmark(document, "Results", 2));
            document.Outlines.Add(parent);
            document.Outlines.Add(Bookmark(document, "Results", 3));
            document.Outlines.Add(Bookmark(document, "Tail", 1));
        });

        string edited = fixture.File("shared.edited.pdf");
        fixture.Engine.ApplyOps(input, new PdfOpsBatch
        {
            Ops = [new DeleteBookmarksOp { Indexes = ["2"] }],
        }, new PdfEditRequest { OutputPath = edited });

        Assert.Equal(
            ["1 Parent 1", "1/1 Results 2", "2 Tail 1"],
            OutlineOf(fixture, edited).Select(static item => $"{item.Index} {item.Title} {item.Page}"));
    }

    [Fact]
    public void Bookmarks_OneOperationDeletesSameTitledSiblingsAndKeepsTheirNamesake()
    {
        using var fixture = new PdfEngineFixture();
        string input = Outlined(fixture, "siblings.pdf", static document =>
        {
            document.Outlines.Add(Bookmark(document, "Results", 1));
            document.Outlines.Add(Bookmark(document, "Results", 2));
            document.Outlines.Add(Bookmark(document, "Results", 3));
        });

        string edited = fixture.File("siblings.edited.pdf");
        fixture.Engine.ApplyOps(input, new PdfOpsBatch
        {
            Ops = [new DeleteBookmarksOp { Indexes = ["1", "2"] }],
        }, new PdfEditRequest { OutputPath = edited });

        Assert.Equal(
            ["1 Results 3"],
            OutlineOf(fixture, edited).Select(static item => $"{item.Index} {item.Title} {item.Page}"));
    }

    [Fact]
    public void Bookmarks_OneOperationDeletesChildrenOfDifferentParents()
    {
        using var fixture = new PdfEngineFixture();
        string input = ListedOutline(fixture, "children.pdf");

        string edited = fixture.File("children.edited.pdf");
        fixture.Engine.ApplyOps(input, new PdfOpsBatch
        {
            Ops = [new DeleteBookmarksOp { Indexes = ["4/1", "2/1"] }],
        }, new PdfEditRequest { OutputPath = edited });

        Assert.Equal(
            ["1 First 1", "2 Second 2", "3 Third 3", "4 Fourth 1", "4/1 Fourth.B 3"],
            OutlineOf(fixture, edited).Select(static item => $"{item.Index} {item.Title} {item.Page}"));
    }

    [Fact]
    public void Bookmarks_DeleteRemovesTheChildrenButNotTheirNamesakes()
    {
        using var fixture = new PdfEngineFixture();
        string input = Outlined(fixture, "subtree.pdf", static document =>
        {
            OutlineItemCollection parent = Bookmark(document, "Parent", 1);
            parent.Add(Bookmark(document, "Results", 2));
            document.Outlines.Add(parent);
            document.Outlines.Add(Bookmark(document, "Results", 3));
        });

        string edited = fixture.File("subtree.edited.pdf");
        fixture.Engine.ApplyOps(input, new PdfOpsBatch
        {
            Ops = [new DeleteBookmarksOp { Indexes = ["1"] }],
        }, new PdfEditRequest { OutputPath = edited });

        Assert.Equal(
            ["1 Results 3"],
            OutlineOf(fixture, edited).Select(static item => $"{item.Index} {item.Title} {item.Page}"));
    }

    /// <summary>
    /// Indexes read from one inspect name the bookmarks they named there, in any order: the
    /// deletion of one listed bookmark does not shift another onto a later sibling.
    /// </summary>
    [Theory]
    [InlineData("2", "3")]
    [InlineData("3", "2")]
    public void Bookmarks_OneOperationDeletesEveryListedIndexAsInspected(string first, string second)
    {
        using var fixture = new PdfEngineFixture();
        string input = ListedOutline(fixture, "listed.pdf");

        string edited = fixture.File("listed.edited.pdf");
        PdfEditResult result = fixture.Engine.ApplyOps(input, new PdfOpsBatch
        {
            Ops = [new DeleteBookmarksOp { Indexes = [first, second] }],
        }, new PdfEditRequest { OutputPath = edited });

        // Second, its child and Third, each addressed as it was before the deletion.
        Assert.Equal(3, Assert.Single(result.Applied).ItemsAffected);
        Assert.Equal([$"pdf/bookmark/{first}", $"pdf/bookmark/{second}"], result.Applied[0].Targets);
        Assert.Equal(
            ["1 First 1", "2 Fourth 1", "2/1 Fourth.A 2", "2/2 Fourth.B 3"],
            OutlineOf(fixture, edited).Select(static item => $"{item.Index} {item.Title} {item.Page}"));
    }

    [Fact]
    public void Bookmarks_DeletingAllCountsEveryBookmark()
    {
        using var fixture = new PdfEngineFixture();
        string input = ListedOutline(fixture, "all.pdf");

        string edited = fixture.File("all.edited.pdf");
        PdfEditResult result = fixture.Engine.ApplyOps(input, new PdfOpsBatch
        {
            Ops = [new DeleteBookmarksOp { All = true }],
        }, new PdfEditRequest { OutputPath = edited });

        Assert.Equal(7, Assert.Single(result.Applied).ItemsAffected);
        Assert.Equal(["pdf/bookmark"], result.Applied[0].Targets);
        Assert.Empty(OutlineOf(fixture, edited));
    }

    [Fact]
    public void Bookmarks_AMissingIndexDeletesNoneOfTheList()
    {
        using var fixture = new PdfEngineFixture();
        string input = ListedOutline(fixture, "partial.pdf");

        CliException error = Assert.Throws<CliException>(() => fixture.Engine.ApplyOps(input, new PdfOpsBatch
        {
            Ops = [new DeleteBookmarksOp { Indexes = ["1", "2/5"] }],
        }, new PdfEditRequest { OutputPath = fixture.File("partial.failed.pdf") }));
        Assert.Equal("BOOKMARK_NOT_FOUND", error.Code.Name);
        Assert.Equal("2/5", error.Details!["requested"]!.GetValue<string>());

        string edited = fixture.File("partial.edited.pdf");
        PdfEditResult result = fixture.Engine.ApplyOps(input, new PdfOpsBatch
        {
            Ops =
            [
                new DeleteBookmarksOp { Indexes = ["1", "2/5"] },
                new AddBookmarkOp { Title = "Added", Page = 1, Parent = "1" },
            ],
        }, new PdfEditRequest
        {
            OutputPath = edited,
            Options = new EditCommandOptions { BestEffort = true },
        });

        Assert.Equal("BOOKMARK_NOT_FOUND", result.Applied[0].Error!.Code);
        Assert.Equal(["pdf/bookmark"], result.Applied[0].Targets);
        Assert.Equal(OpStatuses.Ok, result.Applied[1].Status);
        Assert.Equal(["pdf/bookmark/1/1"], result.Applied[1].Targets);
        Assert.Equal(
            ["1 First 1", "1/1 Added 1", "2 Second 2", "2/1 Second.A 2", "3 Third 3", "4 Fourth 1", "4/1 Fourth.A 2", "4/2 Fourth.B 3"],
            OutlineOf(fixture, edited).Select(static item => $"{item.Index} {item.Title} {item.Page}"));
    }

    /// <summary>Four top-level bookmarks; the second has one child and the fourth two.</summary>
    private static string ListedOutline(PdfEngineFixture fixture, string fileName) =>
        Outlined(fixture, fileName, static document =>
        {
            document.Outlines.Add(Bookmark(document, "First", 1));
            OutlineItemCollection second = Bookmark(document, "Second", 2);
            second.Add(Bookmark(document, "Second.A", 2));
            document.Outlines.Add(second);
            document.Outlines.Add(Bookmark(document, "Third", 3));
            OutlineItemCollection fourth = Bookmark(document, "Fourth", 1);
            fourth.Add(Bookmark(document, "Fourth.A", 2));
            fourth.Add(Bookmark(document, "Fourth.B", 3));
            document.Outlines.Add(fourth);
        });

    private static string Outlined(PdfEngineFixture fixture, string fileName, Action<Document> outline)
    {
        string path = fixture.File(fileName);
        using var document = new Document();
        for (int page = 1; page <= 3; page++)
        {
            document.Pages.Add();
        }

        outline(document);
        document.Save(path);
        return path;
    }

    private static OutlineItemCollection Bookmark(Document document, string title, int page) =>
        new(document.Outlines) { Title = title, Destination = new FitExplicitDestination(document.Pages[page]) };

    private static PdfOutlineItem[] OutlineOf(PdfEngineFixture fixture, string path) =>
        [.. fixture.Engine.GetInfo(path, new PdfInfoRequest { Details = ["outline"] }).Outline!];

    [Fact]
    public void PageTargets_PastTheDocumentReportThePageCount()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("short.pdf", pages: 2);

        PdfEditResult result = fixture.Engine.ApplyOps(input, new PdfOpsBatch
        {
            Ops =
            [
                new AddLinkOp
                {
                    Page = 5,
                    Rect = new PdfRectInput { X = 10, Y = 10, Width = 20, Height = 20 },
                    Url = "https://example.com/",
                },
                new InsertBlankPageOp { At = 5 },
            ],
        }, new PdfEditRequest
        {
            OutputPath = fixture.File("short.out.pdf"),
            Options = new EditCommandOptions { BestEffort = true },
        });

        OpError page = result.Applied[0].Error!;
        Assert.Equal("PAGE_NOT_FOUND", page.Code);
        Assert.Equal("5", page.Details!["requested"]!.GetValue<string>());
        Assert.Equal(2, page.Details["availableCount"]!.GetValue<int>());
        OpError position = result.Applied[1].Error!;
        Assert.Equal("PAGE_NOT_FOUND", position.Code);
        Assert.Equal(3, position.Details!["availableCount"]!.GetValue<int>());
    }

    [Fact]
    public void SetFormField_ChecksTheBoxForAStateItDefines()
    {
        using var fixture = new PdfEngineFixture();
        string input = CheckBoxDocument(fixture);
        string output = fixture.File("checkbox.valid.pdf");

        fixture.Engine.ApplyOps(
            input,
            new PdfOpsBatch { Ops = [new SetFormFieldOp { Name = "Approved", Value = "Yes" }] },
            new PdfEditRequest { OutputPath = output });

        using var reopened = new Document(output);
        var checkbox = (CheckboxField)reopened.Form.Fields.Single();
        Assert.True(checkbox.Checked);
        Assert.Equal("Yes", checkbox.ActiveState);
    }

    [Fact]
    public void QueryForms_ReportsTheValuesThatCheckABoxAndSelectARadioButton()
    {
        using var fixture = new PdfEngineFixture();
        string input = ChoiceDocument(fixture);
        string output = fixture.File("choices.filled.pdf");

        IReadOnlyList<PdfFormField> fields = fixture.Engine.ReadForm(input, new PdfFormReadRequest()).Fields;

        PdfFormField agree = fields.Single(static field => field.Name == "agree");
        Assert.Equal(["Off", "Checked"], agree.States);
        Assert.Equal("Checked", agree.OnValue);
        Assert.Null(agree.Options);
        // Widgets that export several values leave the state to check to the caller.
        PdfFormField multi = fields.Single(static field => field.Name == "multi");
        Assert.Equal(["Off", "Yes", "Alpha", "Beta"], multi.States);
        Assert.Null(multi.OnValue);
        PdfFormField[] buttons = fields.Where(static field => field.Name == "color").ToArray();
        Assert.All(buttons, static button => Assert.Equal(PdfFormFieldTypes.RadioOption, button.Type));
        Assert.All(buttons, static button => Assert.Equal(["Red", "Blue"], button.Options));
        Assert.Equal(["Red", "Blue"], buttons.Select(static button => button.OnValue));
        Assert.All(fields, static field => Assert.True(field.Type is PdfFormFieldTypes.Checkbox or PdfFormFieldTypes.RadioOption));

        fixture.Engine.ApplyOps(
            input,
            new PdfOpsBatch
            {
                Ops =
                [
                    new SetFormFieldOp { Name = "agree", Value = agree.OnValue! },
                    new SetFormFieldOp { Name = "color", Value = buttons[1].OnValue! },
                ],
            },
            new PdfEditRequest { OutputPath = output });

        using var reopened = new Document(output);
        var checkbox = (CheckboxField)reopened.Form["agree"];
        Assert.True(checkbox.Checked);
        Assert.Equal("Checked", checkbox.ActiveState);
        var group = (RadioButtonField)reopened.Form["color"];
        Assert.Equal("Blue", group.Value);
        Assert.Equal(2, group.Selected);
        // Every button of the group reports the group's selection as its value.
        Assert.All(
            fixture.Engine.ReadForm(output, new PdfFormReadRequest()).Fields.Where(static field => field.Name == "color"),
            static button => Assert.Equal("Blue", button.Value));
    }

    [Theory]
    [InlineData("Green")]
    [InlineData("Off")]
    public void SetFormField_RefusesAValueThatSelectsNoRadioButton(string value)
    {
        using var fixture = new PdfEngineFixture();
        string input = ChoiceDocument(fixture);

        CliException error = Assert.Throws<CliException>(() => fixture.Engine.ApplyOps(
            input,
            new PdfOpsBatch { Ops = [new SetFormFieldOp { Name = "color", Value = value }] },
            new PdfEditRequest { OutputPath = fixture.File("choices.invalid.pdf") }));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Contains("Red, Blue", error.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(fixture.File("choices.invalid.pdf")));
    }

    [Fact]
    public void SetFormField_ClearsEveryFieldKindWithANullValue()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("filled.pdf");
        using (var document = new Document())
        {
            Page page = document.Pages.Add();
            var color = new RadioButtonField(page) { PartialName = "color" };
            color.Add(new RadioButtonOptionField(page, new Rectangle(72, 620, 92, 640)) { OptionName = "Red" });
            color.Add(new RadioButtonOptionField(page, new Rectangle(112, 620, 132, 640)) { OptionName = "Blue" });
            document.Form.Add(color);
            document.Form.Add(new CheckboxField(page, new Rectangle(72, 560, 92, 580)) { PartialName = "agree" });
            document.Form.Add(new TextBoxField(page, new Rectangle(72, 500, 200, 520)) { PartialName = "name" });
            var size = new ComboBoxField(page, new Rectangle(72, 440, 200, 460)) { PartialName = "size" };
            size.AddOption("Small");
            size.AddOption("Large");
            document.Form.Add(size);
            color.Value = "Blue";
            ((CheckboxField)document.Form["agree"]).Checked = true;
            ((Field)document.Form["name"]).Value = "Bob";
            size.Value = "Large";
            document.Save(input);
        }

        string output = fixture.File("cleared.pdf");
        PdfEditResult result = fixture.Engine.ApplyOps(input, new PdfOpsBatch
        {
            Ops = [.. new[] { "color", "agree", "name", "size" }.Select(static name => new SetFormFieldOp { Name = name, Value = null })],
        }, new PdfEditRequest { OutputPath = output, Verify = true });

        Assert.True(result.Verification!.Ok);
        using var reopened = new Document(output);
        var group = (RadioButtonField)reopened.Form["color"];
        Assert.Equal("Off", group.Value);
        Assert.Equal(-1, group.Selected);
        Assert.False(((CheckboxField)reopened.Form["agree"]).Checked);
        Assert.Equal(string.Empty, ((Field)reopened.Form["name"]).Value);
        Assert.Equal(string.Empty, ((Field)reopened.Form["size"]).Value);
    }

    /// <summary>
    /// A check box whose on state is not "Yes", one whose widgets export several values, and
    /// a radio group.
    /// </summary>
    private static string ChoiceDocument(PdfEngineFixture fixture)
    {
        string path = fixture.File("choices.pdf");
        using var document = new Document();
        Page page = document.Pages.Add();
        document.Form.Add(new CheckboxField(page, new Rectangle(72, 700, 92, 720))
        {
            PartialName = "agree",
            ExportValue = "Checked",
        });
        var multi = new CheckboxField(page, new Rectangle(72, 660, 92, 680)) { PartialName = "multi" };
        multi.AddOption("Alpha");
        multi.AddOption("Beta");
        document.Form.Add(multi);
        var color = new RadioButtonField(page) { PartialName = "color" };
        color.Add(new RadioButtonOptionField(page, new Rectangle(72, 620, 92, 640)) { OptionName = "Red" });
        color.Add(new RadioButtonOptionField(page, new Rectangle(112, 620, 132, 640)) { OptionName = "Blue" });
        document.Form.Add(color);
        document.Save(path);
        return path;
    }

    private static string CheckBoxDocument(PdfEngineFixture fixture)
    {
        string path = fixture.File("checkbox.pdf");
        using var document = new Document();
        Page page = document.Pages.Add();
        document.Form.Add(new CheckboxField(page, new Rectangle(72, 700, 92, 720))
        {
            PartialName = "Approved",
            ExportValue = "Yes",
        });
        document.Save(path);
        return path;
    }

    [Theory]
    [InlineData("#FFFFFF", 1d, 1d, 1d)]
    [InlineData("#FF0000", 1d, 0d, 0d)]
    public void RedactText_CoversTheTextInTheRequestedColour(
        string fillColor, double red, double green, double blue)
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("redact-colour.pdf", pages: 1);
        string output = fixture.File($"redact-colour{fillColor[1..]}.pdf");

        fixture.Engine.ApplyOps(
            input,
            new PdfOpsBatch
            {
                Ops = [new RedactTextOp { Pattern = "Portable", FillColor = fillColor }],
            },
            new PdfEditRequest { OutputPath = output });

        using var reopened = new Document(output);
        Assert.DoesNotContain("Portable", PageText(reopened.Pages[1]), StringComparison.Ordinal);
        Assert.Contains(FillColours(reopened.Pages[1]), colour =>
            Math.Abs(colour.R - red) < 0.001
            && Math.Abs(colour.G - green) < 0.001
            && Math.Abs(colour.B - blue) < 0.001);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RedactText_WarnsWhenItsPatternMatchesNothing(bool dryRun)
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("unmatched.pdf", pages: 1);

        PdfEditResult result = fixture.Engine.ApplyOps(
            input,
            new PdfOpsBatch
            {
                Ops =
                [
                    new RedactTextOp { Pattern = "Portable" },
                    new RedactTextOp { Pattern = "ID 4711", Pages = "1" },
                ],
            },
            new PdfEditRequest { OutputPath = fixture.File("unmatched.out.pdf"), Verify = !dryRun, Options = new EditCommandOptions { DryRun = dryRun } });

        Warning warning = Assert.Single(result.Warnings!, static warning => warning.Code == "REDACTION_NO_MATCH");
        Assert.Contains("'op-0002' (redact_text)", warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("op-0001", warning.Message, StringComparison.Ordinal);
        // Like a verification issue, the warning never repeats the pattern.
        Assert.DoesNotContain("4711", warning.Message + warning.Hint, StringComparison.Ordinal);
        Assert.Contains("pdf query search", warning.Hint, StringComparison.Ordinal);
    }

    /// <summary>Every fill colour the page sets, including inside its forms.</summary>
    private static IReadOnlyList<Aspose.Pdf.Operators.SetRGBColor> FillColours(Page page)
    {
        var colours = page.Contents.OfType<Aspose.Pdf.Operators.SetRGBColor>().ToList();
        foreach (XForm form in page.Resources.Forms)
        {
            colours.AddRange(form.Contents.OfType<Aspose.Pdf.Operators.SetRGBColor>());
        }

        return colours;
    }

    private static string FormDocument(PdfEngineFixture fixture)
    {
        string path = fixture.File("form.pdf");
        using var document = new Document();
        Page page = document.Pages.Add();
        page.Paragraphs.Add(new TextFragment("Customer"));
        document.Form.Add(new TextBoxField(page, new Rectangle(72, 650, 280, 680))
        {
            PartialName = "Customer",
            Value = "Initial",
            Required = true,
        });
        document.Save(path);
        return path;
    }

    private static string TableDocument(PdfEngineFixture fixture)
    {
        string path = fixture.File("table.pdf");
        using var document = new Document();
        Page page = document.Pages.Add();
        var table = new Table
        {
            ColumnWidths = "160 80",
            DefaultCellBorder = new BorderInfo(BorderSide.All, Color.Black),
        };
        Row header = table.Rows.Add();
        header.Cells.Add("Metric");
        header.Cells.Add("Value");
        Row row = table.Rows.Add();
        row.Cells.Add("Revenue");
        row.Cells.Add("42");
        page.Paragraphs.Add(table);
        document.Save(path);
        return path;
    }

    private static string PageText(Page page)
    {
        var absorber = new TextAbsorber();
        page.Accept(absorber);
        return absorber.Text ?? string.Empty;
    }

    private static byte[] PixelPng() => Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    private static byte[] Bitmap(int width, int height)
    {
        int stride = (width * 3 + 3) & ~3;
        int pixelBytes = stride * height;
        byte[] bytes = new byte[54 + pixelBytes];
        bytes[0] = (byte)'B';
        bytes[1] = (byte)'M';
        BitConverter.GetBytes(bytes.Length).CopyTo(bytes, 2);
        BitConverter.GetBytes(54).CopyTo(bytes, 10);
        BitConverter.GetBytes(40).CopyTo(bytes, 14);
        BitConverter.GetBytes(width).CopyTo(bytes, 18);
        BitConverter.GetBytes(height).CopyTo(bytes, 22);
        BitConverter.GetBytes((short)1).CopyTo(bytes, 26);
        BitConverter.GetBytes((short)24).CopyTo(bytes, 28);
        BitConverter.GetBytes(pixelBytes).CopyTo(bytes, 34);
        for (int y = 0; y < height; y++)
        {
            int row = 54 + y * stride;
            for (int x = 0; x < width; x++)
            {
                int offset = row + x * 3;
                bytes[offset] = (byte)((x * 31 + y * 17) & 255);
                bytes[offset + 1] = (byte)((x * 13 + y * 29) & 255);
                bytes[offset + 2] = (byte)((x * 7 + y * 11) & 255);
            }
        }

        return bytes;
    }

    private static PdfSearchRequest Find(string pattern, bool regex = false, int maxHits = 100) => new()
    {
        Query = new Aspose.Cli.Sdk.Text.SearchQuery(
            Aspose.Cli.Sdk.Text.TextSearch.Create(pattern, regex, caseSensitive: false), maxHits, Scope: null),
    };
}
