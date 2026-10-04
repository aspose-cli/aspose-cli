using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Pdf.Annotations;
using Aspose.Pdf.Facades;
using Aspose.Pdf.Forms;
using Aspose.Pdf.Text;
using Aspose.Pdf;
using CliPageRange = Aspose.Cli.Sdk.Addressing.PageRange;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

public sealed class PdfEngineTests
{
    [Theory]
    [InlineData(Sdk.Extensibility.Output.TableFormat.Plain)]
    [InlineData(Sdk.Extensibility.Output.TableFormat.Markdown)]
    public void Info_TableAndMarkdown_ShowEveryRequestedDetail(Sdk.Extensibility.Output.TableFormat format)
    {
        using var fixture = new PdfEngineFixture();
        string path = fixture.CreateDocument(pages: 2);
        using (var document = new Document(path))
        {
            var chapter = new OutlineItemCollection(document.Outlines) { Title = "Chapter", Destination = new FitExplicitDestination(document.Pages[1]) };
            chapter.Add(new OutlineItemCollection(document.Outlines) { Title = "Section", Destination = new FitExplicitDestination(document.Pages[2]) });
            document.Outlines.Add(chapter);
            document.EmbeddedFiles.Add("data.csv", new FileSpecification(new MemoryStream("a,b\n"u8.ToArray()), "data.csv", "Data") { Name = "data.csv", UnicodeName = "data.csv" });
            document.Info.Title = "Annual report";
            document.Save(path);
        }

        PdfInfoResult info = fixture.Engine.GetInfo(path, new PdfInfoRequest
        {
            Details = ["outline", "forms", "attachments", "fonts", "permissions", "signatures", "layers", "metadata"],
        });
        using var writer = new StringWriter();
        Output.PdfRenderers.Render(info, new Sdk.Extensibility.Output.TableSurface(writer, format));
        string text = writer.ToString();

        string heading = format == Sdk.Extensibility.Output.TableFormat.Markdown ? "### " : string.Empty;
        foreach (string section in new[] { "outline", "forms", "attachments", "fonts", "permissions", "signatures", "layers", "metadata" })
        {
            Assert.Contains(heading + section, text, StringComparison.Ordinal);
        }

        Assert.Contains("1/1", text, StringComparison.Ordinal);
        Assert.Contains("Section", text, StringComparison.Ordinal);
        Assert.Contains("data.csv", text, StringComparison.Ordinal);
        Assert.Contains(info.Fonts![0].Name, text, StringComparison.Ordinal);
        Assert.Contains("Annual report", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_FromHtml_TakesTheTitleFromTheHtmlAndInventsNoOtherMetadata()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("report.html");
        File.WriteAllText(input, """
            <!DOCTYPE html><html><head><meta charset="utf-8">
            <title>
              2026 Q3 运营报告 &amp; East
            </title><meta name="author" content="Operations"></head>
            <body><h1>Report</h1></body></html>
            """);

        PdfWriteResult result = fixture.Engine.Create(new NewPdfRequest { HtmlPath = input, OutputPath = fixture.File("report.pdf") });

        PdfInfoResult info = fixture.Engine.GetInfo(result.Output.Path, new PdfInfoRequest { Details = ["metadata"] });
        Assert.Equal("2026 Q3 运营报告 & East", info.Metadata!["title"]);
        Assert.Null(info.Metadata["author"]);
        Assert.Null(info.Metadata["subject"]);
        Assert.DoesNotContain(info.Metadata.Keys, static key => key.StartsWith("xmp:", StringComparison.Ordinal));
    }

    [Fact]
    public void Create_FromHtml_DrawsTheBoxOfEveryCheckBox()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("form.html");
        File.WriteAllText(input, """
            <html><body><form>
            <input type="checkbox" name="it"/> IT <input type="checkbox" name="office" checked/> Office
            </form></body></html>
            """);

        PdfWriteResult result = fixture.Engine.Create(new NewPdfRequest { HtmlPath = input, OutputPath = fixture.File("form.pdf") });

        using var document = new Document(result.Output.Path);
        CheckboxField[] boxes = [.. document.Form.Fields.OfType<CheckboxField>()];
        Assert.Equal(2, boxes.Length);
        Assert.All(boxes, static box => Assert.All(box.AllowedStates, state =>
            Assert.Contains(box.Appearance[$"N.{state}"].Contents, static drawn => drawn is Aspose.Pdf.Operators.ClosePathStroke or Aspose.Pdf.Operators.Stroke)));
    }

    [Fact]
    public void Create_FromMarkdown_InventsNoMetadata()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("notes.md");
        File.WriteAllText(input, "# Notes\n\nBody text.\n");

        PdfWriteResult result = fixture.Engine.Create(new NewPdfRequest { TextPath = input, Markdown = true, OutputPath = fixture.File("notes.pdf") });

        PdfInfoResult info = fixture.Engine.GetInfo(result.Output.Path, new PdfInfoRequest { Details = ["metadata"] });
        Assert.Null(info.Metadata!["title"]);
        Assert.Null(info.Metadata["author"]);
        Assert.Null(info.Metadata["subject"]);
    }

    [Theory]
    [InlineData("D:20261003100000+05'30'", "2026-10-03T04:30:00Z")]
    [InlineData("D:20261003011908Z00'00'", "2026-10-03T01:19:08Z")]
    [InlineData("D:20260930225834-07'00'", "2026-10-01T05:58:34Z")]
    [InlineData("D:20261003", "2026-10-03T00:00:00")]
    [InlineData("3 October 2026", "3 October 2026")]
    public void Info_ReadsADocumentDateAtTheOffsetItStates(string stored, string expected)
    {
        using var fixture = new PdfEngineFixture();
        string path = fixture.CreateDocument(pages: 1);
        using (var document = new Document(path))
        {
            document.Info["CreationDate"] = stored;
            document.Save(path);
        }

        PdfInfoResult result = fixture.Engine.GetInfo(path, new PdfInfoRequest { Details = ["metadata"] });

        Assert.Equal(expected, result.Metadata!["creationDate"]);
    }

    [Fact]
    public void Info_ProjectsAllM1Details()
    {
        using var fixture = new PdfEngineFixture();
        string path = fixture.CreateDocument(pages: 2);
        using (var document = new Document(path))
        {
            document.Pages[2].SetPageSize(792, 612);
            document.PageLabels.UpdateLabel(0, new PageLabel
            {
                NumberingStyle = NumberingStyle.NumeralsRomanLowercase,
                Prefix = "A-",
                StartingValue = 1,
            });
            var first = new OutlineItemCollection(document.Outlines)
            {
                Title = "First page",
                Destination = new FitExplicitDestination(document.Pages[1]),
            };
            first.Add(new OutlineItemCollection(document.Outlines)
            {
                Title = "Website",
                Action = new GoToURIAction("https://example.com/"),
            });
            document.Outlines.Add(first);
            document.Save(path);
        }

        var result = fixture.Engine.GetInfo(path, new PdfInfoRequest
        {
            IncludePreview = true,
            Details = ["outline", "forms", "attachments", "fonts", "permissions", "signatures", "layers", "metadata"],
        });

        Assert.Equal("pdf", result.Kind);
        Assert.Equal("pdf", result.Source.Format);
        Assert.Equal(2, result.Pdf.PageCount);
        Assert.Equal("none", result.Pdf.FormType);
        Assert.Equal(2, result.Pdf.DistinctPageSizes.Count);
        Assert.Equal(2, result.Pages!.Count);
        Assert.Collection(
            result.PageLabels!,
            label =>
            {
                Assert.Equal(1, label.StartPage);
                Assert.Equal("roman-lower", label.Style);
                Assert.Equal("A-", label.Prefix);
                Assert.Equal(1, label.StartingValue);
            });
        Assert.Collection(
            result.Outline!,
            item =>
            {
                Assert.Equal("First page", item.Title);
                Assert.Equal("1", item.Index);
                Assert.Equal(1, item.Page);
            },
            item =>
            {
                Assert.Equal(2, item.Level);
                Assert.Equal("1/1", item.Index);
                Assert.Null(item.Page);
            });
        Assert.Equal("none", result.Forms!.Type);
        Assert.Empty(result.Attachments!);
        Assert.NotEmpty(result.Fonts!);
        Assert.True(result.Permissions!.OwnerAccess);
        Assert.Empty(result.Signatures!);
        Assert.Empty(result.Layers!);
        Assert.Contains("producer", result.Metadata!.Keys);
    }

    [Theory]
    [InlineData("plain")]
    [InlineData("layout")]
    public void Read_ProjectsSelectedPagesInBothModes(string mode)
    {
        using var fixture = new PdfEngineFixture();
        string path = fixture.CreateDocument(pages: 3);

        var result = fixture.Engine.Read(path, new PdfReadRequest
        {
            Pages = CliPageRange.Parse("2-3"),
            Mode = mode,
            MaxCharacters = 20_000,
        });

        Assert.Equal(mode, result.Mode);
        Assert.Equal(3, result.PageCount);
        Assert.Equal(new ResultWindow { Unit = "page", Returned = 2, Total = 2, Truncated = false }, result.Window);
        Assert.Contains("Portable PDF page 2", result.Pages[0].Text, StringComparison.Ordinal);
        Assert.Contains("Portable PDF page 3", result.Pages[1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_StopsAtCharacterBudgetAndMarksTheCutPage()
    {
        using var fixture = new PdfEngineFixture();
        string path = fixture.CreateDocument(pages: 3);

        var result = fixture.Engine.Read(path, new PdfReadRequest { MaxCharacters = 24 });

        Assert.True(result.Window!.Truncated);
        Assert.Equal(24, result.Pages.Sum(static page => page.Text.Length));
        Assert.True(result.Pages[^1].Truncated);
    }

    [Fact]
    public void Passwords_DistinguishRequiredInvalidUserAndOwnerAccess()
    {
        using var fixture = new PdfEngineFixture();
        string path = fixture.File("protected.pdf");
        using (var document = new Document())
        {
            Page page = document.Pages.Add();
            page.Paragraphs.Add(new TextFragment("Protected"));
            document.Encrypt(
                "reader-secret",
                "owner-secret",
                DocumentPrivilege.ForbidAll,
                CryptoAlgorithm.AESx256,
                usePdf20: false);
            document.Save(path);
        }

        CliException required = Assert.Throws<CliException>(
            () => fixture.Engine.GetInfo(path, new PdfInfoRequest()));
        Assert.Equal(ErrorCodes.PasswordRequired, required.Code);
        CliException invalid = Assert.Throws<CliException>(
            () => fixture.Engine.GetInfo(path, new PdfInfoRequest { Password = "wrong" }));
        Assert.Equal(ErrorCodes.PasswordInvalid, invalid.Code);

        var user = fixture.Engine.GetInfo(path, new PdfInfoRequest
        {
            Password = "reader-secret",
            Details = ["permissions"],
        });
        Assert.Equal("user", user.Pdf.PasswordType);
        Assert.False(user.Permissions!.OwnerAccess);
        Assert.False(user.Permissions.Copy);

        var owner = fixture.Engine.GetInfo(path, new PdfInfoRequest
        {
            Password = "owner-secret",
            Details = ["permissions"],
        });
        Assert.Equal("owner", owner.Pdf.PasswordType);
        Assert.True(owner.Permissions!.OwnerAccess);
        Assert.True(owner.Permissions.Copy);
    }

    [Fact]
    public void Passwords_OwnerOnlyFixtureOpensNormallyAndRecognizesOwnerAccess()
    {
        using var fixture = new PdfEngineFixture();
        string path = fixture.CreateEncryptedDocument(
            string.Empty,
            "owner-secret",
            "owner-password-only.pdf");

        var normal = fixture.Engine.GetInfo(
            path,
            new PdfInfoRequest { Details = ["permissions"] });
        var owner = fixture.Engine.GetInfo(
            path,
            new PdfInfoRequest { Password = "owner-secret", Details = ["permissions"] });

        Assert.True(normal.Pdf.Encrypted);
        // The file has only an owner password, so it opened without one.
        Assert.Equal("none", normal.Pdf.PasswordType);
        Assert.False(normal.Permissions!.HasOpenPassword);
        Assert.True(normal.Permissions.HasOwnerPassword);
        Assert.False(normal.Permissions.OwnerAccess);
        Assert.Equal("owner", owner.Pdf.PasswordType);
        Assert.True(owner.Permissions!.OwnerAccess);
    }

    [Fact]
    public void Load_RejectsRenamedNonPdfBytes()
    {
        using var fixture = new PdfEngineFixture();
        string path = fixture.File("renamed.pdf");
        File.WriteAllText(path, "not really a PDF");

        CliException exception = Assert.Throws<CliException>(
            () => fixture.Engine.GetInfo(path, new PdfInfoRequest()));

        Assert.Equal(ErrorCodes.FileCorrupt, exception.Code);
    }

    [Fact]
    public void Read_OneHundredPageFixtureRemainsWindowed()
    {
        using var fixture = new PdfEngineFixture();
        string path = fixture.CreateRawDocument("hundred-pages.pdf", pages: 100);

        var result = fixture.Engine.Read(path, new PdfReadRequest { MaxCharacters = 1 });

        Assert.Equal(new ResultWindow { Unit = "page", Returned = 1, Total = 100, Truncated = true }, result.Window);
        Assert.Single(result.Pages);
    }

    [LicensedFact]
    public void Info_DisclosesThePagePreviewCap()
    {
        using var fixture = new PdfEngineFixture();
        string path = fixture.CreateRawDocument("many-pages.pdf", pages: 21);

        PdfInfoResult info = fixture.Engine.GetInfo(path, new PdfInfoRequest { IncludePreview = true });

        Assert.Equal(20, info.Pages!.Count);
        Warning warning = Assert.Single(info.Warnings!);
        Assert.Equal(WarningCodes.ListTruncated, warning.Code);
        Assert.Equal("pages", warning.Location);
        Assert.Contains("first 20 of 21", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Info_XfaIsExplicitlyFlaggedReadOnly()
    {
        using var fixture = new PdfEngineFixture();
        string path = fixture.File("xfa.pdf");
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
                  <template xmlns="http://www.xfa.org/schema/xfa-template/3.3/">
                    <subform name="form1"/>
                  </template>
                  <datasets xmlns="http://www.xfa.org/schema/xfa-data/1.0/">
                    <xfa:data xmlns:xfa="http://www.xfa.org/schema/xfa-data/1.0/"/>
                  </datasets>
                </xdp:xdp>
                """);
            document.Form.AssignXfa(xfa);
            document.Save(path);
        }

        var result = fixture.Engine.GetInfo(path, new PdfInfoRequest { Details = ["forms"] });

        Assert.Equal("xfa", result.Pdf.FormType);
        Assert.Equal("xfa", result.Forms!.Type);
        Assert.True(result.Forms.ReadOnly);
    }

    [Fact]
    public void Convert_HtmlIsOneAtomicSelfContainedArtifact()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("html-source.pdf", 1);
        string output = fixture.File("self-contained.html");

        var result = fixture.Engine.Convert(input, new PdfConvertRequest
        {
            TargetFormatId = "html",
            OutputPath = output,
        });

        Assert.Single(result.Outputs);
        Assert.True(File.Exists(output));
        Assert.Equal(
            ["html-source.pdf", "self-contained.html"],
            Directory.GetFiles(fixture.Temp.Path).Select(Path.GetFileName).Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(null, 3)]
    [InlineData("2-3", 2)]
    public void Convert_DocumentFormatsKeepTheDocumentProperties(string? pages, int pageCount)
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("titled.pdf", 3);
        using (var document = new Document(input))
        {
            document.Info.Title = "Quarterly title";
            document.Info.Subject = "Quarterly subject";
            document.Save(input);
        }
        string output = fixture.File("titled.xps");

        fixture.Engine.Convert(input, new PdfConvertRequest
        {
            TargetFormatId = "xps",
            OutputPath = output,
            Pages = pages is null ? null : CliPageRange.Parse(pages),
        });

        using var package = System.IO.Compression.ZipFile.OpenRead(output);
        using var core = new StreamReader(package.GetEntry("docProps/core.xml")!.Open());
        string properties = core.ReadToEnd();
        Assert.Contains("Quarterly title", properties, StringComparison.Ordinal);
        Assert.Contains("Quarterly subject", properties, StringComparison.Ordinal);
        Assert.Equal(pageCount, package.Entries.Count(static entry => entry.FullName.EndsWith(".fpage", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("1-2", true)]
    [InlineData("1-3", false)]
    public void Convert_DocumentFormatsDiscloseNavigationToUnselectedPages(string pages, bool degraded)
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("chapters.pdf", 3);
        using (var document = new Document(input))
        {
            document.Outlines.Add(new OutlineItemCollection(document.Outlines)
            {
                Title = "Appendix",
                Destination = new FitExplicitDestination(document.Pages[3]),
            });
            document.Save(input);
        }

        PdfConvertResult result = fixture.Engine.Convert(input, new PdfConvertRequest
        {
            TargetFormatId = "docx",
            OutputPath = fixture.File("chapters.docx"),
            Pages = CliPageRange.Parse(pages),
        });

        Warning? warning = result.Warnings?.SingleOrDefault(static item => item.Code == "NAVIGATION_DEGRADED");
        Assert.Equal(degraded, warning is not null);
        if (warning is not null)
        {
            Assert.StartsWith("1 bookmark(s), 0 link(s)", warning.Message, StringComparison.Ordinal);
            Assert.Contains("--pages did not select", warning.Message, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("docx")]
    [InlineData("xlsx")]
    [InlineData("pptx")]
    [InlineData("epub")]
    [InlineData("txt")]
    [InlineData("md")]
    [InlineData("svg")]
    [InlineData("xps")]
    [InlineData("pdfa-1b")]
    [InlineData("pdfa-2b")]
    [InlineData("pdfa-3b")]
    [InlineData("png")]
    [InlineData("jpeg")]
    [InlineData("tiff")]
    public void Convert_ProducesEveryFrozenM1Target(string format)
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument(pages: 1);
        string output = fixture.File("output" + PdfFormats.Definitions.ExtensionFor(format));

        var result = fixture.Engine.Convert(input, new PdfConvertRequest
        {
            TargetFormatId = format,
            OutputPath = output,
        });

        Assert.NotEmpty(result.Outputs);
        Assert.All(result.Outputs, item =>
        {
            Assert.True(File.Exists(item.Path), item.Path);
            Assert.True(item.SizeBytes > 0);
        });
        if (fixture.LicenseState != Sdk.Licensing.LicenseState.Licensed)
        {
            Assert.Contains(result.Warnings!, static warning => warning.Code == WarningCodes.EvalMode);
        }
    }

    /// <summary>A single page is refused an output without an extension, as several pages are.</summary>
    [Theory]
    [InlineData("render")]
    [InlineData("convert")]
    public void ImageOutput_OfOnePageWithoutAnExtensionIsRefused(string command)
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument(pages: 2);
        string output = fixture.File("pages");

        CliException error = Assert.Throws<CliException>(() => command == "render"
            ? fixture.Engine.Render(input, new PdfRenderRequest { TargetFormatId = "png", OutputPath = output })
            : (object)fixture.Engine.Convert(input, new PdfConvertRequest { TargetFormatId = "png", OutputPath = output, Pages = CliPageRange.Parse("1") }));

        Assert.Equal(ErrorCodes.OptionInvalid, error.Code);
        Assert.Contains(output + ".png", error.Hint, StringComparison.Ordinal);
        Assert.False(Path.Exists(output));
    }
}
