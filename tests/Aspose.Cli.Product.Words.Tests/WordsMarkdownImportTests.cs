using Aspose.Cli.Sdk.Licensing;
using Aspose.Words;
using Aspose.Words.Saving;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>create, insert_markdown and Markdown headers import Markdown the same way.</summary>
public sealed class WordsMarkdownImportTests
{
    [Fact]
    public void BuiltInDesign_KeepsTitleAndSubtitleOutOfTheTableOfContents()
    {
        using var fixture = new WordsFixture();
        string markdown = fixture.Temp.File("bid.md");
        File.WriteAllText(markdown, "# Chapter one\n\nBody text.\n");
        string created = fixture.Temp.File("bid.docx");
        WordsCreate.Run(fixture.Session, new WordsCreateRequest { Output = TestOutput.At(created), Markdown = new SourceFile("--from-markdown", markdown) });
        string output = fixture.Temp.File("bid-toc.docx");

        WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = created,
            Batch = new WordsOpsBatch
            {
                Ops =
                [
                    new InsertParagraphsOp
                    {
                        At = new WordsTarget { Block = 1 },
                        Position = "before",
                        Paragraphs = [new WordsParagraphInput { Text = "Contents", Style = "Title" }, new WordsParagraphInput { Text = "Bid 2026", Style = "Subtitle" }],
                    },
                    new InsertTocOp { At = new WordsTarget { Block = 1 }, Position = "before" },
                ],
            },
            Output = TestOutput.At(output),
        });

        WordsInspectResult info = WordsInspect.Run(fixture.Session, new WordsInspectRequest { Input = output, Details = ["outline"] });
        Assert.Equal(["Chapter one"], info.Outline!.Select(static item => item.Text));
        string toc = new Document(output).Range.Fields.Cast<Aspose.Words.Fields.Field>()
            .Single(static field => field.Type == Aspose.Words.Fields.FieldType.FieldTOC).Result;
        Assert.Contains("Chapter one", toc, StringComparison.Ordinal);
        Assert.DoesNotContain("Contents", toc, StringComparison.Ordinal);
        Assert.DoesNotContain("Bid 2026", toc, StringComparison.Ordinal);
    }

    /// <summary>
    /// A plain first line restyled as Title in the batch that inserts the table of contents
    /// stays out of it, while the Markdown's <c>#</c> chapters stay Heading 1.
    /// </summary>
    [Fact]
    public void FirstLineSetToTitle_StaysOutOfATableOfContentsInsertedInTheSameBatch()
    {
        using var fixture = new WordsFixture();
        string markdown = fixture.Temp.File("bid.md");
        File.WriteAllText(markdown, "Smart Campus Bid\n\n# Company overview\n\nWe build campuses.\n\n# Pricing\n\nSee the table.\n");
        string created = fixture.Temp.File("bid.docx");
        WordsCreate.Run(fixture.Session, new WordsCreateRequest { Output = TestOutput.At(created), Markdown = new SourceFile("--from-markdown", markdown) });
        string output = fixture.Temp.File("bid-toc.docx");

        WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = created,
            Batch = new WordsOpsBatch
            {
                Ops =
                [
                    new SetStyleOp { Target = new WordsTarget { Find = "Smart Campus Bid" }, Style = "Title" },
                    new InsertTocOp { At = new WordsTarget { Find = "Smart Campus Bid" }, Position = "after" },
                ],
            },
            Output = TestOutput.At(output),
        });

        WordsInspectResult info = WordsInspect.Run(fixture.Session, new WordsInspectRequest { Input = output, Details = ["outline"] });
        Assert.Equal([("Company overview", 1), ("Pricing", 1)], info.Outline!.Select(static item => (item.Text, item.HeadingLevel)));
        var document = new Document(output);
        Assert.Equal("Title", Body(document).Single(static paragraph => paragraph.GetText().Trim() == "Smart Campus Bid").ParagraphFormat.StyleName);
        string toc = document.Range.Fields.Cast<Aspose.Words.Fields.Field>()
            .Single(static field => field.Type == Aspose.Words.Fields.FieldType.FieldTOC).Result;
        Assert.Contains("Company overview", toc, StringComparison.Ordinal);
        Assert.DoesNotContain("Smart Campus Bid", toc, StringComparison.Ordinal);
    }

    /// <summary>
    /// The Markdown footnote syntax, <c>[^id]</c> in the text and <c>[^id]: text</c> below it,
    /// adds a real footnote through both <c>create --from-markdown</c> and <c>insert_markdown</c>:
    /// a footnotes-scope search reads its text, and the body keeps neither marker nor definition.
    /// </summary>
    [Fact]
    public void MarkdownFootnoteSyntax_AddsARealFootnoteOnCreateAndInsertMarkdown()
    {
        using var fixture = new WordsFixture();
        string markdown = fixture.Temp.File("bid.md");
        // Definitions at the end keep every heading a heading (WORDS-MD-FOOTNOTE-HEADING).
        File.WriteAllText(markdown,
            "# Bid\n\nCertified under ISO 9001[^1].\n\n# Pricing\n\nPrices in CNY[^2].\n\n# Schedule\n\nDone.\n\n"
            + "[^1]: Certificate CN-2026-001.\n\n[^2]: Before tax.\n");
        string created = fixture.Temp.File("bid.docx");
        WordsCreate.Run(fixture.Session, new WordsCreateRequest { Output = TestOutput.At(created), Markdown = new SourceFile("--from-markdown", markdown) });
        WordsInspectResult info = WordsInspect.Run(fixture.Session, new WordsInspectRequest { Input = created, Details = ["outline"] });
        Assert.Equal(["Bid", "Pricing", "Schedule"], info.Outline!.Select(static item => item.Text));
        string output = fixture.Temp.File("bid-noted.docx");

        WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = created,
            Batch = new WordsOpsBatch
            {
                Ops =
                [
                    new InsertMarkdownOp
                    {
                        At = new WordsTarget { Find = "Certified under" }, Position = "after",
                        Markdown = "补充说明[^n]\n\n[^n]: 脚注：参见附件 A。",
                    },
                ],
            },
            Output = TestOutput.At(output),
        });

        Assert.Equal(["Before tax.", "Certificate CN-2026-001."], FootnoteLines(fixture, created).Order(StringComparer.Ordinal));
        Assert.Equal(["Before tax.", "Certificate CN-2026-001.", "脚注：参见附件 A。"], FootnoteLines(fixture, output).Order(StringComparer.Ordinal));
        var document = new Document(output);
        Assert.Equal(3, document.GetChildNodes(NodeType.Footnote, true).Count);
        Assert.DoesNotContain(Body(document), static paragraph => paragraph.GetText().Contains("[^", StringComparison.Ordinal));
    }

    private static string[] FootnoteLines(WordsFixture fixture, string input) =>
        [.. WordsSearch.Run(fixture.Session, new WordsSearchRequest
            {
                Input = input,
                Query = new Aspose.Cli.Sdk.Text.SearchQuery(
                    Aspose.Cli.Sdk.Text.TextSearch.Create(".+", regex: true, caseSensitive: false), 100, WordsTextScopes.Footnotes),
            })
            .Hits.Select(static hit => hit.Text.Trim())];

    [Fact]
    public void Create_ClosesEmphasisAfterPunctuationOnlyAsCommonMarkDoes()
    {
        using var fixture = new WordsFixture();
        string markdown = fixture.Temp.File("struck.md");
        // A closing delimiter after punctuation needs a space or punctuation after it; HTML tags do not.
        File.WriteAllText(markdown, "施行。~~原《守则》~~同时废止。\n\n施行。<del>原《守则》</del>同时废止。\n");
        string output = fixture.Temp.File("struck.docx");

        WordsCreate.Run(fixture.Session, new WordsCreateRequest { Output = TestOutput.At(output), Markdown = new SourceFile("--from-markdown", markdown) });

        Paragraph[] paragraphs = [.. new Document(output).FirstSection.Body.Paragraphs.Cast<Paragraph>().TakeLast(2)];
        Assert.Equal("施行。~~原《守则》~~同时废止。", paragraphs[0].GetText().TrimEnd('\r'));
        Assert.Equal("原《守则》", Assert.Single(paragraphs[1].Runs.Cast<Run>(), static run => run.Font.StrikeThrough).Text);
    }

    [Fact]
    public void Create_WithoutTemplate_BreaksChineseLinesByTheEastAsianRules()
    {
        using var fixture = new WordsFixture();
        string markdown = fixture.Temp.File("clauses.md");
        // Half of the characters are commas or full stops, which no line may start with.
        File.WriteAllText(markdown, string.Concat(Enumerable.Repeat("甲，乙。", 120)) + "\n");
        string output = fixture.Temp.File("clauses.docx");

        WordsCreate.Run(fixture.Session, new WordsCreateRequest { Output = TestOutput.At(output), Markdown = new SourceFile("--from-markdown", markdown) });

        IReadOnlyList<string> lines = WordsFixture.LayoutLines(new Document(output));
        Assert.True(lines.Count > 4, string.Join(" | ", lines));
        Assert.False(lines.Any(static line => line.StartsWith('，') || line.StartsWith('。')), string.Join(" | ", lines));
    }

    [Fact]
    public void Create_WithoutTemplate_TakesStylesPageSetupAndFooterFromTheBuiltInDesign()
    {
        using var fixture = new WordsFixture();
        string markdown = fixture.Temp.File("brief.md");
        File.WriteAllText(markdown, "# Brief\n\nPlain **bold** and `code`.\n");
        string output = fixture.Temp.File("brief.docx");

        WordsCreate.Run(fixture.Session, new WordsCreateRequest { Output = TestOutput.At(output), Markdown = new SourceFile("--from-markdown", markdown) });

        var document = new Document(output);
        Section section = Assert.Single(document.Sections.Cast<Section>());
        Assert.Equal(PaperSize.A4, section.PageSetup.PaperSize);
        Assert.Contains(
            section.HeadersFooters[HeaderFooterType.FooterPrimary].Range.Fields.Cast<Aspose.Words.Fields.Field>(),
            static field => field.Type == Aspose.Words.Fields.FieldType.FieldPage);
        // Evaluation mode may add a banner paragraph; select the authored paragraphs by text.
        Paragraph[] paragraphs = section.Body.Paragraphs.Cast<Paragraph>().ToArray();
        Paragraph heading = Assert.Single(paragraphs, static paragraph => paragraph.GetText().Trim() == "Brief");
        Assert.Equal(StyleIdentifier.Heading1, heading.ParagraphFormat.StyleIdentifier);
        Assert.True(heading.Runs[0].Font.Bold);
        Run[] runs = Assert.Single(paragraphs, static paragraph => paragraph.GetText().StartsWith("Plain", StringComparison.Ordinal))
            .Runs.Cast<Run>().ToArray();
        Assert.Equal(["bold"], runs.Where(static run => run.Font.Bold).Select(static run => run.Text));
        Assert.Equal("InlineCode", Assert.Single(runs, static run => run.Text == "code").Font.StyleName);
        Assert.DoesNotContain(paragraphs, static paragraph => !paragraph.HasChildNodes);
    }
    [LicensedFact]
    public void Create_FitsAnImageWiderThanThePageToTheTextColumn()
    {
        using var fixture = new WordsFixture();
        string chart = fixture.Temp.File("chart.png");
        using (var bitmap = new SkiaSharp.SKBitmap(1367, 1150))
        using (SkiaSharp.SKImage image = SkiaSharp.SKImage.FromBitmap(bitmap))
        using (SkiaSharp.SKData png = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100))
        using (FileStream file = File.Create(chart))
        {
            png.SaveTo(file);
        }

        string markdown = fixture.Temp.File("report.md");
        File.WriteAllText(markdown, "# Report\n\n![Completion](chart.png)\n");
        string output = fixture.Temp.File("report.docx");

        WordsCreate.Run(fixture.Session, new WordsCreateRequest { Output = TestOutput.At(output), Markdown = new SourceFile("--from-markdown", markdown) });

        var document = new Document(output);
        PageSetup page = document.FirstSection.PageSetup;
        Aspose.Words.Drawing.Shape picture = Assert.Single(document.GetChildNodes(NodeType.Shape, true).OfType<Aspose.Words.Drawing.Shape>());
        Assert.InRange(picture.Width, 1, page.PageWidth - page.LeftMargin - page.RightMargin + 0.01);
        Assert.Equal(1150d / 1367d, picture.Height / picture.Width, 2);
    }

    [Fact]
    public void InspectFonts_ReportsTheFontsTheTextUses()
    {
        using var fixture = new WordsFixture();
        string markdown = fixture.Temp.File("fonts.md");
        File.WriteAllText(markdown, "# Title\n\nHello 你好\n");
        string output = fixture.Temp.File("fonts.docx");
        WordsCreate.Run(fixture.Session, new WordsCreateRequest { Output = TestOutput.At(output), Markdown = new SourceFile("--from-markdown", markdown) });

        WordsInspectResult info = WordsInspect.Run(fixture.Session, new WordsInspectRequest { Input = output, Details = ["fonts"] });

        Assert.Equal(["Calibri", "Microsoft YaHei"], info.Fonts);
    }

    [Fact]
    public void BuiltInDesign_FontTableNamesExactlyTheFontsItsStylesUse()
    {
        Document template = Engine.Mapping.WordsDocumentLoader.OpenDefaultTemplate();

        string[] styleFonts = template.Styles.Cast<Style>()
            .Where(static style => style.Type is StyleType.Paragraph or StyleType.Character)
            .SelectMany(static style => new[] { style.Font.Name, style.Font.NameFarEast, style.Font.NameBi, style.Font.NameOther })
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(["Calibri", "Microsoft YaHei"], styleFonts);
        Assert.Equal(styleFonts, template.FontInfos.Cast<Aspose.Words.Fonts.FontInfo>()
            .Select(static font => font.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void BuiltInDesign_CarriesNoGeneratorMetadata()
    {
        using Stream stream = typeof(WordsSession).Assembly.GetManifestResourceStream("Templates/default-a4.docx")!;
        using var package = new System.IO.Compression.ZipArchive(stream);
        foreach (System.IO.Compression.ZipArchiveEntry entry in package.Entries)
        {
            using var reader = new StreamReader(entry.Open());
            Assert.DoesNotContain("Aspose", reader.ReadToEnd(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void InsertMarkdownAndMarkdownHeaders_TakeTheDestinationStyles()
    {
        using var fixture = new WordsFixture();
        string input = fixture.CreateReport();
        var source = new Document(input);
        source.Styles[StyleIdentifier.Heading2].Font.Color = System.Drawing.Color.FromArgb(0, 0, 128);
        source.Save(input, SaveFormat.Docx);

        string output = fixture.Temp.File("imported.docx");
        WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = input,
            Batch = new WordsOpsBatch
            {
                Ops =
                [
                    new InsertMarkdownOp
                    {
                        At = new WordsTarget { Find = "Operations remained" }, Position = "after", Markdown = "## Outlook\n\nPlain *emphasis*.",
                    },
                    new SetHeaderOp { Markdown = "## Confidential" },
                ],
            },
            Output = TestOutput.At(output),
        });

        var document = new Document(output);
        Assert.Null(document.Styles["Heading 2_0"]);
        Paragraph outlook = Body(document).Single(static paragraph => paragraph.GetText().StartsWith("Outlook", StringComparison.Ordinal));
        Assert.Equal("Heading 2", outlook.ParagraphFormat.StyleName);
        Assert.False(outlook.Runs[0].Font.Bold && outlook.Runs[0].Font.Italic);
        Run emphasis = Body(document).SelectMany(static paragraph => paragraph.Runs.Cast<Run>())
            .Single(static run => run.Text == "emphasis");
        Assert.True(emphasis.Font.Italic);
        Paragraph header = WordsFixture.FirstAuthoredParagraph(document.FirstSection.HeadersFooters[HeaderFooterType.HeaderPrimary]);
        Assert.Equal("Confidential", header.GetText().Trim());
        Assert.Equal("Heading 2", header.ParagraphFormat.StyleName);
    }

    [Theory]
    [InlineData("first")]
    [InlineData("even")]
    public void FirstAndEvenHeaders_TurnOnTheSectionSettingThatShowsThem(string kind)
    {
        using var fixture = new WordsFixture();
        string input = fixture.CreateReport();
        string output = fixture.Temp.File($"header-{kind}.docx");

        WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = input,
            Batch = new WordsOpsBatch
            {
                Ops = [new SetHeaderOp { Kind = kind, Paragraphs = [new WordsParagraphInput { Text = "Header" }] }],
            },
            Output = TestOutput.At(output),
        });

        PageSetup setup = new Document(output).FirstSection.PageSetup;
        Assert.Equal(kind == "first", setup.DifferentFirstPageHeaderFooter);
        Assert.Equal(kind == "even", setup.OddAndEvenPagesHeaderFooter);
    }

    [Fact]
    public void PlainHeaderAndFooterParagraphs_KeepTheFormatOfWhatTheyReplace()
    {
        using var fixture = new WordsFixture();
        var source = new Document();
        var builder = new DocumentBuilder(source);
        builder.Write("Body");
        builder.MoveToHeaderFooter(HeaderFooterType.HeaderPrimary);
        builder.ParagraphFormat.Alignment = ParagraphAlignment.Right;
        builder.Font.Size = 8;
        builder.Font.Name = "Arial";
        builder.Write("Old header");
        string input = fixture.Temp.File("aligned-header.docx");
        source.Save(input);
        string output = fixture.Temp.File("aligned-header-out.docx");

        WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = input,
            Batch = new WordsOpsBatch
            {
                Ops = [new SetHeaderOp { Paragraphs = [new WordsParagraphInput { Text = "New header" }] }, new SetFooterOp { Paragraphs = [new WordsParagraphInput { Text = "New footer" }] }],
            },
            Output = TestOutput.At(output),
        });

        Section section = new Document(output).FirstSection;
        Paragraph header = WordsFixture.FirstAuthoredParagraph(section.HeadersFooters[HeaderFooterType.HeaderPrimary]);
        Assert.Equal("New header", header.GetText().Trim());
        Assert.Equal(ParagraphAlignment.Right, header.ParagraphFormat.Alignment);
        Assert.Equal(8, header.Runs[0].Font.Size);
        Assert.Equal("Arial", header.Runs[0].Font.Name);
        // With nothing to replace, a footer takes Word's Footer style.
        Paragraph footer = section.HeadersFooters[HeaderFooterType.FooterPrimary].Paragraphs.Cast<Paragraph>()
            .Single(static paragraph => paragraph.GetText().Trim() == "New footer");
        Assert.Equal(StyleIdentifier.Footer, footer.ParagraphFormat.StyleIdentifier);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(2)]
    public void PlainHeaderParagraphs_KeepTheFormatOfTheHeaderASectionContinues(int? target)
    {
        using var fixture = new WordsFixture();
        var source = new Document();
        var builder = new DocumentBuilder(source);
        builder.Write("First");
        builder.InsertBreak(BreakType.SectionBreakNewPage);
        builder.Write("Second");
        builder.MoveToSection(0);
        builder.MoveToHeaderFooter(HeaderFooterType.HeaderPrimary);
        builder.ParagraphFormat.Alignment = ParagraphAlignment.Right;
        builder.Write("Old header");
        string input = fixture.Temp.File("continued-header.docx");
        source.Save(input);
        string output = fixture.Temp.File("continued-header-out.docx");

        WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = input,
            Batch = new WordsOpsBatch
            {
                Ops = [new SetHeaderOp { Paragraphs = [new WordsParagraphInput { Text = "New header" }], Section = target }],
            },
            Output = TestOutput.At(output),
        });

        Section second = new Document(output).Sections[1];
        Paragraph header = WordsFixture.FirstAuthoredParagraph(second.HeadersFooters[HeaderFooterType.HeaderPrimary]);
        Assert.Equal("New header", header.GetText().Trim());
        // Evaluation mode saved the input with an empty header of the second section's own.
        if (fixture.LicenseState == LicenseState.Licensed)
        {
            Assert.Equal(ParagraphAlignment.Right, header.ParagraphFormat.Alignment);
        }
    }

    /// <summary>
    /// A Markdown ordered list inserted after a numbered list of the document, with a plain
    /// paragraph between them, keeps the start number and delimiter the Markdown wrote instead
    /// of continuing the document's list.
    /// </summary>
    [Theory]
    [InlineData(".")]
    [InlineData(")")]
    public void InsertMarkdown_OrderedList_KeepsTheStartNumberTheMarkdownWrote(string delimiter)
    {
        using var fixture = new WordsFixture();
        string markdown = fixture.Temp.File("terms.md");
        File.WriteAllText(markdown, "# Terms\n\n1. Delivery\n2. Acceptance\n3. Payment\n\nThe following continue item 4.\n");
        string input = fixture.Temp.File("terms.docx");
        WordsCreate.Run(fixture.Session, new WordsCreateRequest { Output = TestOutput.At(input), Markdown = new SourceFile("--from-markdown", markdown) });
        string output = fixture.Temp.File("terms-inserted.docx");

        WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = input,
            Batch = new WordsOpsBatch
            {
                Ops =
                [
                    new InsertMarkdownOp
                    {
                        At = new WordsTarget { Find = "The following continue" },
                        Position = "after",
                        Markdown = $"5{delimiter} Supplement\n6{delimiter} Invoice\n",
                    },
                    new SetHeaderOp { Markdown = $"7{delimiter} Header item\n" },
                ],
            },
            Output = TestOutput.At(output),
        });

        Assert.Equal(["1.", "2.", "3.", $"5{delimiter}", $"6{delimiter}"], ListLabels(output));
        var document = new Document(output);
        document.UpdateListLabels();
        Paragraph header = WordsFixture.FirstAuthoredParagraph(document.FirstSection.HeadersFooters[HeaderFooterType.HeaderPrimary]);
        Assert.Equal($"7{delimiter}", header.ListLabel.LabelString);
    }

    /// <summary>
    /// <c>create --template</c> with a template that has a numbered list keeps the start number
    /// of the Markdown's ordered list.
    /// </summary>
    [Fact]
    public void Create_WithATemplateThatHasANumberedList_KeepsTheStartNumberTheMarkdownWrote()
    {
        using var fixture = new WordsFixture();
        string templateMarkdown = fixture.Temp.File("template.md");
        File.WriteAllText(templateMarkdown, "1. Delivery\n2. Acceptance\n3. Payment\n");
        string template = fixture.Temp.File("template.docx");
        WordsCreate.Run(fixture.Session, new WordsCreateRequest { Output = TestOutput.At(template), Markdown = new SourceFile("--from-markdown", templateMarkdown) });
        string markdown = fixture.Temp.File("supplement.md");
        File.WriteAllText(markdown, "5. Supplement\n6. Invoice\n");
        string output = fixture.Temp.File("supplement.docx");

        WordsCreate.Run(fixture.Session, new WordsCreateRequest { Output = TestOutput.At(output), Markdown = new SourceFile("--from-markdown", markdown), Template = new SourceFile("--template", template) });

        Assert.Equal(["5.", "6."], ListLabels(output));
    }

    /// <summary>
    /// The importer asks for an omitted image more than once; the warning counts and names each
    /// reference once, as the Markdown wrote it, with its reason.
    /// </summary>
    [Fact]
    public void OmittedMarkdownImages_AreCountedAndNamedOnce()
    {
        using var fixture = new WordsFixture();
        string input = Directory.CreateDirectory(fixture.Temp.File("in")).FullName;
        string shared = Directory.CreateDirectory(fixture.Temp.File("shared")).FullName;
        File.WriteAllBytes(Path.Combine(shared, "logo.png"), Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGP4z8DwHwAFAAH/iZk9HQAAAABJRU5ErkJggg=="));
        string markdown = Path.Combine(input, "doc.md");
        File.WriteAllText(markdown, "# Title\n\n![gone](images/missing.png)\n\n![far](../shared/logo.png)\n\n<img src=\"x.png\">\n");

        WordsCreateResult created = WordsCreate.Run(fixture.Session, new WordsCreateRequest
        {
            Output = TestOutput.At(Path.Combine(input, "doc.docx")),
            Markdown = new SourceFile("--from-markdown", markdown),
        });

        Warning warning = Assert.Single(created.Warnings ?? [], static warning => warning.Code == WarningCodes.RemoteResourcesBlocked);
        Assert.StartsWith("3 resource(s)", warning.Message, StringComparison.Ordinal);
        foreach (string named in new[] { "images/missing.png (not found)", "../shared/logo.png (outside the input folder)", "x.png (not found)" })
        {
            Assert.Equal(warning.Message.IndexOf(named, StringComparison.Ordinal), warning.Message.LastIndexOf(named, StringComparison.Ordinal));
            Assert.Contains(named, warning.Message, StringComparison.Ordinal);
        }

        Assert.DoesNotContain(shared, warning.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// An image the resource policy omits, one that does not exist or lies outside the Markdown's
    /// directory, leaves no link in the output, through both <c>create --from-markdown</c> and
    /// <c>insert_markdown</c>: no relationship names a local path or an external picture, while
    /// the picture beside the Markdown is stored.
    /// </summary>
    [Fact]
    public void OmittedMarkdownImage_LeavesNoLinkToALocalPath()
    {
        using var fixture = new WordsFixture();
        string root = fixture.Temp.File("root");
        string input = Directory.CreateDirectory(Path.Combine(root, "in")).FullName;
        string outside = Directory.CreateDirectory(Path.Combine(root, "outside")).FullName;
        string output = Directory.CreateDirectory(Path.Combine(root, "out")).FullName;
        byte[] png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGP4z8DwHwAFAAH/iZk9HQAAAABJRU5ErkJggg==");
        File.WriteAllBytes(Path.Combine(input, "beside.png"), png);
        File.WriteAllBytes(Path.Combine(output, "beside.png"), png);
        string sentinel = Path.Combine(outside, "sentinel.png");
        File.WriteAllBytes(sentinel, png);
        string markdown = Path.Combine(input, "doc.md");
        File.WriteAllText(markdown,
            "# Title\n\n![kept](beside.png)\n\n![gone](missing.png)\n\n![far](../outside/sentinel.png)\n\n<img src=\"x\">\n");
        string created = Path.Combine(output, "created.docx");
        string edited = Path.Combine(output, "edited.docx");

        WordsCreateResult create = WordsCreate.Run(fixture.Session, new WordsCreateRequest
        {
            Output = TestOutput.At(created),
            Markdown = new SourceFile("--from-markdown", markdown),
        });
        WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = created,
            Batch = new WordsOpsBatch
            {
                Ops =
                [
                    new InsertMarkdownOp
                    {
                        At = new WordsTarget { Block = 1 }, Position = "after",
                        Markdown = $"![kept](beside.png)\n\n![gone](missing.png)\n\n![far](<{sentinel}>)\n",
                    },
                ],
            },
            Output = TestOutput.At(edited),
        });

        Assert.Contains(create.Warnings ?? [], static warning => warning.Code == WarningCodes.RemoteResourcesBlocked);
        foreach (string path in new[] { created, edited })
        {
            using System.IO.Compression.ZipArchive archive = System.IO.Compression.ZipFile.OpenRead(path);
            foreach (System.IO.Compression.ZipArchiveEntry entry in archive.Entries.Where(static entry => entry.FullName.EndsWith(".rels", StringComparison.Ordinal)))
            {
                using var reader = new StreamReader(entry.Open());
                string rels = reader.ReadToEnd();
                foreach (string form in new[] { root, root.Replace('\\', '/'), new Uri(root).AbsoluteUri, Environment.UserName })
                {
                    Assert.DoesNotContain(form, rels, StringComparison.OrdinalIgnoreCase);
                }

                Assert.DoesNotContain(System.Xml.Linq.XDocument.Parse(rels).Root!.Elements(), static relationship =>
                    (string?)relationship.Attribute("TargetMode") == "External"
                    && ((string?)relationship.Attribute("Type"))?.EndsWith("/image", StringComparison.Ordinal) == true);
            }

            Assert.Contains(archive.Entries, static entry => entry.FullName.StartsWith("word/media/", StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// insert_markdown drops links only from the pictures it imports: a linked picture the
    /// document already held keeps its link.
    /// </summary>
    [Fact]
    public void InsertMarkdown_KeepsALinkedPictureTheDocumentAlreadyHeld()
    {
        using var fixture = new WordsFixture();
        var source = new Document();
        var builder = new DocumentBuilder(source);
        builder.Writeln("Clause one.");
        var linked = new Aspose.Words.Drawing.Shape(source, Aspose.Words.Drawing.ShapeType.Image)
        {
            Width = 40,
            Height = 40,
            WrapType = Aspose.Words.Drawing.WrapType.Inline,
        };
        linked.ImageData.SourceFullName = "logo.png";
        builder.InsertNode(linked);
        string input = fixture.Temp.File("linked.docx");
        source.Save(input, SaveFormat.Docx);
        string output = fixture.Temp.File("linked-inserted.docx");

        WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = input,
            Batch = new WordsOpsBatch
            {
                Ops = [new InsertMarkdownOp { At = new WordsTarget { Block = 1 }, Position = "after", Markdown = "Added **text**.\n\n![gone](missing.png)\n" }],
            },
            Output = TestOutput.At(output),
        });

        Aspose.Words.Drawing.Shape kept = Assert.Single(new Document(output).GetChildNodes(NodeType.Shape, true)
            .OfType<Aspose.Words.Drawing.Shape>(), static shape => shape.HasImage && shape.GetAncestor(NodeType.HeaderFooter) is null);
        Assert.EndsWith("logo.png", kept.ImageData.SourceFullName, StringComparison.Ordinal);
    }

    private static string[] ListLabels(string path)
    {
        var document = new Document(path);
        document.UpdateListLabels();
        return [.. Body(document).Where(static paragraph => paragraph.IsListItem)
            .Select(static paragraph => paragraph.ListLabel.LabelString)];
    }

    private static IEnumerable<Paragraph> Body(Document document) =>
        document.FirstSection.Body.Paragraphs.Cast<Paragraph>();
}
