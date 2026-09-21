using System.Text;
using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Ports;
using Aspose.Cli.Sdk.Views;
using Aspose.Words;
using Aspose.Words.Loading;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

public sealed class WordsDocumentEngineTests : IClassFixture<WordsFixture>
{
    private readonly WordsFixture _fixture;

    public WordsDocumentEngineTests(WordsFixture fixture) => _fixture = fixture;

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

        Assert.Equal(info.Document.Blocks, first.Blocks.Count + rest.Blocks.Count);
        Assert.Equal(1, info.Outline![0].Block);
        BlockData table = Assert.Single(rest.Blocks, block => block.Type == "table");
        Assert.Equal(2, table.Rows);
        Assert.Equal(2, table.Columns);
        Assert.Equal("Metric", table.Cells![0][0]);
        Assert.Equal("120", table.Cells[1][1]);
        Assert.NotNull(first.Next);
    }

    [Fact]
    public void Read_ExcludesAsposeEvaluationBannerFromCanonicalBlockAddresses()
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

        Assert.Equal("User heading", read.Blocks[0].Text);
        Assert.Equal(1, read.Blocks[0].I);
        Assert.DoesNotContain(
            read.Blocks,
            block => block.Text?.Contains("evaluation copy", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public void Read_ContinuationPreservesANonContiguousExplicitRange()
    {
        string input = _fixture.CreateReport();

        DocumentReadResult read = _fixture.Engine.Read(input, new DocumentReadRequest
        {
            Blocks = PageRange.Parse("1,3,5"),
            Scope = "text",
            MaxBlocks = 1,
            MaxCharacters = 10_000,
        });

        Assert.Contains("--blocks 3,5 ", read.Next, StringComparison.Ordinal);
    }

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
        Assert.True(opened.Document.Blocks > 0);
        Assert.True(FileFormatUtil.DetectFileFormat(output).IsEncrypted);
        var reopened = new Document(output, new LoadOptions { Password = outputPassword });
        Assert.Contains("Encrypted portable document", reopened.GetText(), StringComparison.Ordinal);
    }

    [Fact]
    public void Convert_CorruptInputDoesNotPublishOutput()
    {
        string input = _fixture.Temp.File("corrupt.docx");
        string output = _fixture.Temp.File("corrupt.pdf");
        File.WriteAllBytes(input, [0x00, 0x01, 0x02, 0x03, 0xFF, 0xFE]);

        CliException error = Assert.Throws<CliException>(() =>
            _fixture.Engine.Convert(input, new WordsConvertRequest
            {
                TargetFormatId = "pdf",
                OutputPath = output,
            }));

        Assert.Equal(ErrorCodes.FileCorrupt, error.Code);
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
                        MaxReplacements = 1,
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

    private sealed class MemoryArtifactSink : IViewArtifactSink
    {
        private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);

        public void Write(string relativePath, Action<Stream> contentWriter)
        {
            using var stream = new MemoryStream();
            contentWriter(stream);
            _files.Add(relativePath, stream.ToArray());
        }

        public void WriteText(string relativePath, string content) =>
            _files.Add(relativePath, Encoding.UTF8.GetBytes(content));

        public byte[] Bytes(string relativePath) => _files[relativePath];

        public string Text(string relativePath) =>
            Encoding.UTF8.GetString(Bytes(relativePath));
    }
}
