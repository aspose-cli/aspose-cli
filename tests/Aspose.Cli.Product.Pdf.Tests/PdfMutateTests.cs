using System.Text;
using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Pdf;
using Aspose.Pdf.Annotations;
using Aspose.Pdf.Forms;
using Aspose.Pdf.Text;
using SkiaSharp;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

public sealed class PdfMutateTests
{
    [Fact]
    public void MarginText_KeepsItsMarginWhenItsCharactersNeedAnotherFont()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("cjk-margins.pdf", pages: 1);
        string output = fixture.File("cjk-margins.out.pdf");

        // The default font has no CJK glyphs, so the engine draws this text in a wider one.
        PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch
            {
                Ops =
                [
                    new AddPageNumbersOp { Text = "第 {n} 页 / 共 {N} 页", Placement = PdfPlacements.BottomRight },
                    new AddHeaderTextOp { Text = "机密文件", Placement = PdfPlacements.TopLeft },
                ],
            },
            Output = TestOutput.At(output),
        });

        using var reopened = new Document(output);
        Page page = reopened.Pages[1];
        Rectangle number = Fragment(page, "第 1 页 / 共 1 页");
        Rectangle header = Fragment(page, "机密文件");
        Assert.Equal(page.Rect.URX - 24, number.URX, 1d);
        Assert.Equal(page.Rect.LLY + 24, number.LLY, 1d);
        Assert.Equal(page.Rect.LLX + 24, header.LLX, 1d);
        Assert.Equal(page.Rect.URY - 24, header.URY, 1d);

        static Rectangle Fragment(Page page, string text)
        {
            var absorber = new TextFragmentAbsorber(text);
            page.Accept(absorber);
            return Assert.Single(absorber.TextFragments).Rectangle;
        }
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(90, false)]
    [InlineData(180, false)]
    [InlineData(270, false)]
    [InlineData(0, true)]
    [InlineData(90, true)]
    [InlineData(180, true)]
    [InlineData(270, true)]
    public void SearchRect_IsWhereTheMatchIsDrawnOnTheVisiblePageAndRedactionsCoverIt(int angle, bool cropped)
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File($"coordinates-{angle}-{cropped}.pdf");
        using (var document = new Document())
        {
            Page page = document.Pages.Add();
            page.SetPageSize(595, 842);
            // PUBLIC is drawn first: a redaction moves the text drawn after the match.
            page.Paragraphs.Add(Text("PUBLIC", 80, 300));
            page.Paragraphs.Add(Text("SECRET", 80, 500));
            document.Save(input);
        }

        // As rotate_pages and crop_pages leave it: the content stays where it was drawn.
        using (var document = new Document(input))
        {
            Page page = document.Pages[1];
            if (cropped)
            {
                page.CropBox = new Rectangle(20, 30, 400, 600);
            }
            page.Rotate = angle switch { 90 => Rotation.on90, 180 => Rotation.on180, 270 => Rotation.on270, _ => Rotation.None };
            document.Save(input);
        }

        PdfRect rect = Assert.Single(PdfSearch.Run(fixture.Session, Find(input, "SECRET")).Hits).Rect;
        PdfRectInput area = new() { X = rect.X - 1, Y = rect.Y - 1, Width = rect.Width + 2, Height = rect.Height + 2 };
        using SKBitmap original = RenderPage(fixture, input);
        foreach (PdfOp op in new PdfOp[]
        {
            new RedactAreaOp { Page = 1, Rect = area, FillColor = "#FFFFFF" },
            new RedactTextOp { Find = "SECRET", FillColor = "#FFFFFF" },
        })
        {
            string output = fixture.File($"coordinates-{angle}-{cropped}-{op.GetType().Name}.pdf");
            PdfEdit.Run(fixture.Session, new PdfEditRequest
            {
                Input = input,
                Batch = new PdfOpsBatch { Ops = [op] },
                Output = TestOutput.At(output),
            });
            string text = PdfRead.Run(fixture.Session, new PdfReadRequest { Input = output }).Pages[0].Text;
            Assert.True(
                !text.Contains("SECRET", StringComparison.Ordinal) && text.Contains("PUBLIC", StringComparison.Ordinal),
                $"After {op.GetType().Name} at {angle} degrees the page reads: {text}");

            // At 72 dpi a pixel is a point: what the redaction cleared is the match's ink, and it
            // lies in the rectangle search reported.
            using SKBitmap redacted = RenderPage(fixture, output);
            Assert.Equal((original.Width, original.Height), (redacted.Width, redacted.Height));
            (int Left, int Top, int Right, int Bottom)? cleared = ClearedInk(original, redacted);
            Assert.True(cleared is not null, $"{op.GetType().Name} cleared no ink at {angle} degrees.");
            (int left, int top, int right, int bottom) = cleared.Value;
            string where = $"{op.GetType().Name} cleared {left},{top}-{right},{bottom}; search said {rect.X:0.#},{rect.Y:0.#} {rect.Width:0.#}x{rect.Height:0.#}";
            Assert.True(left >= rect.X - 1 && top >= rect.Y - 1 && right <= rect.X + rect.Width + 1 && bottom <= rect.Y + rect.Height + 1, where);
            Assert.True(left <= rect.X + 6 && top <= rect.Y + 6 && right >= rect.X + rect.Width - 6 && bottom >= rect.Y + rect.Height - 6, where);
        }

        static TextFragment Text(string text, double x, double y)
        {
            var fragment = new TextFragment(text) { Position = new Position(x, y) };
            fragment.TextState.FontSize = 20;
            return fragment;
        }

        static SKBitmap RenderPage(PdfEngineFixture fixture, string pdf)
        {
            string png = fixture.File(Path.GetFileNameWithoutExtension(pdf) + ".png");
            PdfRender.Run(fixture.Session, new PdfRenderRequest { Input = pdf, Output = TestOutput.At(png, format: "png"), Dpi = 72 });
            return SKBitmap.Decode(png);
        }

        static (int Left, int Top, int Right, int Bottom)? ClearedInk(SKBitmap before, SKBitmap after)
        {
            int left = int.MaxValue, top = int.MaxValue, right = -1, bottom = -1;
            for (int y = 0; y < before.Height; y++)
            {
                for (int x = 0; x < before.Width; x++)
                {
                    if (IsInk(before.GetPixel(x, y)) && !IsInk(after.GetPixel(x, y)))
                    {
                        (left, top, right, bottom) = (Math.Min(left, x), Math.Min(top, y), Math.Max(right, x), Math.Max(bottom, y));
                    }
                }
            }
            return right < 0 ? null : (left, top, right + 1, bottom + 1);
        }

        static bool IsInk(SKColor color) => color.Red < 128 && color.Green < 128 && color.Blue < 128;
    }

    /// <summary>
    /// Lines of text set 10 points apart, as create sets 10 pt text, have character boxes that
    /// overlap the next line's: a redaction removes only what it was asked to, whichever way the
    /// page is turned.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(180)]
    [InlineData(270)]
    public void Redaction_OnCloselySetLines_RemovesOnlyTheTextItSelects(int angle)
    {
        using var fixture = new PdfEngineFixture();
        string text = fixture.File("contacts.txt");
        File.WriteAllLines(text, ["Shanghai: Zhang 13812345678", "Hangzhou: Wang 13998765432", "Nanjing: Li 13700001111"]);
        string created = fixture.File($"contacts-{angle}.created.pdf");
        PdfCreate.Run(fixture.Session, new PdfCreateRequest { Output = TestOutput.At(created), Source = new PdfTextSource(new SourceFile("--from-text", text)) });
        string input = fixture.File($"contacts-{angle}.pdf");
        PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = created,
            Batch = new PdfOpsBatch { Ops = [new RotatePagesOp { Pages = "1", Rotation = angle }] },
            Output = TestOutput.At(input),
        });
        PdfRect wang = Assert.Single(PdfSearch.Run(fixture.Session, Find(input, "13998765432")).Hits).Rect;
        PdfRect zhang = Assert.Single(PdfSearch.Run(fixture.Session, Find(input, "Zhang")).Hits).Rect;
        Assert.True(Math.Abs(wang.X - zhang.X) > wang.Width || Math.Abs(wang.Y - zhang.Y) > 0, "The lines are not apart.");

        foreach ((string name, PdfOp op, string removed) in new (string, PdfOp, string)[]
        {
            ("area", new RedactAreaOp { Page = 1, Rect = new PdfRectInput { X = wang.X, Y = wang.Y, Width = wang.Width, Height = wang.Height } }, "13998765432"),
            ("text", new RedactTextOp { Find = "Zhang" }, "Zhang"),
        })
        {
            string output = fixture.File($"contacts-{angle}-{name}.pdf");
            PdfEdit.Run(fixture.Session, new PdfEditRequest
            {
                Input = input,
                Batch = new PdfOpsBatch { Ops = [op] },
                Output = TestOutput.At(output),
            });
            Assert.Empty(PdfSearch.Run(fixture.Session, Find(output, removed)).Hits);
            foreach (string kept in new[] { "Shanghai", "Zhang", "13812345678", "Hangzhou", "Wang", "13998765432", "Nanjing", "Li 137", "13700001111" }.Where(kept => kept != removed))
            {
                Assert.True(PdfSearch.Run(fixture.Session, Find(output, kept)).Hits.Count == 1, $"redact {name} at {angle} degrees also removed {kept}.");
            }
        }
    }

    [Fact]
    public void RotatePages_SetsARotatedPageUprightWithAngleZero()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("rotated.pdf");
        using (var document = new Document())
        {
            document.Pages.Add().Rotate = Rotation.on180;
            document.Pages.Add().Rotate = Rotation.on90;
            document.Save(input);
        }

        string output = fixture.File("upright.pdf");
        PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch
            {
                Ops = [new RotatePagesOp { Pages = "1-2", Rotation = 0 }],
            },
            Output = TestOutput.At(output),
        });

        Assert.All(
            PdfInfo.Run(fixture.Session, new PdfInspectRequest { Input = output, IncludePreview = true }).Pages!,
            static page => Assert.Equal(0, page.Rotation));
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
        Assert.Single(PdfSearch.Run(fixture.Session, Find(input, pattern, regex)).Hits);
        string output = fixture.File("context.out.pdf");
        PdfEditResult edited = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch { Ops = [new RedactTextOp { Find = pattern, Regex = regex }] },
            Output = TestOutput.At(output),
        });
        Assert.Equal(1, Assert.Single(edited.Applied).ItemsAffected);
        Assert.Contains("PUBLIC: 1234", PdfRead.Run(fixture.Session, new PdfReadRequest { Input = output }).Pages[0].Text, StringComparison.Ordinal);
        Assert.Empty(PdfSearch.Run(fixture.Session, Find(output, pattern, regex)).Hits);
    }
    [Fact]
    public void Edit_VisualContentAndRedactionPersist()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("visual.pdf", pages: 2);
        string image = fixture.File("pixel.png");
        File.WriteAllBytes(image, PixelPng());
        string output = fixture.File("visual.out.pdf");

        PdfEditResult result = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch
            {
                Ops =
                [
                    new AddWatermarkTextOp { Text = "DRAFT", Pages = "1" },
                    new AddWatermarkImageOp { Path = image, Pages = "2", ScalePercent = 10 },
                    new AddPageNumbersOp { Text = "{n}/{N}" },
                    new AddHeaderTextOp { Text = "Header" },
                    new AddFooterTextOp { Text = "Footer" },
                    new AddStampImageOp
                    {
                        Page = 1,
                        Path = image,
                        Rect = new PdfRectInput { X = 20, Y = 20, Width = 20, Height = 20 },
                    },
                    new AddHyperlinkOp
                    {
                        Page = 1,
                        Url = "https://example.com",
                        Rect = new PdfRectInput { X = 20, Y = 60, Width = 120, Height = 20 },
                    },
                    new RedactTextOp { Find = "Portable", Pages = "1" },
                    new RedactAreaOp
                    {
                        Page = 2,
                        Rect = new PdfRectInput { X = 20, Y = 100, Width = 40, Height = 20 },
                    },
                ],
            },
            Output = TestOutput.At(output),
        });

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
        PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch
            {
                Ops = [new SetPageLabelsOp
                {
                    Ranges = [new PdfPageLabelRange { StartPage = startPage, Prefix = "R-", StartingValue = 7 }],
                }],
            },
            Output = TestOutput.At(output),
        });

        PdfInspectResult info = PdfInfo.Run(fixture.Session, new PdfInspectRequest { Input = output });
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

        PdfEditResult firstEdit = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch
            {
                Ops =
                [
                    new SetPropertiesOp
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
                        Ranges = [new PdfPageLabelRange { StartPage = 1, NumberStyle = "roman-lower", Prefix = "A-" }],
                    },
                ],
            },
            Output = TestOutput.At(first),
        });

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

        PdfInspectResult info = PdfInfo.Run(fixture.Session, new PdfInspectRequest { Input = first, Details = ["attachments"] });
        string[] attachmentNames = info.Attachments!.Select(static item => item.Name).ToArray();
        Assert.Equal([logicalName, Path.GetFileName(defaultNamedAttachment)], attachmentNames);
        Assert.DoesNotContain(namedAttachment, attachmentNames);
        Assert.DoesNotContain(defaultNamedAttachment, attachmentNames);
        Assert.All(attachmentNames, static name => Assert.False(Path.IsPathFullyQualified(name)));

        PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = first,
            Batch = new PdfOpsBatch
            {
                Ops =
                [
                    new ClearPropertiesOp(),
                    new DeleteBookmarksOp(),
                    new DeleteAttachmentOp { Name = logicalName },
                    new DeleteAttachmentOp { Name = Path.GetFileName(defaultNamedAttachment) },
                ],
            },
            Output = TestOutput.At(final),
        });

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
        PdfFormInfo read = fixture.Forms(input);
        Assert.Equal("acro", read.Kind);
        Assert.Contains(read.Fields, static field => field.Name == "Customer");

        string filled = fixture.File("form.filled.pdf");
        PdfEditResult filledResult = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch
            {
                Ops = [new SetFormFieldOp { Name = "Customer", Value = "Contoso" }],
            },
            Output = TestOutput.At(filled),
        });
        Assert.Equal(["field/Customer"], Assert.Single(filledResult.Applied).Targets);
        Assert.NotNull(filledResult.Input.Fingerprint);
        Assert.NotNull(filledResult.Output?.Fingerprint);
        Assert.Equal("Contoso", fixture.Forms(filled).Fields.Single().Value);

        PdfExtractResult exported = PdfExtract.Run(fixture.Session, new PdfExtractRequest
        {
            Input = filled,
            What = PdfExtractKinds.Forms,
            Output = new ResolvedDirectory(fixture.File("form-data")),
            FormFormat = PdfFormats.FormData.Single(static format => format.Id == "json"),
        });
        PdfPageOutput data = Assert.Single(exported.Outputs);
        Assert.Equal("json", data.Format);
        Assert.Equal("form.json", Path.GetFileName(data.Path));
        Assert.Null(data.Page);
        Assert.True(data.SizeBytes > 0);
        Assert.Contains("Contoso", File.ReadAllText(data.Path), StringComparison.Ordinal);

        string flattened = fixture.File("form.flattened.pdf");
        PdfEditResult flattenResult = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = filled,
            Batch = new PdfOpsBatch
            {
                Ops =
                [
                    new SetFormFieldOp { Name = "Customer", Value = "Northwind" },
                    new FlattenFormsOp(),
                ],
            },
            Output = TestOutput.At(flattened),
        });
        Assert.All(flattenResult.Applied, static item => Assert.Equal(["field/Customer"], item.Targets));
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
        CliException exception = Assert.Throws<CliException>(() => PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch { Ops = [new SetFormFieldOp { Name = "seed", Value = "value" }] },
            Output = TestOutput.At(output),
        }));

        Assert.Equal("FORM_XFA_UNSUPPORTED", exception.Code.Name);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void Edit_EncryptAndDecryptUseOutOfBandSecrets()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("plain.pdf", pages: 1);
        string encrypted = fixture.File("encrypted.pdf");
        var secrets = new Dictionary<string, Secret>
        {
            ["PDF_USER"] = new("reader"),
            ["PDF_OWNER"] = new("owner"),
        };

        PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch
            {
                Ops =
                [
                    new EncryptDocumentOp
                    {
                        UserPasswordEnv = "PDF_USER",
                        OwnerPasswordEnv = "PDF_OWNER",
                        Permissions = new PdfPermissionsInput { Print = true },
                    },
                ],
            },
            Output = TestOutput.At(encrypted),
            OpSecrets = secrets,
        });

        Assert.Throws<InvalidPasswordException>(() => new Document(encrypted));
        using (var opened = new Document(encrypted, "reader"))
        {
            Assert.True(opened.IsEncrypted);
        }
        PdfInspectResult encryptedInfo = PdfInfo.Run(fixture.Session, new PdfInspectRequest
        {
            Input = encrypted,
            Password = new Secret("reader"),
            Details = ["permissions"],
        });
        Assert.True(encryptedInfo.Permissions?.Print);
        Assert.False(encryptedInfo.Permissions?.Copy);

        string plain = fixture.File("decrypted.pdf");
        PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = encrypted,
            Batch = new PdfOpsBatch { Ops = [new DecryptDocumentOp()] },
            Output = TestOutput.At(plain),
            Password = new Secret("owner"),
        });
        using var decrypted = new Document(plain);
        Assert.False(decrypted.IsEncrypted);
    }

    /// <summary>
    /// The engine garbles document information beyond Latin-1 that it writes into an encrypted
    /// file (PDF-ENCRYPTED-INFO-TEXT), so a batch that sets it and encrypts encrypts a copy.
    /// </summary>
    [Fact]
    public void Edit_KeepsMetadataBeyondLatin1InABatchThatEncrypts()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("plain.pdf", pages: 1);
        string output = fixture.File("secured.pdf");

        PdfEditResult result = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch
            {
                Ops =
                [
                    new SetPropertiesOp { Author = "张伟", Subject = "采购申请", Custom = new Dictionary<string, string> { ["Dept"] = "采购部" } },
                    new EncryptDocumentOp { OwnerPasswordEnv = "PDF_OWNER", Permissions = new PdfPermissionsInput { Print = true } },
                ],
            },
            Output = TestOutput.At(output),
            Verify = true,
            OpSecrets = new Dictionary<string,
            Secret> { ["PDF_OWNER"] = new("owner") },
        });

        Assert.True(result.Verification!.Ok);
        using var reopened = new Document(output, "owner");
        Assert.True(reopened.IsEncrypted);
        Assert.Equal(Permissions.PrintDocument, (Permissions)reopened.Permissions);
        Assert.Equal(("张伟", "采购申请", "采购部"), (reopened.Info.Author, reopened.Info.Subject, reopened.Info["Dept"]));
        // Saving the copy too does not stamp a second evaluation notice.
        Assert.Equal(
            fixture.LicenseState == Aspose.Cli.Sdk.Licensing.LicenseState.Evaluation ? 1 : 0,
            System.Text.RegularExpressions.Regex.Count(PageText(reopened.Pages[1]), "Evaluation Only"));
    }

    /// <summary>
    /// Latin-1 text the PDFDocEncoding gives other characters, such as a no-break space, a C1
    /// control or a soft hyphen, is saved once with the encryption and still reads back as set.
    /// </summary>
    [Theory]
    [InlineData("A B")]
    [InlineData("A\u0080B\u0093C")]
    [InlineData("A­B")]
    [InlineData("A\u0018B")]
    public void Edit_KeepsLatin1MetadataThatPdfDocEncodingMapsElsewhereInABatchThatEncrypts(string title)
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("plain.pdf", pages: 1);
        string output = fixture.File("secured.pdf");

        PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch
            {
                Ops =
                [
                    new SetPropertiesOp { Title = title },
                    new EncryptDocumentOp { OwnerPasswordEnv = "PDF_OWNER" },
                ],
            },
            Output = TestOutput.At(output),
            OpSecrets = new Dictionary<string, Secret> { ["PDF_OWNER"] = new("owner") },
        });

        using var reopened = new Document(output, "owner");
        Assert.Equal(title, reopened.Info.Title);
    }

    /// <summary>
    /// An input that stays encrypted cannot take document information beyond Latin-1
    /// (PDF-ENCRYPTED-INFO-TEXT); a batch that encrypts after setting it, with or without
    /// decrypting first, can.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Edit_RefusesMetadataBeyondLatin1ForAnInputThatStaysEncrypted(bool decryptFirst)
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateEncryptedDocument(string.Empty, "owner", "secured.pdf");
        var secrets = new Dictionary<string, Secret> { ["PDF_OWNER"] = new("owner") };
        string output = fixture.File("secured.out.pdf");

        CliException error = Assert.Throws<CliException>(() => PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch { Ops = [new SetPropertiesOp { Subject = "采购申请" }, new RotatePagesOp { Pages = "1", Rotation = 90 }] },
            Output = TestOutput.At(output),
            Password = new Secret("owner"),
        }));
        PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch
            {
                Ops =
                [
                    .. decryptFirst ? new PdfOp[] { new DecryptDocumentOp() } : [],
                    new SetPropertiesOp { Subject = "采购申请", Keywords = "Café" },
                    new EncryptDocumentOp { OwnerPasswordEnv = "PDF_OWNER" },
                ],
            },
            Output = TestOutput.At(output),
            Password = new Secret("owner"),
            OpSecrets = secrets,
        });

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Contains("the subject '采购申请' has characters beyond Latin-1", error.Message, StringComparison.Ordinal);
        Assert.Contains("encrypt_document", error.Hint, StringComparison.Ordinal);
        Assert.Contains("--encrypt-env", error.Hint, StringComparison.Ordinal);
        Assert.Contains("decrypt_document", error.Hint, StringComparison.Ordinal);
        using var reopened = new Document(output, "owner");
        Assert.True(reopened.IsEncrypted);
        Assert.Equal("采购申请", reopened.Info.Subject);

        // The hint's other way: decrypting first saves the text without encryption.
        string plain = fixture.File("secured.plain.pdf");
        PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch { Ops = [new DecryptDocumentOp(), new SetPropertiesOp { Subject = "采购申请" }] },
            Output = TestOutput.At(plain),
            Password = new Secret("owner"),
        });
        using var decrypted = new Document(plain);
        Assert.False(decrypted.IsEncrypted);
        Assert.Equal("采购申请", decrypted.Info.Subject);
    }

    [Theory]
    [InlineData("reader", Permissions.PrintDocument | Permissions.FillForm, "metadata", "keeps the input's encryption")]
    [InlineData("reader", Permissions.PrintDocument | Permissions.FillForm, "field", null)]
    [InlineData("reader", Permissions.PrintDocument | Permissions.ModifyTextAnnotations, "field", null)]
    [InlineData("reader", Permissions.PrintDocument, "field", "keeps the input's encryption")]
    [InlineData("reader", Permissions.PrintDocument | Permissions.AssembleDocument, "rotate", null)]
    [InlineData("reader", Permissions.PrintDocument | Permissions.AssembleDocument, "metadata", "keeps the input's encryption")]
    [InlineData("reader", Permissions.PrintDocument | Permissions.ModifyTextAnnotations, "rotate", "keeps the input's encryption")]
    [InlineData("reader", Permissions.PrintDocument | Permissions.FillForm, "decrypt_document", "is not encrypted")]
    [InlineData("reader", Permissions.PrintDocument | Permissions.FillForm, "encrypt_document", "encrypt operation set")]
    [InlineData("reader", Permissions.PrintDocument | Permissions.ModifyContent, "metadata", null)]
    [InlineData("reader", Permissions.PrintDocument | Permissions.ModifyContent, "decrypt_document", "is not encrypted")]
    [InlineData("owner", Permissions.PrintDocument | Permissions.FillForm, "metadata", null)]
    [InlineData("owner", Permissions.PrintDocument, "decrypt_document", null)]
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
        PdfEditResult result = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch
            {
                Ops =
                [
                    change switch
                    {
                        "metadata" => new SetPropertiesOp { Title = "Changed" },
                        "field" => new SetFormFieldOp { Name = "Customer", Value = "Contoso" },
                        "rotate" => new RotatePagesOp { Pages = "1", Rotation = 90 },
                        "decrypt_document" => new DecryptDocumentOp(),
                        _ => new EncryptDocumentOp
                        {
                            UserPasswordEnv = "PDF_USER",
                            OwnerPasswordEnv = "PDF_OWNER",
                            Permissions = new PdfPermissionsInput { Print = true, Modify = true },
                        },
                    },
                ],
            },
            Output = TestOutput.At(output),
            Password = new Secret(password),
            OpSecrets = new Dictionary<string,
            Secret> { ["PDF_USER"] = new("new-reader"), ["PDF_OWNER"] = new("new-owner") },
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
    public void Edit_SaysAFileWithOnlyAnOwnerPasswordWasOpenedWithoutAPassword()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateEncryptedDocument(string.Empty, "owner", "owner-only.pdf");

        PdfEditResult result = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch
            {
                Ops = [new SetPropertiesOp { Title = "Changed" }],
            },
            Output = TestOutput.At(fixture.File("owner-only.out.pdf")),
        });

        Warning warning = Assert.Single(result.Warnings!, static item => item.Code == WarningCodes.ProtectionNotEnforced);
        Assert.StartsWith(
            "The input has no open password and was opened without its owner password, so its reader permissions apply; they do not allow set_properties;",
            warning.Message,
            StringComparison.Ordinal);
        Assert.DoesNotContain("user password", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Edit_DryRunDisclosesTheUnpermittedChangeItWouldMake()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateEncryptedDocument("reader", "owner", "restricted.pdf");
        string output = fixture.File("restricted.out.pdf");
        PdfEditResult result = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch
            {
                Ops = [new SetPropertiesOp { Title = "Changed" }, new DecryptDocumentOp()],
            },
            Output = TestOutput.At(output),
            Password = new Secret("reader"),
            Options = new EditCommandOptions { DryRun = true },
        });

        Warning warning = Assert.Single(result.Warnings!, static item => item.Code == WarningCodes.ProtectionNotEnforced);
        Assert.Contains("do not allow set_properties, decrypt_document;", warning.Message, StringComparison.Ordinal);
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
                new SetPropertiesOp { Title = "Changed" },
                new DeleteAttachmentOp { Name = "missing.bin" },
            ],
        };

        Assert.ThrowsAny<Exception>(() => PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = batch,
            Output = TestOutput.At(output),
        }));
        Assert.False(File.Exists(output));

        PdfEditResult partial = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = batch,
            Output = TestOutput.At(output),
            Options = new EditCommandOptions { BestEffort = true },
        });
        Assert.True(partial.HasFailures);
        Assert.Equal([OpStatuses.Ok, OpStatuses.Failed], partial.Applied.Select(static item => item.Status));
        Assert.Equal(["property/title"], partial.Applied[0].Targets);
        Assert.Equal(["attachment/missing.bin"], partial.Applied[1].Targets);
        Assert.Equal("ATTACHMENT_NOT_FOUND", partial.Applied[1].Error!.Code);
        Assert.Equal("missing.bin", partial.Applied[1].Error!.Details!["requested"]!.GetValue<string>());
        Assert.Equal(0, partial.Applied[1].Error!.Details!["availableCount"]!.GetValue<int>());
        using (var reopened = new Document(output))
        {
            Assert.Equal("Changed", reopened.Info.Title);
        }

        string dryOutput = fixture.File("dry.pdf");
        PdfEditResult dry = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch
            {
                Ops = [new RotatePagesOp { Pages = "1", Rotation = 90 }],
            },
            Output = TestOutput.At(dryOutput),
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
        PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch
            {
                Ops =
                [
                    new OptimizeDocumentOp
                    {
                        DownsampleImagesDpi = 72,
                        ImageQuality = 30,
                        CompressStreams = true,
                        RemoveUnusedObjects = true,
                    },
                ],
            },
            Output = TestOutput.At(output),
        });

        Assert.True(new FileInfo(output).Length < new FileInfo(input).Length);
    }

    [Fact]
    public void SearchValidateAndTableExtractionReturnBoundedSemanticResults()
    {
        using var fixture = new PdfEngineFixture();
        string input = TableDocument(fixture);
        PdfSearchResult search = PdfSearch.Run(fixture.Session, Find(input, "Revenue", maxHits: 1));
        Assert.Single(search.Hits);
        Assert.True(search.Hits[0].Rect.Width > 0);

        PdfValidateResult validation = PdfValidate.Run(fixture.Session, new PdfValidateRequest { Input = input, Profile = "pdfa-2b" });
        Assert.Equal("pdfa-2b", validation.Profile);
        Assert.False(validation.Valid);
        Assert.NotEmpty(validation.Issues);

        PdfExtractResult tables = PdfExtract.Run(fixture.Session, new PdfExtractRequest
        {
            Input = input,
            Output = new ResolvedDirectory(fixture.File("tables")),
            What = "tables",
        });
        Assert.NotEmpty(tables.Outputs);
        Assert.All(tables.Outputs, static table =>
        {
            Assert.Equal("csv", table.Format);
            Assert.NotNull(table.Page);
        });
        Assert.Contains(
            tables.Outputs,
            table => File.ReadAllText(table.Path).Contains("Revenue", StringComparison.Ordinal));
        Assert.All(tables.Outputs, static table => Assert.NotEqual(0xEF, File.ReadAllBytes(table.Path)[0]));
    }

    [Fact]
    public void ExtractTables_StartsEachCsvWithAByteOrderMarkOnRequest()
    {
        using var fixture = new PdfEngineFixture();
        string input = TableDocument(fixture);

        PdfExtractResult tables = PdfExtract.Run(fixture.Session, new PdfExtractRequest
        {
            Input = input,
            Output = new ResolvedDirectory(fixture.File("tables")),
            What = "tables",
            ByteOrderMark = true,
        });

        Assert.NotEmpty(tables.Outputs);
        Assert.All(tables.Outputs, static table =>
        {
            byte[] bytes = File.ReadAllBytes(table.Path);
            Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]);
            Assert.Equal(bytes.LongLength, table.SizeBytes);
        });
    }

    [Fact]
    public void ExtractTables_QuotesFieldsWithCommasAndQuotesAndEndsRecordsWithCrLf()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("csv-table.pdf");
        using (var document = new Document())
        {
            Page page = document.Pages.Add();
            var table = new Table
            {
                ColumnWidths = "160 160",
                DefaultCellBorder = new BorderInfo(BorderSide.All, Color.Black),
            };
            Row header = table.Rows.Add();
            header.Cells.Add("Metric");
            header.Cells.Add("Value, note");
            Row row = table.Rows.Add();
            row.Cells.Add("Say \"hi\"");
            row.Cells.Add("42");
            page.Paragraphs.Add(table);
            document.Save(input);
        }

        PdfExtractResult plain = PdfExtract.Run(fixture.Session, new PdfExtractRequest
        {
            Input = input,
            Output = new ResolvedDirectory(fixture.File("csv-plain")),
            What = "tables",
        });
        PdfExtractResult marked = PdfExtract.Run(fixture.Session, new PdfExtractRequest
        {
            Input = input,
            Output = new ResolvedDirectory(fixture.File("csv-marked")),
            What = "tables",
            ByteOrderMark = true,
        });

        // The absorber finds each row as a table of its own.
        string[] expected = ["Metric,\"Value, note\"\r\n", "\"Say \"\"hi\"\"\",42\r\n"];
        Assert.Equal(expected.Select(static csv => Encoding.UTF8.GetBytes(csv)), plain.Outputs.Select(static table => File.ReadAllBytes(table.Path)));
        Assert.Equal(
            expected.Select(static csv => (byte[])[0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(csv)]),
            marked.Outputs.Select(static table => File.ReadAllBytes(table.Path)));
    }

    [Fact]
    public void SetFormField_RefusesACheckBoxStateThatWouldNotDisplay()
    {
        using var fixture = new PdfEngineFixture();
        string input = CheckBoxDocument(fixture);

        CliException error = Assert.Throws<CliException>(() => PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch { Ops = [new SetFormFieldOp { Name = "Approved", Value = "true" }] },
            Output = TestOutput.At(fixture.File("checkbox.invalid.pdf")),
        }));

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

        CliException error = Assert.Throws<CliException>(() => PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch { Ops = [new SetFormFieldOp { Name = "customer", Value = "Contoso" }] },
            Output = TestOutput.At(fixture.File("form.missing.pdf")),
        }));

        Assert.Equal("FIELD_NOT_FOUND", error.Code.Name);
        Assert.Equal("customer", error.Details!["requested"]!.GetValue<string>());
        Assert.Equal(["Customer"], error.Details["available"]!.AsArray().Select(static name => name!.GetValue<string>()));
        Assert.Equal("Customer", error.Details["suggestions"]![0]!.GetValue<string>());
        Assert.Equal("op-0001", error.Details["id"]!.GetValue<string>());
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

        PdfEditResult result = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch
            {
                Ops =
                [
                    new DeleteBookmarksOp { Bookmarks = ["9"] },
                    new DeleteBookmarksOp { Bookmarks = ["1/5"] },
                    new DeleteBookmarksOp { Bookmarks = ["9/1"] },
                    new AddBookmarkOp { Title = "Detail", Page = 1, Parent = "2/1" },
                ],
            },
            Output = TestOutput.At(fixture.File("levels.failed.pdf")),
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
                ErrorPayload error = applied.Error!;
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
        PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch
            {
                Ops =
                [
                    new AddBookmarkOp { Title = "Intro", Page = 1 },
                    new AddBookmarkOp { Title = "Scope", Page = 2, Parent = "1" },
                ],
            },
            Output = TestOutput.At(outlined),
        });

        PdfOutlineItem[] outline = OutlineOf(fixture, outlined);
        Assert.Equal(["1 Intro 1", "1/1 Scope 2"], outline.Select(static item => $"{item.Bookmark} {item.Title} {item.Page}"));

        string edited = fixture.File("indexes.edited.pdf");
        PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = outlined,
            Batch = new PdfOpsBatch
            {
                Ops =
                [
                    new DeleteBookmarksOp { Bookmarks = [outline[1].Bookmark] },
                    new AddBookmarkOp { Title = "Detail", Page = outline[1].Page!.Value, Parent = outline[0].Bookmark },
                ],
            },
            Output = TestOutput.At(edited),
        });

        Assert.Equal(
            ["1 Intro 1", "1/1 Detail 2"],
            OutlineOf(fixture, edited).Select(static item => $"{item.Bookmark} {item.Title} {item.Page}"));
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
        Assert.Equal("2", slashed.Bookmark);

        string edited = fixture.File("slash.edited.pdf");
        PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch
            {
                Ops = [new DeleteBookmarksOp { Bookmarks = [slashed.Bookmark] }],
            },
            Output = TestOutput.At(edited),
        });

        Assert.Equal(
            ["1 A 1", "1/1 B 2"],
            OutlineOf(fixture, edited).Select(static item => $"{item.Bookmark} {item.Title} {item.Page}"));
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
        PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch
            {
                Ops =
                [
                    new DeleteBookmarksOp { Bookmarks = [outline[1].Bookmark] },
                    new AddBookmarkOp { Title = "Detail", Page = 3, Parent = outline[0].Bookmark },
                ],
            },
            Output = TestOutput.At(edited),
        });

        Assert.Equal(
            ["1 Results 1", "1/1 Detail 3"],
            OutlineOf(fixture, edited).Select(static item => $"{item.Bookmark} {item.Title} {item.Page}"));
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
        PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch
            {
                Ops =
                [
                    new AddBookmarkOp { Title = "Child", Page = 3, Parent = outline[0].Bookmark },
                    new DeleteBookmarksOp { Bookmarks = [outline[1].Bookmark] },
                ],
            },
            Output = TestOutput.At(edited),
        });

        Assert.Equal(
            ["1  1", "1/1 Child 3"],
            OutlineOf(fixture, edited).Select(static item => $"{item.Bookmark} {item.Title} {item.Page}"));
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
        PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch
            {
                Ops = [new DeleteBookmarksOp { Bookmarks = ["1"] }, new DeleteBookmarksOp { Bookmarks = ["1"] }],
            },
            Output = TestOutput.At(edited),
        });

        Assert.Equal(
            ["1 Third 3"],
            OutlineOf(fixture, edited).Select(static item => $"{item.Bookmark} {item.Title} {item.Page}"));
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
        PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch
            {
                Ops = [new DeleteBookmarksOp { Bookmarks = ["2"] }],
            },
            Output = TestOutput.At(edited),
        });

        Assert.Equal(
            ["1 Parent 1", "1/1 Results 2", "2 Tail 1"],
            OutlineOf(fixture, edited).Select(static item => $"{item.Bookmark} {item.Title} {item.Page}"));
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
        PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch
            {
                Ops = [new DeleteBookmarksOp { Bookmarks = ["1", "2"] }],
            },
            Output = TestOutput.At(edited),
        });

        Assert.Equal(
            ["1 Results 3"],
            OutlineOf(fixture, edited).Select(static item => $"{item.Bookmark} {item.Title} {item.Page}"));
    }

    [Fact]
    public void Bookmarks_OneOperationDeletesChildrenOfDifferentParents()
    {
        using var fixture = new PdfEngineFixture();
        string input = ListedOutline(fixture, "children.pdf");

        string edited = fixture.File("children.edited.pdf");
        PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch
            {
                Ops = [new DeleteBookmarksOp { Bookmarks = ["4/1", "2/1"] }],
            },
            Output = TestOutput.At(edited),
        });

        Assert.Equal(
            ["1 First 1", "2 Second 2", "3 Third 3", "4 Fourth 1", "4/1 Fourth.B 3"],
            OutlineOf(fixture, edited).Select(static item => $"{item.Bookmark} {item.Title} {item.Page}"));
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
        PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch
            {
                Ops = [new DeleteBookmarksOp { Bookmarks = ["1"] }],
            },
            Output = TestOutput.At(edited),
        });

        Assert.Equal(
            ["1 Results 3"],
            OutlineOf(fixture, edited).Select(static item => $"{item.Bookmark} {item.Title} {item.Page}"));
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
        PdfEditResult result = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch
            {
                Ops = [new DeleteBookmarksOp { Bookmarks = [first, second] }],
            },
            Output = TestOutput.At(edited),
        });

        // Second, its child and Third, each addressed as it was before the deletion.
        Assert.Equal(3, Assert.Single(result.Applied).ItemsAffected);
        Assert.Equal(["bookmark/2", "bookmark/2/1", "bookmark/3"], result.Applied[0].Targets);
        Assert.Equal(
            ["1 First 1", "2 Fourth 1", "2/1 Fourth.A 2", "2/2 Fourth.B 3"],
            OutlineOf(fixture, edited).Select(static item => $"{item.Bookmark} {item.Title} {item.Page}"));
    }

    [Fact]
    public void Bookmarks_DeletingAllCountsEveryBookmark()
    {
        using var fixture = new PdfEngineFixture();
        string input = ListedOutline(fixture, "all.pdf");

        string edited = fixture.File("all.edited.pdf");
        PdfEditResult result = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch
            {
                Ops = [new DeleteBookmarksOp()],
            },
            Output = TestOutput.At(edited),
        });

        Assert.Equal(7, Assert.Single(result.Applied).ItemsAffected);
        Assert.Equal(
            ["bookmark/1", "bookmark/2", "bookmark/2/1", "bookmark/3", "bookmark/4", "bookmark/4/1", "bookmark/4/2"],
            result.Applied[0].Targets);
        Assert.Empty(OutlineOf(fixture, edited));
    }

    /// <summary>
    /// An outcome lists at most 100 targets: deleting more listed bookmarks than that reports
    /// the ranges that cover them, never an internal error, and never every bookmark while some
    /// remain.
    /// </summary>
    [Fact]
    public void Bookmarks_DeletingMoreThanAnOutcomeListsReportsTheRangesThatCoverThem()
    {
        using var fixture = new PdfEngineFixture();
        string input = Outlined(fixture, "many.pdf", static document =>
        {
            for (int index = 1; index <= 150; index++)
            {
                document.Outlines.Add(Bookmark(document, $"Bookmark {index}", 1));
            }
        });

        string edited = fixture.File("many.edited.pdf");
        PdfEditResult result = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch
            {
                Ops = [new DeleteBookmarksOp { Bookmarks = [.. Enumerable.Range(1, 101).Select(static index => $"{index}")] }],
            },
            Output = TestOutput.At(edited),
        });

        Assert.Equal(101, Assert.Single(result.Applied).ItemsAffected);
        Assert.Equal(["bookmark/1-101"], result.Applied[0].Targets);
        Assert.Equal(49, OutlineOf(fixture, edited).Length);
    }

    [Fact]
    public void Bookmarks_AMissingIndexDeletesNoneOfTheList()
    {
        using var fixture = new PdfEngineFixture();
        string input = ListedOutline(fixture, "partial.pdf");

        CliException error = Assert.Throws<CliException>(() => PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch
            {
                Ops = [new DeleteBookmarksOp { Bookmarks = ["1", "2/5"] }],
            },
            Output = TestOutput.At(fixture.File("partial.failed.pdf")),
        }));
        Assert.Equal("BOOKMARK_NOT_FOUND", error.Code.Name);
        Assert.Equal("2/5", error.Details!["requested"]!.GetValue<string>());

        string edited = fixture.File("partial.edited.pdf");
        PdfEditResult result = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch
            {
                Ops =
                [
                    new DeleteBookmarksOp { Bookmarks = ["1", "2/5"] },
                    new AddBookmarkOp { Title = "Added", Page = 1, Parent = "1" },
                ],
            },
            Output = TestOutput.At(edited),
            Options = new EditCommandOptions { BestEffort = true },
        });

        Assert.Equal("BOOKMARK_NOT_FOUND", result.Applied[0].Error!.Code);
        Assert.Equal(["bookmark/1", "bookmark/2/5"], result.Applied[0].Targets);
        Assert.Equal(OpStatuses.Ok, result.Applied[1].Status);
        Assert.Equal(["bookmark/1/1"], result.Applied[1].Targets);
        Assert.Equal(
            ["1 First 1", "1/1 Added 1", "2 Second 2", "2/1 Second.A 2", "3 Third 3", "4 Fourth 1", "4/1 Fourth.A 2", "4/2 Fourth.B 3"],
            OutlineOf(fixture, edited).Select(static item => $"{item.Bookmark} {item.Title} {item.Page}"));
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
        [.. PdfInfo.Run(fixture.Session, new PdfInspectRequest { Input = path, Details = ["outline"] }).Outline!];

    [Fact]
    public void PageTargets_PastTheDocumentReportThePageCount()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("short.pdf", pages: 2);

        PdfEditResult result = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch
            {
                Ops =
                [
                    new AddHyperlinkOp
                    {
                        Page = 5,
                        Rect = new PdfRectInput { X = 10, Y = 10, Width = 20, Height = 20 },
                        Url = "https://example.com/",
                    },
                    new InsertPageOp { At = 5 },
                ],
            },
            Output = TestOutput.At(fixture.File("short.out.pdf")),
            Options = new EditCommandOptions { BestEffort = true },
        });

        ErrorPayload page = result.Applied[0].Error!;
        Assert.Equal("PAGE_NOT_FOUND", page.Code);
        Assert.Equal("5", page.Details!["requested"]!.GetValue<string>());
        Assert.Equal(2, page.Details["availableCount"]!.GetValue<int>());
        ErrorPayload position = result.Applied[1].Error!;
        Assert.Equal("PAGE_NOT_FOUND", position.Code);
        Assert.Equal(3, position.Details!["availableCount"]!.GetValue<int>());
    }

    [Fact]
    public void SetFormField_ChecksTheBoxForAStateItDefines()
    {
        using var fixture = new PdfEngineFixture();
        string input = CheckBoxDocument(fixture);
        string output = fixture.File("checkbox.valid.pdf");

        PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch { Ops = [new SetFormFieldOp { Name = "Approved", Value = "Yes" }] },
            Output = TestOutput.At(output),
        });

        using var reopened = new Document(output);
        var checkbox = (CheckboxField)reopened.Form.Fields.Single();
        Assert.True(checkbox.Checked);
        Assert.Equal("Yes", checkbox.ActiveState);
    }

    [Fact]
    public void InspectForms_ReportsTheValuesThatCheckABoxAndSelectARadioButton()
    {
        using var fixture = new PdfEngineFixture();
        string input = ChoiceDocument(fixture);
        string output = fixture.File("choices.filled.pdf");

        IReadOnlyList<PdfFormField> fields = fixture.Forms(input).Fields;

        PdfFormField agree = fields.Single(static field => field.Name == "agree");
        Assert.Equal(["Off", "Checked"], agree.Options);
        // Widgets that export several values list every state they accept.
        PdfFormField multi = fields.Single(static field => field.Name == "multi");
        Assert.Equal(["Off", "Yes", "Alpha", "Beta"], multi.Options);
        // A radio group is one field, with the values of its buttons and no page of its own.
        PdfFormField color = Assert.Single(fields, static field => field.Name == "color");
        Assert.Equal(PdfFormFieldKinds.Radio, color.Kind);
        Assert.Equal(["Red", "Blue"], color.Options);
        Assert.Null(color.Page);
        Assert.Equal(3, fields.Count);
        Assert.All(fields, static field => Assert.True(field.Kind is PdfFormFieldKinds.Checkbox or PdfFormFieldKinds.Radio));

        PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch
            {
                Ops =
                [
                    new SetFormFieldOp { Name = "agree", Value = agree.Options![1] },
                    new SetFormFieldOp { Name = "color", Value = color.Options![1] },
                ],
            },
            Output = TestOutput.At(output),
        });

        using var reopened = new Document(output);
        var checkbox = (CheckboxField)reopened.Form["agree"];
        Assert.True(checkbox.Checked);
        Assert.Equal("Checked", checkbox.ActiveState);
        var group = (RadioButtonField)reopened.Form["color"];
        Assert.Equal("Blue", group.Value);
        Assert.Equal(2, group.Selected);
        // The group reports its selection as its value.
        Assert.Equal("Blue", Assert.Single(fixture.Forms(output).Fields, static field => field.Name == "color").Value);
    }

    [Fact]
    public void FlattenForms_FlattensEveryButtonOfANamedRadioGroup()
    {
        using var fixture = new PdfEngineFixture();
        string input = ChoiceDocument(fixture);
        string output = fixture.File("choices.flat.pdf");

        PdfEditResult result = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch { Ops = [new FlattenFormsOp { Fields = ["color"] }] },
            Output = TestOutput.At(output),
            Verify = true,
        });

        Assert.Equal(2, Assert.Single(result.Applied).ItemsAffected);
        Assert.True(result.Verification!.Ok, string.Join("; ", result.Verification.Issues.Select(static issue => issue.Message)));
        Assert.Equal(["op-0001"], result.Verification.CheckedOps);
        Assert.Equal(
            ["agree", "multi"],
            fixture.Forms(output).Fields.Select(static field => field.Name));
    }

    [Fact]
    public void SetFormField_UnknownNameListsARadioGroupOnce()
    {
        using var fixture = new PdfEngineFixture();
        string input = ChoiceDocument(fixture);

        CliException error = Assert.Throws<CliException>(() => PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch { Ops = [new SetFormFieldOp { Name = "colour", Value = "Red" }] },
            Output = TestOutput.At(fixture.File("choices.missing.pdf")),
        }));

        Assert.Equal("FIELD_NOT_FOUND", error.Code.Name);
        string[] available = [.. error.Details!["available"]!.AsArray().Select(static name => name!.GetValue<string>())];
        Assert.Equal(["agree", "color", "multi"], available.Order(StringComparer.Ordinal));
        Assert.Equal(3, error.Details["availableCount"]!.GetValue<int>());
        Assert.Equal(
            PdfInfo.Run(fixture.Session, new PdfInspectRequest { Input = input, Details = ["forms"] }).Forms!.FieldCount,
            error.Details["availableCount"]!.GetValue<int>());
    }

    [Theory]
    [InlineData("Green")]
    [InlineData("Off")]
    public void SetFormField_RefusesAValueThatSelectsNoRadioButton(string value)
    {
        using var fixture = new PdfEngineFixture();
        string input = ChoiceDocument(fixture);

        CliException error = Assert.Throws<CliException>(() => PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch { Ops = [new SetFormFieldOp { Name = "color", Value = value }] },
            Output = TestOutput.At(fixture.File("choices.invalid.pdf")),
        }));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Contains("Red, Blue", error.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(fixture.File("choices.invalid.pdf")));
    }

    /// <summary>
    /// A push button holds no value: the engine would draw the value as its caption. A signature
    /// field is filled by signing. Both refuse a value and point to the operation that fills them.
    /// </summary>
    [Theory]
    [InlineData("photo_btn", "D:/inputs/stamp-a.png")]
    [InlineData("photo_btn", null)]
    [InlineData("approval", "Signed")]
    public void SetFormField_OnAPushButtonOrASignatureField_IsRefusedAndNamesAddStampImage(string name, string? value)
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("buttons.pdf");
        using (var document = new Document())
        {
            Page page = document.Pages.Add();
            document.Form.Add(new ButtonField(page, new Rectangle(72, 600, 200, 640)) { PartialName = "photo_btn" });
            document.Form.Add(new SignatureField(page, new Rectangle(72, 500, 200, 540)) { PartialName = "approval" });
            document.Save(input);
        }

        byte[] before = File.ReadAllBytes(input);
        string output = fixture.File("buttons.invalid.pdf");
        CliException error = Assert.Throws<CliException>(() => PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch { Ops = [new SetFormFieldOp { Name = name, Value = value }] },
            Output = TestOutput.At(output),
        }));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Contains($"'{name}'", error.Message, StringComparison.Ordinal);
        Assert.Contains("add_stamp_image", error.Hint, StringComparison.Ordinal);
        Assert.Contains("pdf inspect --detail forms", error.Hint, StringComparison.Ordinal);
        Assert.False(File.Exists(output));
        Assert.Equal(before, File.ReadAllBytes(input));
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
        PdfEditResult result = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch
            {
                Ops = [.. new[] { "color", "agree", "name", "size" }.Select(static name => new SetFormFieldOp { Name = name, Value = null })],
            },
            Output = TestOutput.At(output),
            Verify = true,
        });

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

        PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch
            {
                Ops = [new RedactTextOp { Find = "Portable", FillColor = fillColor }],
            },
            Output = TestOutput.At(output),
        });

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

        PdfEditResult result = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch
            {
                Ops =
                [
                    new RedactTextOp { Find = "Portable" },
                    new RedactTextOp { Find = "ID 4711", Pages = "1" },
                ],
            },
            Output = TestOutput.At(fixture.File("unmatched.out.pdf")),
            Verify = !dryRun,
            Options = new EditCommandOptions { DryRun = dryRun },
        });

        Warning warning = Assert.Single(result.Warnings!, static warning => warning.Code.Name == "OP_NO_EFFECT");
        Assert.Contains("Operation op-0002 (redact_text)", warning.Message, StringComparison.Ordinal);
        Assert.Equal("op-0002", warning.Details!["id"]!.GetValue<string>());
        Assert.Equal("redact_text", warning.Details["op"]!.GetValue<string>());
        Assert.Equal("no-match", warning.Details["reason"]!.GetValue<string>());
        Assert.DoesNotContain("op-0001", warning.Message, StringComparison.Ordinal);
        // Like a verification issue, the warning never repeats the pattern.
        Assert.DoesNotContain("4711", warning.Message + warning.Hint, StringComparison.Ordinal);
        Assert.Contains("pdf search", warning.Hint, StringComparison.Ordinal);
    }

    /// <summary>
    /// One line of three text runs that each start where the previous one ends, without
    /// positioning of their own, as Word writes the runs of a line.
    /// </summary>
    internal const string ConsecutiveRuns = "BT /F1 12 Tf 72 720 Td (Name: ) Tj (Jane Roe) Tj ( signed the order.) Tj ET";

    [Fact]
    public void Redaction_WarnsWhenTheEngineMovesTheTextAfterWhatItRemoved()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateRawDocument("runs.pdf", pages: 3, textContent: ConsecutiveRuns);

        PdfEditResult result = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch
            {
                Ops =
                [
                    new RedactTextOp { Find = "Jane Roe", Pages = "1" },
                    // "Jane Roe" spans x 110.7-162.0 on the baseline at y 720.
                    new RedactAreaOp { Page = 2, Rect = new PdfRectInput { X = 110, Y = 58, Width = 52, Height = 20 } },
                    // Nothing follows "order." on its line.
                    new RedactTextOp { Find = "order.", Pages = "3" },
                ],
            },
            Output = TestOutput.At(fixture.File("runs.out.pdf")),
            Verify = true,
        });

        Assert.True(result.Verification!.Ok);
        Warning warning = Assert.Single(result.Warnings!, static warning => warning.Code.Name == "REDACTION_TEXT_MOVED");
        Assert.Contains("'op-0001' (redact_text) on page 1", warning.Message, StringComparison.Ordinal);
        Assert.Contains("'op-0002' (redact_area) on page 2", warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("op-0003", warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Jane", warning.Message + warning.Hint, StringComparison.Ordinal);
        Assert.Contains("review", warning.Hint, StringComparison.Ordinal);
    }

    /// <summary>
    /// Redacting a match moves the rest of its line left, so one operation that matches twice
    /// on a line removes the later match first: the earlier one still lies where it was found.
    /// </summary>
    [Theory]
    [InlineData("(ID: ) Tj (330106198507124518) Tj (, phone: ) Tj (5550101) Tj (.) Tj")]
    [InlineData("[(ID: )] TJ [(3301061985) 1 (07124518)] TJ [(, phone: )] TJ [(555) 1 (0101)] TJ [(.)] TJ")]
    public void RedactText_RemovesEveryMatchOnALineWhoseTextFollowsOn(string runs)
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateRawDocument("line.pdf", pages: 1, textContent: $"BT /F1 12 Tf 72 720 Td {runs} ET");
        string output = fixture.File("line.out.pdf");

        PdfEditResult result = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch { Ops = [new RedactTextOp { Find = @"\d{18}|555\d{4}", Regex = true }] },
            Output = TestOutput.At(output),
            Verify = true,
        });

        Assert.True(result.Verification!.Ok);
        // Both matches lie on the one page the operation changed.
        Assert.Equal(1, Assert.Single(result.Applied).ItemsAffected);
        Assert.Equal(["page/1"], result.Applied[0].Targets);
        using var reopened = new Document(output);
        string text = PageText(reopened.Pages[1]);
        Assert.DoesNotContain("0101", text, StringComparison.Ordinal);
        Assert.Contains(", phone: ", text, StringComparison.Ordinal);
        Assert.Contains(".", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The removal moves the text after a match in the content stream, wherever it is drawn, and
    /// the search returns the matches in that order: text kerned back to the left of the first
    /// match is removed first.
    /// </summary>
    [Fact]
    public void RedactText_RemovesMatchesInReverseContentOrder()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateRawDocument("kerned.pdf", pages: 1,
            textContent: "BT /F1 12 Tf 300 720 Td [(5550101.) 20000] TJ (5550202 tail) Tj ET");
        string output = fixture.File("kerned.out.pdf");

        PdfEditResult result = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch { Ops = [new RedactTextOp { Find = @"555\d{4}", Regex = true }] },
            Output = TestOutput.At(output),
        });

        Assert.Equal(1, Assert.Single(result.Applied).ItemsAffected);
        using var reopened = new Document(output);
        string text = PageText(reopened.Pages[1]);
        Assert.DoesNotMatch("55|0101|0202", text);
        Assert.Contains("tail", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The PDF-only recipe the Skill gives for areas: each search hit's rectangle as it is,
    /// the areas of a line from right to left, removes the values and keeps the text between.
    /// </summary>
    [Fact]
    public void RedactArea_OnSearchRectanglesFromRightToLeftKeepsTheTextBesideThem()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateRawDocument("line.pdf", pages: 1,
            textContent: "BT /F1 12 Tf 72 720 Td (ID: ) Tj (330106198507124518) Tj (, phone: ) Tj (5550101) Tj (.) Tj ET");
        string output = fixture.File("line.out.pdf");
        IReadOnlyList<PdfSearchHit> hits = PdfSearch.Run(fixture.Session, Find(input, @"\d{18}|555\d{4}", regex: true)).Hits;

        PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch
            {
                Ops = [.. hits.OrderByDescending(static hit => hit.Rect.X).Select(static hit => new RedactAreaOp
                {
                    Page = hit.Page,
                    Rect = new PdfRectInput { X = hit.Rect.X, Y = hit.Rect.Y, Width = hit.Rect.Width, Height = hit.Rect.Height },
                })],
            },
            Output = TestOutput.At(output),
        });

        Assert.Equal(2, hits.Count);
        using var reopened = new Document(output);
        string text = PageText(reopened.Pages[1]);
        Assert.DoesNotContain("0101", text, StringComparison.Ordinal);
        Assert.DoesNotContain("4518", text, StringComparison.Ordinal);
        Assert.Contains("ID: , phone: .", text, StringComparison.Ordinal);
    }

    [Fact]
    public void SearchAndRedaction_MatchLiteralTextAcrossGapsBetweenEastAsianAndOtherCharacters()
    {
        using var fixture = new PdfEngineFixture();
        string input = WriteSpacedRuns(fixture, "autospace.pdf", "Due", "2026", "年", "10", "月", "31", "日");

        PdfSearchHit hit = Assert.Single(PdfSearch.Run(fixture.Session, Find(input, "2026年10月31日")).Hits);
        Assert.Matches(@"^2026\s*年\s*10\s*月\s*31\s*日$", hit.Text);
        // Only the boundaries between East Asian and other characters take a gap.
        Assert.Empty(PdfSearch.Run(fixture.Session, Find(input, "Due2026")).Hits);

        string output = fixture.File("autospace.out.pdf");
        PdfEditResult edited = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch { Ops = [new RedactTextOp { Find = "2026年10月31日" }] },
            Output = TestOutput.At(output),
            Verify = true,
        });

        Assert.Equal(1, Assert.Single(edited.Applied).ItemsAffected);
        Assert.True(edited.Verification!.Ok);
        // Each run has a position of its own, so the redaction moves none.
        Assert.DoesNotContain(edited.Warnings ?? [], static warning => warning.Code.Name == "REDACTION_TEXT_MOVED");
        string text = PdfRead.Run(fixture.Session, new PdfReadRequest { Input = output }).Pages[0].Text;
        Assert.Contains("Due", text, StringComparison.Ordinal);
        // The evaluation watermark names a year, so the check reads the East Asian characters.
        Assert.DoesNotContain("年", text, StringComparison.Ordinal);
        Assert.DoesNotContain("日", text, StringComparison.Ordinal);
    }

    [Fact]
    public void SearchAndRedaction_DoNotMatchLiteralTextAcrossALineBreak()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("twolines.pdf");
        using (var document = new Document())
        {
            var builder = new TextBuilder(document.Pages.Add());
            Aspose.Pdf.Text.Font font = FontRepository.FindFont("SimSun");
            foreach ((string run, double y) in new[] { ("Due 2026年", 700.0), ("10月31日", 680.0) })
            {
                var fragment = new TextFragment(run) { Position = new Position(72, y) };
                fragment.TextState.Font = font;
                fragment.TextState.FontSize = 12;
                builder.AppendText(fragment);
            }

            document.Save(input);
        }

        Assert.Single(PdfSearch.Run(fixture.Session, Find(input, "2026年")).Hits);
        Assert.Empty(PdfSearch.Run(fixture.Session, Find(input, "2026年10月")).Hits);

        PdfEditResult edited = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch { Ops = [new RedactTextOp { Find = "2026年10月" }] },
            Output = TestOutput.At(fixture.File("twolines.out.pdf")),
            Verify = true,
        });

        Assert.Equal(0, Assert.Single(edited.Applied).ItemsAffected);
        Assert.Single(edited.Warnings!, static warning => warning.Code.Name == "OP_NO_EFFECT");
    }

    /// <summary>
    /// One line of runs, each placed 3 points after the previous one ends, the space Word's
    /// automatic spacing leaves between East Asian text and digits.
    /// </summary>
    internal static string WriteSpacedRuns(PdfEngineFixture fixture, string fileName, params string[] runs)
    {
        string path = fixture.File(fileName);
        using var document = new Document();
        var builder = new TextBuilder(document.Pages.Add());
        Aspose.Pdf.Text.Font font = FontRepository.FindFont("SimSun");
        double x = 72;
        foreach (string run in runs)
        {
            var fragment = new TextFragment(run) { Position = new Position(x, 700) };
            fragment.TextState.Font = font;
            fragment.TextState.FontSize = 12;
            builder.AppendText(fragment);
            x += font.MeasureString(run, 12) + 3;
        }

        document.Save(path);
        return path;
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

    internal static byte[] Bitmap(int width, int height)
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

    [Theory]
    [InlineData("redact_area")]
    [InlineData("add_hyperlink")]
    [InlineData("add_stamp_image")]
    public void Rectangle_BeyondTheVisiblePageIsAnInvalidOperation(string operation)
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("plain.pdf", pages: 1);
        string image = fixture.File("stamp.bmp");
        File.WriteAllBytes(image, Bitmap(4, 4));
        var rect = new PdfRectInput { X = 10, Y = 900, Width = 50, Height = 20 };
        PdfOp op = operation switch
        {
            "redact_area" => new RedactAreaOp { Page = 1, Rect = rect },
            "add_hyperlink" => new AddHyperlinkOp { Page = 1, Rect = rect, Url = "https://example.com" },
            _ => new AddStampImageOp { Page = 1, Rect = rect, Path = image },
        };

        CliException error = Assert.Throws<CliException>(() => PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch { Ops = [op] },
            Output = TestOutput.At(fixture.File("outside.pdf")),
        }));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Equal("op-0001", error.Details!["id"]!.GetValue<string>());
        Assert.Contains("595 x 842", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Search_ReportsRectanglesToTheHundredthOfAPoint()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("fractional.pdf");
        using (var document = new Document())
        {
            Page page = document.Pages.Add();
            for (int index = 0; index < 5; index++)
            {
                var stamp = new TextStamp($"Total {index}") { XIndent = 72.3 + index * 0.17, YIndent = 347.7 + index * 41.13 };
                stamp.TextState.FontSize = 11.3f;
                page.AddStamp(stamp);
            }
            document.Save(input);
        }

        IReadOnlyList<PdfSearchHit> hits = PdfSearch.Run(fixture.Session, Find(input, "Total")).Hits;

        Assert.Equal(5, hits.Count);
        Assert.All(hits.SelectMany(static hit => new[] { hit.Rect.X, hit.Rect.Y, hit.Rect.Width, hit.Rect.Height }),
            static value => Assert.Equal(Math.Round(value, 2), value));
    }

    private static PdfSearchRequest Find(string input, string pattern, bool regex = false, int maxHits = 100) => new()
    {
        Input = input,
        Query = new Aspose.Cli.Sdk.Text.SearchQuery(
            Aspose.Cli.Sdk.Text.TextSearch.Create(pattern, regex, caseSensitive: false), maxHits, Scope: null),
    };
}
