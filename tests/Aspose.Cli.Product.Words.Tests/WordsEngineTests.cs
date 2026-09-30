using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Ports;
using Aspose.Cli.Sdk.Views;
using Aspose.Words;
using Aspose.Words.Loading;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

public sealed class WordsEngineTests : IClassFixture<WordsFixture>
{
    private readonly WordsFixture _fixture;

    public WordsEngineTests(WordsFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData(10, 1, 10000)]
    [InlineData(15, 3, 10)]
    [InlineData(20, 3, 10)]
    public void FullRead_CountsTextAndRunsAgainstOneBudget(int budget, int runCount, int textLength)
    {
        string input = _fixture.Temp.File($"read-budget-{budget}.docx");
        var document = new Document();
        Paragraph paragraph = document.FirstSection.Body.FirstParagraph;
        for (int index = 0; index < runCount; index++)
        {
            var run = new Run(document, index == runCount - 1 ? new string('x', textLength - (3 * index)) : "xxx");
            run.Font.Bold = index % 2 == 0;
            paragraph.AppendChild(run);
        }
        document.Save(input);
        BlockData block = Assert.Single(_fixture.Engine.Read(input, new DocumentReadRequest
        {
            Blocks = PageRange.Parse("1"), Scope = "full", MaxCharacters = budget,
        }).Blocks);
        int characters = (block.Text?.Length ?? 0) + (block.Runs?.Sum(static run => run.Text.Length) ?? 0);
        Assert.Equal(budget, characters);
        Assert.Equal(budget < textLength * 2, block.ContentTruncated);
        Assert.Equal(1, block.Block);
    }
    [Fact]
    public void InfoAndRead_UseOneStableParagraphTableBlockIndex()
    {
        string input = _fixture.CreateReport();

        DocumentInfoResult info = _fixture.Engine.GetInfo(input, new DocumentInfoRequest
        {
            Details = ["outline", "tables", "bookmarks"],
        });
        DocumentReadResult first = _fixture.Engine.Read(input, new DocumentReadRequest
        {
            Scope = "full",
            MaxBlocks = 2,
            MaxCharacters = 1000,
        });
        DocumentReadResult rest = _fixture.Engine.Read(input, new DocumentReadRequest
        {
            Blocks = PageRange.Parse("3-"),
            Scope = "text",
            MaxBlocks = 100,
            MaxCharacters = 10_000,
        });

        Assert.Equal(info.Document.BlockCount, first.Blocks.Count + rest.Blocks.Count);
        Assert.Equal(1, info.Outline![0].Block);
        BlockData table = Assert.Single(rest.Blocks, block => block.Type == "table");
        Assert.Equal(2, table.RowCount);
        Assert.Equal(2, table.ColumnCount);
        Assert.Equal("Metric", table.Cells![0][0]);
        Assert.Equal("120", table.Cells[1][1]);
        Assert.True(first.Window!.Truncated);
        Assert.Equal("block", first.Window.Unit);
        Assert.Equal(first.Blocks.Count, first.Window.Returned);
        Assert.Equal(info.Document.BlockCount, first.Window.Total);
        Assert.Equal(info.Document.BlockCount, first.BlockCount);
    }

    [LicensedFact]
    public void Inspect_DisclosesAnOutlineCappedAtAThousandHeadings()
    {
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.ParagraphFormat.StyleIdentifier = StyleIdentifier.Heading1;
        for (int heading = 1; heading <= 1000; heading++)
        {
            builder.Writeln($"Heading {heading}");
        }

        builder.Write("Heading 1001");
        string input = _fixture.Temp.File("headings.docx");
        document.Save(input, SaveFormat.Docx);

        DocumentInfoResult info = _fixture.Engine.GetInfo(input, new DocumentInfoRequest { Details = ["outline"] });

        Assert.Equal(1000, info.Outline!.Count);
        Warning warning = Assert.Single(info.Warnings!, static warning => warning.Code == WarningCodes.ListTruncated);
        Assert.Equal("outline", warning.Location);
        Assert.Contains("first 1000 of 1001", warning.Message, StringComparison.Ordinal);
        Assert.Contains("words query blocks --scope outline", warning.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void BlockRangesPastTheLastBlock_AreBlockNotFound()
    {
        string input = _fixture.CreateReport();

        CliException read = Assert.Throws<CliException>(() =>
            _fixture.Engine.Read(input, new DocumentReadRequest { Blocks = PageRange.Parse("99") }));
        CliException edit = Assert.Throws<CliException>(() => _fixture.Engine.ApplyOps(
            input,
            new WordsOpsBatch { Ops = [new DeleteBlocksOp { Target = new WordsTarget { Blocks = "2-99" } }] },
            new WordsEditRequest { OutputPath = _fixture.Temp.File("past-the-end.docx") }));

        Assert.Equal(WordsDiagnostics.BlockNotFound, read.Code);
        Assert.Equal("99", read.Details!["requested"]!.GetValue<string>());
        Assert.Equal(6, read.Details["availableCount"]!.GetValue<int>());
        Assert.Equal(WordsDiagnostics.BlockNotFound, edit.Code);
    }

    [Fact]
    public void MissingSectionsAndOccurrences_ReportHowManyExist()
    {
        string input = _fixture.CreateReport();

        CliException section = Assert.Throws<CliException>(() =>
            _fixture.Engine.Read(input, new DocumentReadRequest { Section = 5 }));
        CliException occurrence = Assert.Throws<CliException>(() => Edit(input, "occurrence",
            new SetTextOp { At = new WordsTarget { Find = "revenue", Nth = 5 }, Text = "x" }));

        Assert.Equal(WordsDiagnostics.SectionNotFound, section.Code);
        Assert.Equal("5", section.Details!["requested"]!.GetValue<string>());
        Assert.Equal(1, section.Details["availableCount"]!.GetValue<int>());
        Assert.Equal(WordsDiagnostics.AnchorNotFound, occurrence.Code);
        Assert.Equal("5", occurrence.Details!["requested"]!.GetValue<string>());
        Assert.Equal(2, occurrence.Details["availableCount"]!.GetValue<int>());
    }

    [Fact]
    public void MissingNamedTargets_ListTheAvailableNamesAndTheClosest()
    {
        string input = _fixture.Temp.File("named-targets.docx");
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.ParagraphFormat.StyleIdentifier = StyleIdentifier.Heading1;
        builder.Writeln("Quarterly report");
        builder.ParagraphFormat.StyleIdentifier = StyleIdentifier.Normal;
        builder.StartBookmark("Summary");
        builder.Write("Summary text");
        builder.EndBookmark("Summary");
        builder.StartBookmark("_Toc1");
        builder.Write(" hidden target");
        builder.EndBookmark("_Toc1");
        document.Save(input);

        CliException bookmark = Assert.Throws<CliException>(() => Edit(input, "bookmark",
            new SetTextOp { At = new WordsTarget { Bookmark = "Sumary" }, Text = "x" }));
        CliException heading = Assert.Throws<CliException>(() => Edit(input, "heading",
            new SetTextOp { At = new WordsTarget { Heading = "Quartely report" }, Text = "x" }));
        CliException style = Assert.Throws<CliException>(() => Edit(input, "style",
            new SetStyleOp { Target = new WordsTarget { Block = 1 }, Style = "heading 7x" }));

        Assert.Equal(ErrorCodes.BookmarkNotFound, bookmark.Code);
        Assert.Equal("Sumary", bookmark.Details!["requested"]!.GetValue<string>());
        Assert.Equal(["Summary"], Names(bookmark, "available"));
        Assert.Equal(["Summary"], Names(bookmark, "suggestions"));
        Assert.Equal(WordsDiagnostics.AnchorNotFound, heading.Code);
        Assert.Equal(["Quarterly report"], Names(heading, "available"));
        Assert.Equal("Did you mean 'Quarterly report'?", heading.Hint);
        Assert.Equal(ErrorCodes.StyleNotFound, style.Code);
        Assert.Equal("heading 7x", style.Details!["requested"]!.GetValue<string>());
        Assert.Contains("Normal", Names(style, "available"));
    }

    private WordsEditResult Edit(string input, string name, WordsOp op) => _fixture.Engine.ApplyOps(
        input,
        new WordsOpsBatch { Ops = [op] },
        new WordsEditRequest { OutputPath = _fixture.Temp.File($"not-found-{name}.docx") });

    private static string[] Names(CliException error, string key) =>
        error.Details![key]!.AsArray().Select(static node => node!.GetValue<string>()).ToArray();

    [Fact]
    public void Read_ExcludesTheEvaluationBannerOnlyUnderEvaluation()
    {
        string input = _fixture.Temp.File("evaluation-banner.docx");
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.Writeln(
            "Created with an evaluation copy of Aspose.Words. To remove all limitations, " +
            "use https://products.aspose.com/words/temporary-license/");
        builder.ParagraphFormat.StyleIdentifier = StyleIdentifier.Heading1;
        builder.Writeln("User heading");
        builder.ParagraphFormat.StyleIdentifier = StyleIdentifier.Normal;
        builder.Write("User body");
        document.Save(input);

        DocumentReadResult read = _fixture.Engine.Read(input, new DocumentReadRequest
        {
            Scope = "text",
            MaxBlocks = 20,
            MaxCharacters = 10_000,
        });

        // A licensed document that merely quotes the banner keeps it as its first block.
        bool evaluation = _fixture.LicenseState == Aspose.Cli.Sdk.Licensing.LicenseState.Evaluation;
        Assert.Equal(evaluation ? "User heading" : "Created with an evaluation copy of Aspose.Words.", read.Blocks[0].Text![..(evaluation ? 12 : 48)]);
        Assert.Equal(1, read.Blocks[0].Block);
    }

    [Fact]
    public void AppendAndMerge_LeaveNoEvaluationBannerAmongTheBlocks()
    {
        // Under evaluation every saved input starts with the banner; appending one document to
        // another must not carry that banner into the middle of the result as a block.
        string markdown = _fixture.Temp.File("banner-source.md");
        File.WriteAllText(markdown, "Contents\n\n# One\n\nSee [the source](https://example.com/source).\n");
        string input = _fixture.Temp.File("banner-source.docx");
        _fixture.Engine.Create(new NewDocumentRequest { OutputPath = input, MarkdownPath = markdown });
        string[] original = BlockTexts(input);
        string appended = _fixture.Temp.File("banner-appended.docx");
        string merged = _fixture.Temp.File("banner-merged.docx");

        _fixture.Engine.ApplyOps(
            input,
            new WordsOpsBatch { Ops = [new AppendDocumentOp { Path = input }] },
            new WordsEditRequest { OutputPath = appended });
        _fixture.Engine.ApplyOps(
            input,
            new WordsOpsBatch
            {
                Ops =
                [
                    new MailMergeOp
                    {
                        Inline = [new Dictionary<string, string?> { ["Name"] = "Ava" }, new Dictionary<string, string?> { ["Name"] = "Noah" }],
                    },
                ],
            },
            new WordsEditRequest { OutputPath = merged });

        Assert.Equal([.. original, .. original], BlockTexts(appended));
        Assert.Equal([.. original, .. original], BlockTexts(merged));
    }

    private string[] BlockTexts(string path) =>
        _fixture.Engine.Read(path, new DocumentReadRequest { MaxBlocks = 1000 })
            .Blocks.Select(static block => block.Text ?? $"table {block.RowCount}x{block.ColumnCount}").ToArray();

    [Fact]
    public void ConvertRenderAndExtract_ProduceRealOutputs()
    {
        string input = _fixture.CreateReport();
        string pdf = _fixture.Temp.File("report.pdf");
        string image = _fixture.Temp.File("page.png");
        string assets = Path.Combine(_fixture.Temp.Path, "assets");

        var converted = _fixture.Engine.Convert(input, new WordsConvertRequest
        {
            TargetFormatId = "pdf",
            OutputPath = pdf,
            Overwrite = false,
        });
        var rendered = _fixture.Engine.Render(input, new WordsRenderRequest
        {
            TargetFormatId = "png",
            OutputPath = image,
            Overwrite = false,
            Dpi = 150,
        });
        var extracted = _fixture.Engine.Extract(input, new WordsExtractRequest
        {
            What = "text",
            OutputDirectory = assets,
        });

        Assert.True(HasHeader(pdf, [0x25, 0x50, 0x44, 0x46]));
        Assert.True(HasHeader(image, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]));
        string expectedMode = _fixture.LicenseState == LicenseState.Licensed
            ? LicenseModes.Licensed
            : LicenseModes.Evaluation;
        Assert.Equal(expectedMode, converted.License?.Mode);
        Assert.Equal(expectedMode, rendered.License?.Mode);
        bool evaluation = _fixture.LicenseState == LicenseState.Evaluation;
        Assert.Equal(
            evaluation,
            converted.Warnings?.Any(warning => warning.Code == WarningCodes.EvalMode) == true);
        Assert.Equal(
            evaluation,
            rendered.Warnings?.Any(warning => warning.Code == WarningCodes.EvalMode) == true);
        Assert.Contains("Quarterly report", File.ReadAllText(extracted.Items[0].Path), StringComparison.Ordinal);
    }

    [Fact]
    public void View_WritesPagePartsThroughSinkAndPreservesPassword()
    {
        const string password = "view-password";
        string input = _fixture.CreateEncryptedDocument(password);
        var artifacts = new MemoryArtifactSink();

        ViewManifest manifest = _fixture.Engine.RenderView(
            input,
            new ViewRenderRequest
            {
                View = WordsViews.Pages,
                MaxParts = 8,
                Purpose = ViewPurpose.Display,
                Password = password,
            },
            artifacts);

        Assert.Equal(WordsViews.Pages, manifest.View);
        Assert.Equal("docx", manifest.SourceFormat);
        Assert.Null(manifest.Warnings);
        ViewPart first = manifest.Parts[0];
        Assert.Equal("page-0001.png", first.File);
        Assert.Equal(ViewPartKinds.Image, first.Kind);
        Assert.True(first.Width > 0 && first.Height > 0);
        Assert.True(HasHeader(
            artifacts.Bytes("page-0001.png"),
            [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]));
    }

    [Fact]
    public void EncryptedInputAndOutput_EnforcePasswordsAndRoundTrip()
    {
        const string inputPassword = "source-password";
        const string outputPassword = "output-password";
        string input = _fixture.CreateEncryptedDocument(inputPassword);
        string output = _fixture.Temp.File("reencrypted.docx");

        CliException missing = Assert.Throws<CliException>(() =>
            _fixture.Engine.GetInfo(input, new DocumentInfoRequest()));
        CliException wrong = Assert.Throws<CliException>(() =>
            _fixture.Engine.GetInfo(input, new DocumentInfoRequest { Password = "wrong" }));
        DocumentInfoResult opened = _fixture.Engine.GetInfo(
            input,
            new DocumentInfoRequest { Password = inputPassword });

        _fixture.Engine.Convert(input, new WordsConvertRequest
        {
            TargetFormatId = "docx",
            OutputPath = output,
            Password = inputPassword,
            EncryptPassword = outputPassword,
        });

        Assert.Equal(ErrorCodes.PasswordRequired, missing.Code);
        Assert.Equal(ErrorCodes.PasswordInvalid, wrong.Code);
        Assert.True(opened.Document.BlockCount > 0);
        Assert.True(FileFormatUtil.DetectFileFormat(output).IsEncrypted);
        var reopened = new Document(output, new LoadOptions { Password = outputPassword });
        Assert.Contains("Encrypted portable document", reopened.GetText(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("malformed-zip")]
    [InlineData("truncated-docx")]
    public void CorruptInput_ReportsParsingFailureAndPreservesFiles(string kind)
    {
        string input = _fixture.Temp.File($"corrupt-{kind}.docx");
        string output = _fixture.Temp.File($"corrupt-{kind}.pdf");
        byte[] bytes = kind switch
        {
            "garbage" => [0x00, 0x01, 0x02, 0x03, 0xFF, 0xFE],
            "malformed-zip" => "PK\x03\x04invalid synthetic docx"u8.ToArray(),
            _ => File.ReadAllBytes(_fixture.CreateReport("intact-before-truncation.docx"))[..64],
        };
        File.WriteAllBytes(input, bytes);

        CliException inspection = Assert.Throws<CliException>(() =>
            _fixture.Engine.GetInfo(input, new DocumentInfoRequest()));
        CliException conversion = Assert.Throws<CliException>(() =>
            _fixture.Engine.Convert(input, new WordsConvertRequest
            {
                TargetFormatId = "pdf",
                OutputPath = output,
            }));

        Assert.Equal(ErrorCodes.FileCorrupt, inspection.Code);
        Assert.Equal(ErrorCodes.FileCorrupt, conversion.Code);
        Assert.False(File.Exists(output));
        Assert.Equal(bytes, File.ReadAllBytes(input));

        byte[] retained = "Existing output must survive a rejected conversion."u8.ToArray();
        File.WriteAllBytes(output, retained);
        CliException replacement = Assert.Throws<CliException>(() =>
            _fixture.Engine.Convert(input, new WordsConvertRequest
            {
                TargetFormatId = "pdf",
                OutputPath = output,
                Overwrite = true,
            }));
        Assert.Equal(ErrorCodes.FileCorrupt, replacement.Code);
        Assert.Equal(retained, File.ReadAllBytes(output));
        Assert.Equal(bytes, File.ReadAllBytes(input));
    }

    [Fact]
    public void ExclusiveInputLock_IsDistinctFromCorruptionAndReopensAfterRelease()
    {
        string input = _fixture.CreateReport("locked-input.docx");
        byte[] original = File.ReadAllBytes(input);
        string output = _fixture.Temp.File("locked-input.pdf");
        using (var held = new FileStream(input, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            CliException error = Assert.Throws<CliException>(() =>
                _fixture.Engine.Convert(input, new WordsConvertRequest
                {
                    TargetFormatId = "pdf",
                    OutputPath = output,
                }));
            Assert.Equal(ErrorCodes.FileLocked, error.Code);
            Assert.False(File.Exists(output));
        }

        Assert.True(_fixture.Engine.GetInfo(input, new DocumentInfoRequest()).Document.BlockCount > 0);
        Assert.Equal(original, File.ReadAllBytes(input));
    }

    [Fact]
    public void MissingInput_RemainsAFileNotFoundError()
    {
        string input = _fixture.Temp.File("missing-parent/missing.docx");
        string output = _fixture.Temp.File("missing-input.pdf");
        CliException error = Assert.Throws<CliException>(() =>
            _fixture.Engine.Convert(input, new WordsConvertRequest
            {
                TargetFormatId = "pdf",
                OutputPath = output,
            }));
        Assert.Equal(ErrorCodes.FileNotFound, error.Code);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void Edit_ResolvesAllBlockAddressesBeforeInsertions()
    {
        string input = _fixture.CreateReport();
        string output = _fixture.Temp.File("edited.docx");
        var batch = new WordsOpsBatch
        {
            Ops =
            [
                new InsertParagraphsOp
                {
                    At = new WordsTarget { Block = 1 },
                    Position = "after",
                    Paragraphs = [new ParagraphInput { Text = "Inserted" }],
                },
                new SetTextOp { At = new WordsTarget { Block = 2 }, Text = "Original second block changed" },
            ],
        };

        WordsEditResult result = _fixture.Engine.ApplyOps(input, batch, new WordsEditRequest
        {
            OutputPath = output,
            Overwrite = false,
        });
        DocumentReadResult read = _fixture.Engine.Read(output, new DocumentReadRequest
        {
            Scope = "text",
            MaxBlocks = 20,
            MaxCharacters = 10_000,
        });

        Assert.All(result.Applied, item => Assert.Equal("ok", item.Status));
        Assert.Equal(["block/1"], result.Applied[0].Targets);
        Assert.Equal(["block/2"], result.Applied[1].Targets);
        Assert.Equal("Inserted", read.Blocks[1].Text);
        Assert.Equal("Original second block changed", read.Blocks[2].Text);
    }

    [Fact]
    public void Edit_RejectsDeleteThenReferenceBeforeWriting()
    {
        string input = _fixture.CreateReport();
        string output = _fixture.Temp.File("conflict.docx");
        var batch = new WordsOpsBatch
        {
            Ops =
            [
                new DeleteBlocksOp { Target = new WordsTarget { Block = 2 } },
                new SetTextOp { At = new WordsTarget { Block = 2 }, Text = "too late" },
            ],
        };

        Sdk.Errors.CliException error = Assert.Throws<Sdk.Errors.CliException>(() =>
            _fixture.Engine.ApplyOps(input, batch, new WordsEditRequest { OutputPath = output }));

        Assert.Equal(Sdk.Errors.ErrorCodes.OpsInvalid, error.Code);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void Edit_ReplaceTextHonorsMaximumReplacementCount()
    {
        string input = _fixture.Temp.File("replace-source.docx");
        string output = _fixture.Temp.File("replace-output.docx");
        var document = new Document();
        new DocumentBuilder(document).Write("token token token");
        document.Save(input);

        _fixture.Engine.ApplyOps(
            input,
            new WordsOpsBatch
            {
                Ops =
                [
                    new ReplaceTextOp
                    {
                        Find = "token",
                        Replace = "done",
                        Scope = "body",
                        MaxReplacementCount = 1,
                    },
                ],
            },
            new WordsEditRequest { OutputPath = output });

        var changed = new Document(output);
        Assert.Contains("done token token", changed.GetText(), StringComparison.Ordinal);
    }

    [Fact]
    public void Edit_MailMergeCreatesOneLetterPerDataRow()
    {
        string input = _fixture.Temp.File("merge-source.docx");
        string output = _fixture.Temp.File("merge-output.docx");
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.Write("Dear ");
        builder.InsertField("MERGEFIELD FirstName");
        document.Save(input);

        _fixture.Engine.ApplyOps(
            input,
            new WordsOpsBatch
            {
                Ops =
                [
                    new MailMergeOp
                    {
                        Inline =
                        [
                            new Dictionary<string, string?> { ["FirstName"] = "Ava" },
                            new Dictionary<string, string?> { ["FirstName"] = "Noah" },
                        ],
                    },
                ],
            },
            new WordsEditRequest { OutputPath = output });

        var merged = new Document(output);
        string text = merged.GetText();
        Assert.Equal(2, merged.Sections.Count);
        Assert.Equal(1, Count(text, "Ava"));
        Assert.Equal(1, Count(text, "Noah"));
    }

    [Theory]
    [InlineData("header-only.csv", "Name,City\n", false)]
    [InlineData("header-only.csv", "Name,City\n", true)]
    [InlineData("blank-line.csv", "Name,City\n\n", false)]
    [InlineData("blank-line.csv", "Name,City\n\n", true)]
    [InlineData("empty-array.json", "[]", false)]
    [InlineData("empty-array.json", "[]", true)]
    public void Edit_MailMergeWithoutDataRowsIsRefusedAndWritesNothing(string dataName, string data, bool regions)
    {
        string input = _fixture.Temp.File($"merge-empty-{regions}-{dataName}.docx");
        string output = _fixture.Temp.File($"merge-empty-{regions}-{dataName}.out.docx");
        string path = _fixture.Temp.File($"merge-empty-{regions}-{dataName}");
        var document = new Document();
        new DocumentBuilder(document).InsertField("MERGEFIELD Name");
        document.Save(input);
        File.WriteAllText(path, data);

        CliException error = Assert.Throws<CliException>(() => _fixture.Engine.ApplyOps(
            input,
            new WordsOpsBatch { Ops = [new MailMergeOp { Path = path, Regions = regions }] },
            new WordsEditRequest { OutputPath = output }));

        Assert.Equal(WordsDiagnostics.MergeDataInvalid, error.Code);
        Assert.Contains("mail_merge needs at least one row", error.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void Split_PreflightsTheWholeOutputSetBeforePublishingFiles()
    {
        string input = _fixture.CreateTwoSectionDocument("split-source.docx");
        string outputDirectory = Path.Combine(_fixture.Temp.Path, "split-output");
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(Path.Combine(outputDirectory, "part-002.docx"), "existing");

        Sdk.Errors.CliException error = Assert.Throws<Sdk.Errors.CliException>(() =>
            _fixture.Engine.Split(input, new WordsSplitRequest
            {
                By = "section",
                OutputDirectory = outputDirectory,
            }));

        Assert.Equal(Sdk.Errors.ErrorCodes.OutputExists, error.Code);
        Assert.False(File.Exists(Path.Combine(outputDirectory, "part-001.docx")));
        Assert.Equal("existing", File.ReadAllText(Path.Combine(outputDirectory, "part-002.docx")));
    }

    [Fact]
    public void SetText_RejectedForATableTargetChangesNothing()
    {
        string input = _fixture.Temp.File("set-text-mixed.docx");
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.Writeln("Original paragraph");
        builder.StartTable();
        builder.InsertCell();
        builder.Write("Cell");
        builder.EndRow();
        builder.EndTable();
        document.Save(input);
        string output = _fixture.Temp.File("set-text-mixed.out.docx");

        WordsEditResult result = _fixture.Engine.ApplyOps(input, new WordsOpsBatch
        {
            Ops = [new SetTextOp { At = new WordsTarget { Blocks = "1-2" }, Text = "Replaced" }],
        }, new WordsEditRequest
        {
            OutputPath = output,
            Options = new EditCommandOptions { BestEffort = true },
        });

        Assert.Equal(OpStatuses.Failed, Assert.Single(result.Applied).Status);
        string text = new Document(output).GetText();
        Assert.Contains("Original paragraph", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Replaced", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractComments_WritesTheCommentsAsJson()
    {
        string input = _fixture.Temp.File("extract-comments.docx");
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.Writeln("Reviewed clause");
        var comment = new Comment(document, "Reviewer", "R", DateTime.UnixEpoch);
        comment.SetText("Please confirm the term.");
        builder.CurrentParagraph.AppendChild(comment);
        document.Save(input);

        var extracted = _fixture.Engine.Extract(input, new WordsExtractRequest
        {
            What = "comments",
            OutputDirectory = _fixture.Temp.File("extract-comments"),
        });

        string json = File.ReadAllText(Assert.Single(extracted.Items).Path);
        Assert.Contains("\"Reviewer\"", json, StringComparison.Ordinal);
        Assert.Contains("Please confirm the term.", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractImages_SkipsLinkOnlyImagesAndDisclosesThem()
    {
        string input = _fixture.Temp.File("extract-linked-image.docx");
        var document = new Document();
        var linked = new Aspose.Words.Drawing.Shape(document, Aspose.Words.Drawing.ShapeType.Image) { Width = 10, Height = 10 };
        linked.ImageData.SourceFullName = "https://example.invalid/logo.png";
        new DocumentBuilder(document).InsertNode(linked);
        document.Save(input);

        var extracted = _fixture.Engine.Extract(input, new WordsExtractRequest
        {
            What = "images",
            OutputDirectory = _fixture.Temp.File("extract-linked-image"),
        });

        Assert.Empty(extracted.Items);
        Assert.Contains(extracted.Warnings!, warning => warning.Code == "LINKED_IMAGES_SKIPPED");
    }

    [Fact]
    public void Extract_ReplacesAnExistingFileOnlyWithOverwrite()
    {
        string input = _fixture.Temp.File("extract-overwrite.docx");
        var builder = new DocumentBuilder();
        builder.Writeln("Extracted text");
        builder.Document.Save(input);
        string directory = _fixture.Temp.File("extract-overwrite");
        Directory.CreateDirectory(directory);
        string existing = Path.Combine(directory, "document.txt");
        File.WriteAllText(existing, "kept");

        CliException refused = Assert.Throws<CliException>(() => _fixture.Engine.Extract(input, new WordsExtractRequest
        {
            What = "text",
            OutputDirectory = directory,
        }));
        Assert.Equal(ErrorCodes.OutputExists, refused.Code);
        Assert.Equal("kept", File.ReadAllText(existing));
        Assert.Equal([existing], Directory.GetFiles(directory));

        string replaced = Assert.Single(_fixture.Engine.Extract(input, new WordsExtractRequest
        {
            What = "text",
            OutputDirectory = directory,
            Overwrite = true,
        }).Items).Path;
        Assert.Equal(existing, replaced);
        Assert.Contains("Extracted text", File.ReadAllText(existing), StringComparison.Ordinal);
    }

    private static bool HasHeader(string path, byte[] expected)
    {
        byte[] actual = new byte[expected.Length];
        using FileStream stream = File.OpenRead(path);
        return stream.Read(actual, 0, actual.Length) == actual.Length
            && actual.AsSpan().SequenceEqual(expected);
    }

    private static bool HasHeader(byte[] actual, byte[] expected) =>
        actual.AsSpan().StartsWith(expected);

    private static int Count(string value, string text) =>
        value.Split(text, StringSplitOptions.None).Length - 1;

    [Fact]
    public void Verify_AcceptsEditsWholeBodyComparisonCannotSee()
    {
        string input = _fixture.CreateReport("verify-metadata.docx");
        string output = _fixture.Temp.File("verify-metadata.out.docx");

        WordsEditResult result = _fixture.Engine.ApplyOps(
            input,
            new WordsOpsBatch { Ops = [new SetPropertiesOp { Title = "Accepted" }] },
            new WordsEditRequest { OutputPath = output, Verify = true });

        // Document.Compare only models the body, so a metadata-only edit leaves it
        // silent. That is evidence, not a fault: the operation outcome already says
        // the edit happened, and the document itself proves it.
        Assert.Equal("ok", Assert.Single(result.Applied).Status);
        Assert.True(result.Verification!.Ok);
        Assert.Empty(result.Verification.Issues!);
        Assert.False(result.Verification.SemanticChangesDetected);
        Assert.Equal("Accepted", new Document(output).BuiltInDocumentProperties.Title);
    }

    [Fact]
    public void CheckFonts_ReportsOnlyTheFontsTheContentRenders()
    {
        const string eastAsian = "Fictional East Asian Font 987";
        const string unusedStyle = "Fictional Unused Font 987";
        string input = _fixture.CreateReport("fonts-latin.docx");
        var document = new Document(input);
        foreach (Run run in document.GetChildNodes(NodeType.Run, true).Cast<Run>())
        {
            run.Font.NameFarEast = eastAsian;
        }

        document.Styles.Add(StyleType.Paragraph, "Never Applied").Font.Name = unusedStyle;
        document.Save(input);

        IReadOnlyList<FontAvailability> fonts =
            _fixture.Fonts.CheckFonts(input, new FontCheckRequest()).Fonts;

        // A style nothing applies draws no glyph, and neither does an East Asian
        // font named by runs that contain no East Asian character.
        Assert.DoesNotContain(fonts, font => font.Name == unusedStyle);
        Assert.DoesNotContain(fonts, font => font.Name == eastAsian);
        Assert.Contains(fonts, font => font.Name == "Arial");
    }

    [Fact]
    public void CheckFonts_ReportsTheEastAsianFontWhenTheTextNeedsIt()
    {
        string input = _fixture.CreateReport("fonts-cjk.docx");
        var document = new Document(input);
        var builder = new DocumentBuilder(document);
        builder.MoveToDocumentEnd();
        builder.Font.NameFarEast = "Microsoft YaHei";
        builder.Writeln("中文段落");
        document.Save(input);

        IReadOnlyList<FontAvailability> fonts =
            _fixture.Fonts.CheckFonts(input, new FontCheckRequest()).Fonts;

        Assert.Contains(fonts, font => font.Name == "Microsoft YaHei");
    }

    [Fact]
    public void CheckFonts_ReportsTheFontTheLayoutSubstitutesForAMissingOne()
    {
        const string missing = "Fictional Missing Font 987";
        string input = _fixture.Temp.File("fonts-missing.docx");
        var builder = new DocumentBuilder();
        builder.Font.Name = missing;
        builder.Writeln("Text drawn with a font this machine lacks.");
        builder.Font.Name = "Arial";
        builder.Writeln("Text drawn with an installed font.");
        builder.Document.Save(input);

        IReadOnlyList<FontAvailability> fonts =
            _fixture.Fonts.CheckFonts(input, new FontCheckRequest()).Fonts;

        FontAvailability substituted = Assert.Single(fonts, font => font.Name == missing);
        Assert.False(substituted.Available);
        Assert.False(string.IsNullOrWhiteSpace(substituted.SubstitutedBy));
        Assert.NotEqual(missing, substituted.SubstitutedBy);
        FontAvailability installed = Assert.Single(fonts, font => font.Name == "Arial");
        Assert.True(installed.Available);
        Assert.Null(installed.SubstitutedBy);
    }
}
