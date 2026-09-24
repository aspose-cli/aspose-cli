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
            new PdfSearchRequest { Pattern = "SECRET" }).Hits).Rect;
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
        Assert.Single(fixture.Engine.Search(input, new PdfSearchRequest { Pattern = pattern, Regex = regex }).Hits);
        string output = fixture.File("context.out.pdf");
        PdfEditResult edited = fixture.Engine.ApplyOps(input,
            new PdfOpsBatch { Ops = [new RedactTextOp { Pattern = pattern, Regex = regex }] },
            new PdfEditRequest { OutputPath = output });
        Assert.Equal(1, Assert.Single(edited.Applied).ItemsAffected);
        Assert.Contains("PUBLIC: 1234", fixture.Engine.Read(output, new PdfReadRequest()).Pages[0].Text, StringComparison.Ordinal);
        Assert.Empty(fixture.Engine.Search(output, new PdfSearchRequest { Pattern = pattern, Regex = regex }).Hits);
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

        Assert.Equal("reopened", result.Mutation?.Verification);
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
        Assert.Equal("reopened", filledResult.Mutation?.Verification);
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
        PdfSearchResult search = fixture.Engine.Search(input, new PdfSearchRequest
        {
            Pattern = "Revenue",
            MaxHits = 1,
        });
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
}
