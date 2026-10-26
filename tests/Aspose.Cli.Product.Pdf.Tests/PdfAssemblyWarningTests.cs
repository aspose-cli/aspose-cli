using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.TestKit;
using Aspose.Pdf;
using Aspose.Pdf.Annotations;
using Aspose.Pdf.Devices;
using Aspose.Pdf.Forms;
using Aspose.Pdf.Text;
using SkiaSharp;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>
/// What assembling a document from converted PDFs discloses: page numbers stamped over the
/// numbers a converted page already shows, inputs of different paper merged together, and font
/// subsets that optimize_document cuts to what the characters need.
/// </summary>
public sealed class PdfAssemblyWarningTests
{
    /// <summary>
    /// A page whose header or footer already shows a page number, as a PDF converted from Word
    /// does, is numbered a second time with a warning that names it; pages without one, a footer
    /// that is not a number and a number in the body do not warn.
    /// </summary>
    [Theory]
    [InlineData("{n}", "bottom-center")]
    [InlineData("- {n} -", "bottom-right")]
    [InlineData("Page {n} of {N}", "bottom-left")]
    [InlineData("第 {n} 页 / 共 {N} 页", "bottom-center")]
    public void AddPageNumbers_OnPagesThatShowANumber_WarnsWithThosePages(string existing, string placement)
    {
        using var fixture = new PdfEngineFixture();
        string text = fixture.File("body.txt");
        File.WriteAllLines(text, ["12", "Quarterly figures"]);
        string created = fixture.File("created.pdf");
        PdfCreate.Run(fixture.Session, new PdfCreateRequest { Output = TestOutput.At(created), Source = new PdfTextSource(new SourceFile("--from-text", text)) });
        string pages = fixture.File("pages.pdf");
        PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = created,
            Batch = new PdfOpsBatch { Ops = [new InsertPageOp { At = 2 }, new InsertPageOp { At = 3 }, new AddFooterTextOp { Text = "Confidential" }] },
            Output = TestOutput.At(pages),
        });
        string numbered = fixture.File("numbered.pdf");
        PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = pages,
            Batch = new PdfOpsBatch { Ops = [new AddPageNumbersOp { Pages = "2-3", Text = existing, Placement = placement }] },
            Output = TestOutput.At(numbered),
        });

        PdfEditResult plain = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = pages,
            Batch = new PdfOpsBatch { Ops = [new AddPageNumbersOp()] },
            Output = TestOutput.At(fixture.File("plain.out.pdf")),
        });
        PdfEditResult twice = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = numbered,
            Batch = new PdfOpsBatch { Ops = [new AddPageNumbersOp { Id = "numbers" }] },
            Output = TestOutput.At(fixture.File("twice.out.pdf")),
        });

        Assert.DoesNotContain(plain.Warnings ?? [], static warning => warning.Code.Name == "PAGE_NUMBERS_ALREADY_PRESENT");
        Warning warning = Assert.Single(twice.Warnings ?? [], static warning => warning.Code.Name == "PAGE_NUMBERS_ALREADY_PRESENT");
        Assert.Equal("page/2", warning.Location);
        Assert.Contains("'numbers' (add_page_numbers) numbered pages 2-3,", warning.Message, StringComparison.Ordinal);
        Assert.Contains("header or footer", warning.Message, StringComparison.Ordinal);
        Assert.Equal("numbers", (string?)warning.Details?["id"]);
    }

    /// <summary>Merging A4 portrait pages with Letter landscape ones warns with both sizes; one paper, also turned, does not.</summary>
    [Fact]
    public void Merge_OfDifferentPaper_WarnsWithTheSizes()
    {
        using var fixture = new PdfEngineFixture();
        string a4 = Create(fixture, "a4.pdf", PageSizePresets.A4);
        string otherA4 = Create(fixture, "a4-other.pdf", PageSizePresets.A4);
        string letter = Create(fixture, "letter.pdf", PageSizePresets.Letter);
        string landscape = fixture.File("letter-landscape.pdf");
        PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = letter,
            Batch = new PdfOpsBatch { Ops = [new RotatePagesOp { Pages = "1", Rotation = 90 }] },
            Output = TestOutput.At(landscape),
        });
        string a4Landscape = fixture.File("a4-landscape.pdf");
        PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = otherA4,
            Batch = new PdfOpsBatch { Ops = [new RotatePagesOp { Pages = "1", Rotation = 90 }] },
            Output = TestOutput.At(a4Landscape),
        });

        PdfMergeResult mixed = PdfMerge.Run(fixture.Session, new PdfMergeRequest { Output = TestOutput.At(fixture.File("mixed.pdf")), InputPaths = [a4, landscape] });
        PdfMergeResult same = PdfMerge.Run(fixture.Session, new PdfMergeRequest { Output = TestOutput.At(fixture.File("same.pdf")), InputPaths = [a4, otherA4, a4Landscape] });

        Warning warning = Assert.Single(mixed.Warnings ?? [], static warning => warning.Code.Name == "PAGE_SIZES_MIXED");
        Assert.Contains("a4 595.28 x 841.89 pt (1 page)", warning.Message, StringComparison.Ordinal);
        Assert.Contains("letter 792 x 612 pt (1 page)", warning.Message, StringComparison.Ordinal);
        Assert.False(warning.AffectsCompleteness);
        Assert.DoesNotContain(same.Warnings ?? [], static warning => warning.Code.Name == "PAGE_SIZES_MIXED");
    }

    /// <summary>
    /// A PDF created from a few lines of Chinese and English embeds font subsets still sized for
    /// the whole font; optimize_document cuts them to what the characters need, reports the fonts
    /// it changed, and keeps the text.
    /// </summary>
    [Fact]
    public void OptimizeDocument_CutsFontSubsetsToTheCharactersUsed()
    {
        using var fixture = new PdfEngineFixture();
        string markdown = fixture.File("annex.md");
        File.WriteAllText(markdown, "# 附件 Annex A：设备清单 Equipment list\n\n1. 机架式服务器 Rack server 2U\n2. 万兆交换机 10GbE switch\n");
        string created = fixture.File("annex.pdf");
        PdfCreate.Run(fixture.Session, new PdfCreateRequest { Output = TestOutput.At(created), Source = new PdfMarkdownSource(new SourceFile("--from-markdown", markdown)) });
        string optimized = fixture.File("annex.optimized.pdf");

        PdfEditResult result = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = created,
            Batch = new PdfOpsBatch { Ops = [new OptimizeDocumentOp()] },
            Output = TestOutput.At(optimized),
        });

        long before = new FileInfo(created).Length, after = new FileInfo(optimized).Length;
        Assert.True(after * 3 < before, $"optimize_document left {after} of {before} bytes.");
        Assert.True(Assert.Single(result.Applied).ItemsAffected > 0);
        Assert.DoesNotContain(result.Warnings ?? [], static warning => warning.Code.Name == "OP_NO_EFFECT");
        // Without a license the edit adds another evaluation mark, so each phrase is searched for.
        foreach (string phrase in new[] { "Equipment list", "Rack server 2U", "10GbE switch", "机架式服务器" })
        {
            Assert.Equal(Count(created, phrase), Count(optimized, phrase));
            Assert.True(Count(optimized, phrase) > 0, phrase);
        }
    }

    /// <summary>
    /// The engine leaves a font it subset as it is, so optimizing an optimized PDF again changes
    /// nothing. It runs with a license: each edit in evaluation mode adds an evaluation mark in a
    /// new font, which the next optimizing cuts.
    /// </summary>
    [LicensedFact]
    public void OptimizeDocument_OfAnOptimizedPdf_ChangesNothing()
    {
        using var fixture = new PdfEngineFixture();
        string optimized = fixture.File("optimized.pdf");
        PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = CreateFromMarkdown(fixture, "created.pdf"),
            Batch = new PdfOpsBatch { Ops = [new OptimizeDocumentOp()] },
            Output = TestOutput.At(optimized),
        });

        PdfEditResult again = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = optimized,
            Batch = new PdfOpsBatch { Ops = [new OptimizeDocumentOp()] },
            Output = TestOutput.At(fixture.File("optimized-again.pdf")),
        });

        Assert.Equal(0, Assert.Single(again.Applied).ItemsAffected);
        Assert.Contains(again.Warnings ?? [], static warning => warning.Code.Name == "OP_NO_EFFECT");
    }

    /// <summary>
    /// A form whose field draws its value in an embedded font keeps the font whole when
    /// optimized, so a value of characters the document did not show before still fills the field
    /// and draws as it does without optimizing; flattening first lets optimizing cut the font.
    /// </summary>
    [Fact]
    public void OptimizeDocument_KeepsTheFontsOfAFormFieldLeftToFill()
    {
        using var fixture = new PdfEngineFixture();
        _ = fixture.LicenseState;
        string form = fixture.File("form.pdf");
        using (var document = new Document())
        {
            Page page = document.Pages.Add();
            Font font = FontRepository.FindFont("Arial");
            font.IsEmbedded = true;
            var label = new TextFragment("Name");
            label.TextState.Font = font;
            page.Paragraphs.Add(label);
            document.Form.Add(new TextBoxField(page, new Rectangle(72, 600, 400, 630))
            {
                PartialName = "name",
                DefaultAppearance = new DefaultAppearance(font, 12, System.Drawing.Color.Black),
                Value = "Ming",
            });
            document.Save(form);
        }

        string optimized = fixture.File("form.optimized.pdf");
        PdfEdit.Run(fixture.Session, new PdfEditRequest { Input = form, Batch = new PdfOpsBatch { Ops = [new OptimizeDocumentOp()] }, Output = TestOutput.At(optimized) });
        string filled = Fill(optimized, "filled.pdf"), reference = Fill(form, "reference.pdf");
        string flattened = fixture.File("form.flattened.pdf");
        PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = filled,
            Batch = new PdfOpsBatch { Ops = [new FlattenFormsOp(), new OptimizeDocumentOp()] },
            Output = TestOutput.At(flattened),
        });

        Assert.Equal("Zoe Quartz 42", Assert.Single(fixture.Forms(filled).Fields!).Value);
        Assert.True(DifferentPixels(filled, reference, new SKRectI(60, 200, 420, 250)) < 0.001, "The field draws its value otherwise than without optimizing.");
        Assert.True(new FileInfo(flattened).Length * 2 < new FileInfo(filled).Length, "Flattening and optimizing did not cut the field's font.");

        string Fill(string input, string name)
        {
            string output = fixture.File(name);
            PdfEditResult result = PdfEdit.Run(fixture.Session, new PdfEditRequest
            {
                Input = input,
                Batch = new PdfOpsBatch { Ops = [new SetFormFieldOp { Name = "name", Value = "Zoe Quartz 42" }] },
                Output = TestOutput.At(output),
                Verify = true,
            });
            Assert.True(result.Verification!.Ok);
            return output;
        }
    }

    /// <summary>
    /// An encrypted PDF stays encrypted with its password when optimizing cuts its fonts. Streams
    /// stay uncompressed: compressing them in an encrypted PDF writes a file no reader opens, as it
    /// did before fonts were cut.
    /// </summary>
    [Fact]
    public void OptimizeDocument_KeepsTheEncryptionOfAnEncryptedInput()
    {
        using var fixture = new PdfEngineFixture();
        string created = CreateFromMarkdown(fixture, "plain.pdf");
        string encrypted = fixture.File("encrypted.pdf");
        using (var document = new Document(created))
        {
            document.Encrypt("user-secret", "owner-secret", Aspose.Pdf.Facades.DocumentPrivilege.AllowAll, CryptoAlgorithm.AESx256, usePdf20: false);
            document.Save(encrypted);
        }

        string optimized = fixture.File("encrypted.optimized.pdf");
        PdfEditResult result = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = encrypted,
            Password = new Secret("user-secret"),
            Batch = new PdfOpsBatch { Ops = [new OptimizeDocumentOp { CompressStreams = false }] },
            Output = TestOutput.At(optimized),
        });

        Assert.True(Assert.Single(result.Applied).ItemsAffected > 0);
        Assert.True(new FileInfo(optimized).Length * 3 < new FileInfo(encrypted).Length);
        Assert.ThrowsAny<Exception>(() => new Document(optimized).Dispose());
        using var reopened = new Document(optimized, "user-secret");
        Assert.True(reopened.IsEncrypted);
    }

    /// <summary>
    /// Optimizing a signed PDF keeps its fonts whole, whose subsetting would rewrite what the
    /// signature covers: it changes no font and adds no warning, as before fonts were cut.
    /// </summary>
    [Fact]
    public void OptimizeDocument_OfASignedPdf_KeepsItsFontsWhole()
    {
        using var fixture = new PdfEngineFixture();
        const string certificatePassword = "test-certificate-password";
        string created = CreateFromMarkdown(fixture, "unsigned.pdf");
        string signed = fixture.File("signed.pdf");
        PdfSign.Run(fixture.Session, new PdfSignRequest
        {
            Input = created,
            Output = TestOutput.At(signed),
            CertificatePath = fixture.CreateCertificate(certificatePassword),
            CertificatePassword = new Secret(certificatePassword),
        });

        PdfEditResult result = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = signed,
            Batch = new PdfOpsBatch { Ops = [new OptimizeDocumentOp()] },
            Output = TestOutput.At(fixture.File("signed.optimized.pdf")),
        });

        Assert.Equal(0, Assert.Single(result.Applied).ItemsAffected);
        Assert.DoesNotContain(result.Warnings ?? [], static warning => warning.Code.Name == "SIGNATURE_INVALIDATED");
    }

    /// <summary>
    /// Through the built CLI: PDFs that Word and Excel documents convert to with a page number in
    /// their footer warn when numbered again; a sheet whose rows hold lone numbers near the page
    /// edges, with narrow margins, and a footer that shows a year do not. It runs with a license:
    /// an evaluation copy of a converted sheet holds more pages than evaluation mode reads.
    /// </summary>
    [Category(TestCategory.Slow)]
    [LicensedFact]
    public void AddPageNumbers_OnConvertedDocuments_WarnsOnlyWhereTheyShowAPageNumber()
    {
        using var workspace = new TempWorkspace();
        File.WriteAllText(workspace.File("report.md"), "# Report\n\nRevenue grew in October.\n");
        Run("words", "create", "report.docx", "--from-markdown", "report.md").Succeeded();
        Run("words", "convert", "report.docx", "--to", "pdf", "--out", "report.pdf").Succeeded();
        Sheet("numbered", """{"ops":[{"op":"set_page_setup","footerCenter":"&P"}]}""", "--set", "Sheet1!A1=Revenue", "--set", "Sheet1!B1=700000");
        Sheet("year", """{"ops":[{"op":"set_page_setup","footerCenter":"2025"}]}""", "--set", "Sheet1!A1=Revenue", "--set", "Sheet1!B1=700000");
        // Lone numbers on tall rows reach the top and bottom edges of both pages.
        Sheet("rows", """{"ops":[{"op":"resize_rows","first":1,"last":40,"height":30},{"op":"set_page_setup","margins":{"top":14,"bottom":14},"headerMargin":0,"footerMargin":0}]}""",
            [.. Enumerable.Range(1, 40).SelectMany(static row => new[] { "--set", $"Sheet1!A{row}={row % 5 + 5}" })]);

        Assert.Equal(["page/1"], Numbered("report.pdf"));
        Assert.Equal(["page/1"], Numbered("numbered.pdf"));
        Assert.Empty(Numbered("year.pdf"));
        Assert.Empty(Numbered("rows.pdf"));

        void Sheet(string name, string ops, params string[] cells)
        {
            Run("cells", "create", name + ".xlsx").Succeeded();
            Run(["cells", "edit", name + ".xlsx", "--in-place", "--ops", ops, .. cells]).Succeeded();
            Run("cells", "convert", name + ".xlsx", "--to", "pdf", "--out", name + ".pdf").Succeeded();
        }

        CliResult Run(params string[] arguments) => workspace.Run([.. arguments, "--license", TestLicense.Path!]);

        string[] Numbered(string pdf) =>
        [
            .. Run("pdf", "edit", pdf, "--ops", """{"ops":[{"op":"add_page_numbers"}]}""", "--out", pdf + ".numbered.pdf", "--output-format", "json")
                .Json()["warnings"]?.AsArray()
                .Where(static warning => (string?)warning!["code"] == "PAGE_NUMBERS_ALREADY_PRESENT")
                .Select(static warning => (string?)warning!["location"] ?? "") ?? [],
        ];
    }

    private static string Create(PdfEngineFixture fixture, string name, string pageSize)
    {
        string text = fixture.File(name + ".txt");
        File.WriteAllText(text, "Body text");
        string path = fixture.File(name);
        PdfCreate.Run(fixture.Session, new PdfCreateRequest { Output = TestOutput.At(path), Source = new PdfTextSource(new SourceFile("--from-text", text)), PageSize = pageSize });
        return path;
    }

    private static string CreateFromMarkdown(PdfEngineFixture fixture, string name)
    {
        string markdown = fixture.File(name + ".md");
        File.WriteAllText(markdown, "# 设备清单 Equipment list\n\n机架式服务器 Rack server 2U\n");
        string path = fixture.File(name);
        PdfCreate.Run(fixture.Session, new PdfCreateRequest { Output = TestOutput.At(path), Source = new PdfMarkdownSource(new SourceFile("--from-markdown", markdown)) });
        return path;
    }

    /// <summary>
    /// The share of the pixels in an area of the first page, in points from its top-left corner,
    /// that differ between two PDFs at 72 dpi.
    /// </summary>
    private static double DifferentPixels(string first, string second, SKRectI area)
    {
        using SKBitmap a = Render(first), b = Render(second);
        int different = 0;
        for (int y = area.Top; y < area.Bottom; y++)
        {
            for (int x = area.Left; x < area.Right; x++)
            {
                different += a.GetPixel(x, y) == b.GetPixel(x, y) ? 0 : 1;
            }
        }

        return (double)different / (area.Width * area.Height);

        static SKBitmap Render(string path)
        {
            using var document = new Document(path);
            using var image = new MemoryStream();
            new PngDevice(new Resolution(72)).Process(document.Pages[1], image);
            return SKBitmap.Decode(image.ToArray());
        }
    }

    private static int Count(string path, string phrase)
    {
        using var document = new Document(path);
        var absorber = new TextFragmentAbsorber(phrase);
        document.Pages.Accept(absorber);
        return absorber.TextFragments.Count;
    }
}
