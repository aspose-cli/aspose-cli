using System.Text.RegularExpressions;
using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.IO.Publication;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.TestKit;
using Aspose.Words;
using Aspose.Words.Vba;
using Aspose.Words.Saving;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

public sealed class WordsDisclosureTests
{
    [Theory]
    [InlineData(LicenseState.Evaluation, 2)]
    [InlineData(LicenseState.Licensed, 3)]
    public void EvaluationArtifacts_AreRecognizedOnlyUnderEvaluation(LicenseState state, int blocks)
    {
        if (state == LicenseState.Licensed)
        {
            TestLicense.Require("Only a licensed SDK writes the quoted banner without adding its own.");
        }
        using var fixture = new WordsFixture();
        var source = new Document();
        var builder = new DocumentBuilder(source);
        builder.Writeln(WordsFixture.BannerText);
        builder.Writeln("This document was truncated here because of the evaluation notice we quote.");
        builder.Write("Body");
        string input = fixture.Temp.File("quotes.docx");
        source.Save(input, SaveFormat.Docx);

        ResourceBudgetLedger budgets = ProductTestBudgets.Create<WordsModule>();
        var outputs = new OutputPipeline<Document>(new FixedGate(state), new WordsEvaluationProfile(), new SafeFileWriter(budgets));
        var loader = new WordsDocumentLoader(budgets, outputs);
        using LoadedDocument loaded = loader.Open(input, null);
        var index = new DocumentBlockIndex(loaded.Document, loaded.Evaluation);
        WordsReadResult read = (WordsReadResult)outputs.Disclose(
            WordsRead.Run(WordsActivation.Session(outputs, budgets), new WordsReadRequest { Input = input, Content = "text" }));

        bool evaluation = state == LicenseState.Evaluation;
        // The SDK discloses the truncation notice of an input read in evaluation mode only.
        Assert.Equal(evaluation, read.Warnings?.Any(static warning => warning.Code == WarningCodes.EvaluationInputTruncated) == true);
        // Under evaluation the SDK may add its own banner; the quoted one is skipped only there.
        Assert.True(evaluation ? index.Count <= blocks : index.Count == blocks);
    }

    /// <summary>
    /// A document that ends with the evaluation truncation notice an earlier save left is edited
    /// in evaluation mode, not refused: nothing tells whether this open cut it short.
    /// </summary>
    [Fact]
    public void ReplaceText_InADocumentCarryingAnOldTruncationNotice_IsAppliedInEvaluationMode()
    {
        using var fixture = new WordsFixture();
        var source = new Document();
        var builder = new DocumentBuilder(source);
        builder.Writeln("Clause one.");
        builder.Write("This document was truncated here because it was created in the Evaluation Mode.");
        string input = fixture.Temp.File("old-notice.docx");
        source.Save(input, SaveFormat.Docx);
        ResourceBudgetLedger budgets = ProductTestBudgets.Create<WordsModule>();
        var outputs = new OutputPipeline<Document>(new FixedGate(LicenseState.Evaluation), new WordsEvaluationProfile(), new SafeFileWriter(budgets));
        WordsSession engine = WordsActivation.Session(outputs, budgets);

        var edited = (WordsEditResult)outputs.Disclose(WordsEdit.Run(engine, new WordsEditRequest
        {
            Input = input,
            Batch = new WordsOpsBatch { Ops = [new ReplaceTextOp { Find = "one", Replace = "two" }] },
            Output = TestOutput.At(fixture.Temp.File("old-notice-edited.docx")),
        }));

        Assert.Equal(1, edited.Applied[0].ItemsAffected);
        Assert.Contains(edited.Warnings!, static warning => warning.Code == WarningCodes.EvaluationMode);
    }

    /// <summary>
    /// In evaluation mode a verified edit whose output ends with the truncation notice reports
    /// OUTPUT_TRUNCATED, whether this run or an earlier save cut the document short.
    /// </summary>
    [Fact]
    public void Verify_InEvaluationMode_ReportsAnOutputCutShort()
    {
        using var fixture = new WordsFixture();
        var source = new Document();
        var builder = new DocumentBuilder(source);
        builder.Writeln("Clause one.");
        builder.Write("This document was truncated here because it was created in the Evaluation Mode.");
        string input = fixture.Temp.File("cut.docx");
        source.Save(input, SaveFormat.Docx);
        ResourceBudgetLedger budgets = ProductTestBudgets.Create<WordsModule>();
        var outputs = new OutputPipeline<Document>(new FixedGate(LicenseState.Evaluation), new WordsEvaluationProfile(), new SafeFileWriter(budgets));

        var edited = (WordsEditResult)outputs.Disclose(WordsEdit.Run(WordsActivation.Session(outputs, budgets), new WordsEditRequest
        {
            Input = input,
            Batch = new WordsOpsBatch { Ops = [new ReplaceTextOp { Find = "one", Replace = "two" }] },
            Output = TestOutput.At(fixture.Temp.File("cut-edited.docx")),
            Verify = true,
        }));

        Assert.False(edited.Verification!.Ok);
        Assert.Contains(edited.Verification.Issues, static issue => issue.Code == "OUTPUT_TRUNCATED");
    }

    [Fact]
    public void EditingARestrictedDocument_DisclosesThatTheRestrictionWasNotEnforced()
    {
        using var fixture = new WordsFixture();
        var source = new Document();
        new DocumentBuilder(source).Write("Locked text");
        source.Protect(ProtectionType.ReadOnly, "owner");
        string input = fixture.Temp.File("locked.docx");
        source.Save(input, SaveFormat.Docx);
        string output = fixture.Temp.File("edited.docx");

        WordsEditResult result = WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = input,
            Batch = new WordsOpsBatch
            {
                Ops = [new ReplaceTextOp { Find = "Locked", Replace = "Changed" }],
            },
            Output = TestOutput.At(output),
        });

        Assert.Contains(result.Warnings ?? [], static warning => warning.Code == WarningCodes.ProtectionNotEnforced);
        Assert.Equal(ProtectionType.ReadOnly, new Document(output).ProtectionType);
    }

    [Fact]
    public void ConvertingOrEditingADocumentAnUnlicensedSaveMarked_WarnsThatTheOutputKeepsTheMarks()
    {
        using var fixture = new WordsFixture();
        // The banner and footer sentence an unlicensed save writes, as a later licensed run reads them.
        var source = new Document();
        var builder = new DocumentBuilder(source);
        builder.Writeln(WordsFixture.BannerText);
        builder.Write("Clause one.");
        builder.MoveToHeaderFooter(HeaderFooterType.FooterPrimary);
        builder.Write(WordsFixture.FooterMarkText);
        string input = fixture.Temp.File("marked.docx");
        source.Save(input, SaveFormat.Docx);

        WordsConvertResult converted = fixture.Disclosed(engine => WordsConvert.Run(engine, new WordsConvertRequest
        {
            Input = input,
            Output = TestOutput.At(fixture.Temp.File("marked.pdf"), format: "pdf"),
        }));
        WordsEditResult edited = fixture.Disclosed(engine => WordsEdit.Run(engine, new WordsEditRequest
        {
            Input = input,
            Batch = new WordsOpsBatch
            {
                Ops = [new ReplaceTextOp { Find = "one", Replace = "two" }],
            },
            Output = TestOutput.At(fixture.Temp.File("edited.docx")),
        }));

        // Without a license, opening the document adds the marks itself, and EVALUATION_MODE says so.
        WarningCode expected = fixture.LicenseState == LicenseState.Licensed ? WarningCodes.EvaluationInputMarked : WarningCodes.EvaluationMode;
        foreach (ResultEnvelope result in new ResultEnvelope[] { converted, edited })
        {
            Warning disclosure = Assert.Single(result.Warnings!, static warning => warning.Code.Name.StartsWith("EVALUATION_", StringComparison.Ordinal));
            Assert.Equal(expected, disclosure.Code);
            if (expected == WarningCodes.EvaluationInputMarked)
            {
                Assert.Contains("the evaluation banner", disclosure.Message, StringComparison.Ordinal);
                Assert.Contains("the footer sentence", disclosure.Message, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void EditingIntoAFormatThatCannotKeepTheRestrictions_DisclosesTheLoss()
    {
        using var fixture = new WordsFixture();
        var source = new Document();
        new DocumentBuilder(source).Write("Locked text");
        source.Protect(ProtectionType.ReadOnly, "owner");
        string input = fixture.Temp.File("locked.docx");
        source.Save(input, SaveFormat.Docx);
        var batch = new WordsOpsBatch { Ops = [new ReplaceTextOp { Find = "Locked", Replace = "Changed" }] };

        WordsEditResult text = WordsEdit.Run(fixture.Session, new WordsEditRequest { Input = input, Batch = batch, Output = TestOutput.At(fixture.Temp.File("edited.odt")) });
        WordsEditResult word = WordsEdit.Run(fixture.Session, new WordsEditRequest { Input = input, Batch = batch, Output = TestOutput.At(fixture.Temp.File("edited.docx")) });

        // OpenDocument text keeps the content but not Word's editing restrictions.
        Warning lost = Assert.Single(text.Warnings ?? [], static warning => warning.Code == WarningCodes.LossyConversion
            && Regex.IsMatch(warning.Message + warning.Hint, "restrict|protect", RegexOptions.IgnoreCase));
        Assert.Contains("cannot keep the read-only editing restrictions", lost.Message, StringComparison.Ordinal);
        Warning restriction = Assert.Single(text.Warnings ?? [], static warning => warning.Code == WarningCodes.ProtectionNotEnforced);
        Assert.DoesNotContain("output keeps", restriction.Hint, StringComparison.Ordinal);
        Assert.DoesNotContain(word.Warnings ?? [], static warning => warning.Code == WarningCodes.LossyConversion);
        Assert.Contains("output keeps the restrictions", Assert.Single(word.Warnings ?? [], static warning => warning.Code == WarningCodes.ProtectionNotEnforced).Hint, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("pdf", true)]
    [InlineData("odt", true)]
    [InlineData("docx", false)]
    public void ConvertingARestrictedDocument_DisclosesRestrictionsTheOutputCannotKeep(string format, bool lost)
    {
        using var fixture = new WordsFixture();
        var source = new Document();
        new DocumentBuilder(source).Write("Locked text");
        source.Protect(ProtectionType.ReadOnly, "owner");
        string input = fixture.Temp.File("locked.docx");
        source.Save(input, SaveFormat.Docx);

        WordsConvertResult converted = WordsConvert.Run(fixture.Session, new WordsConvertRequest
        {
            Input = input,
            Output = TestOutput.At(fixture.Temp.File("converted." + format), format: format),
        });

        Assert.Equal(lost, (converted.Warnings ?? []).Any(static warning => warning.Code == WarningCodes.LossyConversion
            && warning.Message.Contains("cannot keep the read-only editing restrictions", StringComparison.Ordinal)));
    }

    [Fact]
    public void SavingAPdfInput_DisclosesThatHeadersAndFootersAreGuessed()
    {
        using var fixture = new WordsFixture();
        var source = new Document();
        var builder = new DocumentBuilder(source);
        builder.Write("Clause one.");
        builder.MoveToHeaderFooter(HeaderFooterType.FooterPrimary);
        builder.InsertField("PAGE");
        string pdf = fixture.Temp.File("contract.pdf");
        string docx = fixture.Temp.File("contract.docx");
        source.Save(pdf, SaveFormat.Pdf);
        source.Save(docx, SaveFormat.Docx);

        WordsConvertResult fromPdf = WordsConvert.Run(fixture.Session, new WordsConvertRequest { Input = pdf, Output = TestOutput.At(fixture.Temp.File("from-pdf.docx"), format: "docx") });
        WordsConvertResult fromDocx = WordsConvert.Run(fixture.Session, new WordsConvertRequest { Input = docx, Output = TestOutput.At(fixture.Temp.File("from-docx.docx"), format: "docx") });

        WordsEditResult edited = WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = pdf,
            Batch = new WordsOpsBatch
            {
                Ops = [new ReplaceTextOp { Find = "one", Replace = "two" }],
            },
            Output = TestOutput.At(fixture.Temp.File("edited.docx")),
        });

        Warning lossy = Assert.Single(fromPdf.Warnings ?? [], static warning => warning.Code == WarningCodes.LossyConversion);
        Assert.Contains("may become body text, or headers and footers in which a number", lossy.Message, StringComparison.Ordinal);
        Assert.Contains("--detail sections --detail fields", lossy.Hint, StringComparison.Ordinal);
        Assert.Equal(lossy, Assert.Single(edited.Warnings ?? [], static warning => warning.Code == WarningCodes.LossyConversion));
        Assert.DoesNotContain(fromDocx.Warnings ?? [], static warning => warning.Code == WarningCodes.LossyConversion);
    }

    [Fact]
    public void SavingAChinesePdfInput_DisclosesTheSpacesAtItsLineEnds()
    {
        using var fixture = new WordsFixture();
        var source = new Document();
        new DocumentBuilder(source).Write("甲乙双方经平等协商，自愿签订本合同。");
        string pdf = fixture.Temp.File("chinese.pdf");
        source.Save(pdf, SaveFormat.Pdf);

        WordsConvertResult converted = WordsConvert.Run(fixture.Session, new WordsConvertRequest { Input = pdf, Output = TestOutput.At(fixture.Temp.File("chinese.docx"), format: "docx") });

        Warning[] lossy = [.. (converted.Warnings ?? []).Where(static warning => warning.Code == WarningCodes.LossyConversion)];
        Assert.Equal(2, lossy.Length);
        Assert.Contains("has a space wherever a line ended", lossy[1].Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A review of an html-fixed page rendition warns that it was reflowed as flowing HTML and
    /// points to the source document; a review of ordinary HTML does not.
    /// </summary>
    [Theory]
    [InlineData("html-fixed", true)]
    [InlineData("html", false)]
    public void ReviewingAnHtmlFixedRendition_DisclosesThatItIsReflowed(string format, bool disclosed)
    {
        using var fixture = new WordsFixture();
        var source = new Document();
        var builder = new DocumentBuilder(source);
        builder.Writeln("Clause one.");
        builder.StartTable();
        builder.InsertCell();
        builder.Write("Cell");
        builder.EndRow();
        builder.EndTable();
        string docx = fixture.Temp.File("rendition.docx");
        source.Save(docx, SaveFormat.Docx);
        string html = fixture.Temp.File($"rendition-{format}.html");
        WordsConvert.Run(fixture.Session, new WordsConvertRequest { Input = docx, Output = TestOutput.At(html, format: format) });
        var adapter = new WordsViewAdapter();
        var request = new Sdk.Views.ViewRenderRequest { View = WordsViews.Pages, MaxPartCount = 4, Purpose = Sdk.Views.ViewPurpose.Evidence };

        Sdk.Views.ViewManifest rendered = adapter.Render(fixture.Session, html, request, new MemoryArtifactSink());
        var assessment =adapter.Assess(fixture.Session, html, request, rendered);

        // The review reports the view's warnings and, once each, the assessment's.
        Warning[] lossy = [.. (rendered.Warnings ?? []).Where(static warning => warning.Code == WarningCodes.LossyConversion)];
        if (!disclosed)
        {
            Assert.Empty(lossy);
            Assert.DoesNotContain(assessment.Warnings ?? [], static warning => warning.Code == WarningCodes.LossyConversion);
            return;
        }

        Warning reflowed = Assert.Single(lossy);
        // The assessment repeats it word for word, so the review reports it once.
        Assert.All(
            (assessment.Warnings ?? []).Where(static warning => warning.Code == WarningCodes.LossyConversion),
            warning => Assert.Equal(reflowed.Message, warning.Message));
        Assert.Contains("loads as flowing HTML", reflowed.Message, StringComparison.Ordinal);
        Assert.Contains("page count", reflowed.Message, StringComparison.Ordinal);
        Assert.Contains("source document", reflowed.Hint, StringComparison.Ordinal);
        Assert.Contains(".docx", reflowed.Hint, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true, "docx", false)]
    [InlineData(false, "docx", true)]
    [InlineData(false, "txt", false)]
    public void EditingARevisedDocument_DisclosesTheRevisionsTheOutputKeeps(bool accept, string extension, bool disclosed)
    {
        using var fixture = new WordsFixture();
        string input = RevisedDocument(fixture);
        string output = fixture.Temp.File("edited." + extension);

        WordsEditResult result = WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = input,
            Batch = new WordsOpsBatch
            {
                Ops = accept ? [new AcceptRevisionsOp()] : [new ReplaceTextOp { Find = "Other", Replace = "More" }],
            },
            Output = TestOutput.At(output),
        });

        Assert.Equal(disclosed, (result.Warnings ?? []).Any(static warning => warning.Code == WordsDiagnostics.TrackedChangesPresent));
        Assert.Equal(disclosed, new Document(output).HasRevisions);
    }

    [Theory]
    [InlineData("docx", true, false)]
    [InlineData("rtf", true, false)]
    [InlineData("odt", true, false)]
    [InlineData("html", false, true)]
    [InlineData("epub", false, true)]
    [InlineData("pdf", false, true)]
    [InlineData("xps", false, true)]
    // A text output names the deleted text it mixes into the body instead.
    [InlineData("txt", false, false)]
    public void SavingARevisedDocument_DisclosesWhetherTheOutputKeepsTheRevisions(string format, bool kept, bool dropped)
    {
        using var fixture = new WordsFixture();
        string input = RevisedDocument(fixture);

        string convertedPath = fixture.Temp.File("converted." + format);
        string editedPath = fixture.Temp.File("edited." + format);

        WordsConvertResult converted = WordsConvert.Run(fixture.Session, new WordsConvertRequest
        {
            Input = input,
            Output = TestOutput.At(convertedPath, format: format),
        });
        // Edit writes only the formats that keep revisions; convert writes the rest.
        WordsEditResult? edited = Edits(format) ? WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = input,
            Batch = new WordsOpsBatch
            {
                Ops = [new ReplaceTextOp { Find = "Other", Replace = "More" }],
            },
            Output = TestOutput.At(editedPath),
        }) : null;

        foreach (IReadOnlyList<Warning>? warnings in edited is null ? [converted.Warnings] : new[] { converted.Warnings, edited.Warnings })
        {
            Assert.Equal(kept, (warnings ?? []).Any(static warning => warning.Code == WordsDiagnostics.TrackedChangesPresent));
            Assert.Equal(dropped, (warnings ?? []).Any(static warning => warning.Code == WarningCodes.LossyConversion
                && warning.Message.Contains("cannot keep tracked changes", StringComparison.Ordinal)));
        }

        // A format that keeps revisions still holds them. HTML and EPUB are not checked the
        // other way: the SDK reads their ins and del marks back as revisions.
        foreach (string path in kept ? new[] { convertedPath, editedPath } : [])
        {
            Assert.True(new Document(path).HasRevisions, path);
        }
    }

    /// <summary>
    /// A format the engine lays out on pages draws the tracked changes on them as markup and the
    /// comments in balloons in a margin that narrows the pages: each is named, with a hint to
    /// delete the comments and settle the revisions first. Other formats keep or drop them with
    /// their own warnings, and a document without comments has no balloons to name.
    /// </summary>
    [Theory]
    [InlineData("pdf", true, true)]
    [InlineData("xps", true, true)]
    [InlineData("ps", true, true)]
    [InlineData("openxps", true, true)]
    [InlineData("pcl", true, true)]
    [InlineData("html-fixed", true, true)]
    [InlineData("pdf", false, true)]
    [InlineData("html", true, false)]
    [InlineData("epub", true, false)]
    [InlineData("docx", true, false)]
    public void ConvertingToPages_NamesTheMarkupAndCommentBalloonsItDraws(string format, bool withComment, bool drawn)
    {
        using var fixture = new WordsFixture();
        string input = RevisedDocument(fixture, withComment);
        string extension = WordsFormats.Definitions.Named(FormatUse.Convert, format)!.Extensions[0];

        WordsConvertResult converted = WordsConvert.Run(fixture.Session, new WordsConvertRequest
        {
            Input = input,
            Output = TestOutput.At(fixture.Temp.File($"drawn-{format}-{withComment}{extension}"), format: format),
        });

        Warning[] lossy = [.. (converted.Warnings ?? []).Where(static warning => warning.Code == WarningCodes.LossyConversion)];
        Warning[] balloons = [.. lossy.Where(static warning => warning.Message.Contains("balloons", StringComparison.Ordinal))];
        Assert.Equal(drawn, lossy.Any(static warning => warning.Message.Contains("draws the tracked changes on its pages as markup", StringComparison.Ordinal)));
        if (!(drawn && withComment))
        {
            Assert.Empty(balloons);
            return;
        }

        Warning warning = Assert.Single(balloons);
        Assert.Equal($"The {format} output draws 1 comment(s) in balloons in a margin beside the text, which narrows its pages.", warning.Message);
        Assert.Equal("""{"content":["comments"]}""", warning.Details?.ToJsonString());
        Assert.Contains("delete_comments", warning.Hint, StringComparison.Ordinal);
        Assert.Contains("accept_revisions or reject_revisions", warning.Hint, StringComparison.Ordinal);
        Assert.False(warning.AffectsCompleteness);
    }

    /// <summary>
    /// A page render draws the comments in balloons too, which narrow its pages: it names them,
    /// though it is not told that it drops the source's revisions.
    /// </summary>
    [Fact]
    public void RenderingACommentedDocument_NamesTheCommentBalloons()
    {
        using var fixture = new WordsFixture();
        string input = RevisedDocument(fixture, withComment: true);

        WordsRenderResult rendered = WordsRender.Run(fixture.Session, new WordsRenderRequest
        {
            Input = input,
            Output = TestOutput.At(fixture.Temp.File("commented-page.png"), format: "png"),
        });

        Warning warning = Assert.Single(rendered.Warnings ?? [], static warning => warning.Code == WarningCodes.LossyConversion);
        Assert.Equal("The png output draws 1 comment(s) in balloons in a margin beside the text, which narrows its pages.", warning.Message);
        Assert.Equal("""{"content":["comments"]}""", warning.Details?.ToJsonString());
        Assert.DoesNotContain(rendered.Warnings ?? [], static warning => warning.Code == WordsDiagnostics.TrackedChangesPresent);
    }

    [Fact]
    public void RenderingARevisedDocument_DoesNotReportLostRevisions()
    {
        using var fixture = new WordsFixture();
        string input = RevisedDocument(fixture);

        // A render shows the document as it looks; the source keeps its revisions.
        WordsRenderResult rendered = WordsRender.Run(fixture.Session, new WordsRenderRequest
        {
            Input = input,
            Output = TestOutput.At(fixture.Temp.File("page.png"), format: "png"),
        });

        Assert.DoesNotContain(rendered.Warnings ?? [], static warning => warning.Code == WarningCodes.LossyConversion);
        Assert.DoesNotContain(rendered.Warnings ?? [], static warning => warning.Code == WordsDiagnostics.TrackedChangesPresent);
    }

    [Theory]
    [InlineData("txt", true)]
    [InlineData("md", true)]
    [InlineData("html", false)]
    [InlineData("docx", false)]
    public void SavingToText_DisclosesTheCommentsAndDeletionsMixedIntoTheBody(string format, bool mixed)
    {
        using var fixture = new WordsFixture();
        string input = RevisedDocument(fixture, withComment: true);

        WordsConvertResult converted = WordsConvert.Run(fixture.Session, new WordsConvertRequest
        {
            Input = input,
            Output = TestOutput.At(fixture.Temp.File("converted." + format), format: format),
        });
        WordsEditResult? edited = Edits(format) ? WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = input,
            Batch = new WordsOpsBatch
            {
                Ops = [new ReplaceTextOp { Find = "Other", Replace = "More" }],
            },
            Output = TestOutput.At(fixture.Temp.File("edited." + format)),
        }) : null;

        foreach (IReadOnlyList<Warning>? warnings in edited is null ? [converted.Warnings] : new[] { converted.Warnings, edited.Warnings })
        {
            string[] messages = (warnings ?? []).Where(static warning => warning.Code == WarningCodes.LossyConversion)
                .Select(static warning => warning.Message).ToArray();
            Assert.Equal(mixed, messages.Any(message => message.Contains($"{format} output writes the text of 1 comment(s) into the body", StringComparison.Ordinal)));
            Assert.Equal(mixed, messages.Any(message => message.Contains("deleted and moved-from text", StringComparison.Ordinal)));
        }
    }

    /// <summary>
    /// The comments and deleted text a plain-text or Markdown output mixes into its body are named
    /// whoever writes it: without the convert that names its input and output, as for a writer
    /// that does not pass them, both warnings are still given, and their hints say what to do in
    /// words, naming no command.
    /// </summary>
    [Fact]
    public void TextWarnings_WithoutTheConvertThatWroteTheOutput_AreStillGiven()
    {
        using var fixture = new WordsFixture();
        var document = new Document(RevisedDocument(fixture, withComment: true));

        Warning[] mixed = [.. WordsEngineSupport.ConversionWarnings(document, "md", evaluation: false)
            .Where(static warning => warning.Message.Contains("into the body text", StringComparison.Ordinal)
                || warning.Message.Contains("deleted and moved-from text", StringComparison.Ordinal))];

        Assert.Equal(2, mixed.Length);
        Assert.All(mixed, static warning => Assert.DoesNotContain("aspose-cli", warning.Hint, StringComparison.Ordinal));
    }

    [Fact]
    public void SavingToText_AfterRemovingCommentsAndAcceptingRevisions_MixesNothing()
    {
        using var fixture = new WordsFixture();
        string input = RevisedDocument(fixture, withComment: true);
        string output = fixture.Temp.File("clean.txt");

        WordsEditResult result = WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = input,
            Batch = new WordsOpsBatch
            {
                Ops = [new DeleteCommentsOp(), new AcceptRevisionsOp()],
            },
            Output = TestOutput.At(output),
        });

        Warning lossy = Assert.Single(result.Warnings ?? [], static warning => warning.Code == WarningCodes.LossyConversion);
        Assert.Equal("Conversion to txt cannot preserve every Word feature.", lossy.Message);
        string text = File.ReadAllText(output);
        Assert.DoesNotContain("Reviewer note", text, StringComparison.Ordinal);
        Assert.Contains("sixty days", text, StringComparison.Ordinal);
        Assert.DoesNotContain("thirty", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// An e-book (epub, azw3, mobi) has no headers or footers, and plain text and Markdown hold
    /// the body text alone, so a convert to one writes none of them into its text and names the
    /// sections whose header or footer holds text or a drawing, with the reason for its format,
    /// without marking the output incomplete. A document whose header is an empty paragraph and
    /// whose footer is only a page number loses nothing such an output could show: it does not warn.
    /// </summary>
    [Theory]
    [InlineData("epub", "text", "the headers of section(s) 1 and the footers of section(s) 1, 2")]
    [InlineData("azw3", "text", "the headers of section(s) 1 and the footers of section(s) 1, 2")]
    [InlineData("mobi", "text", "the headers of section(s) 1 and the footers of section(s) 1, 2")]
    [InlineData("txt", "text", "the headers of section(s) 1 and the footers of section(s) 1, 2")]
    [InlineData("md", "text", "the headers of section(s) 1 and the footers of section(s) 1, 2")]
    [InlineData("epub", "drawing", "the headers of section(s) 1")]
    [InlineData("epub", "empty", null)]
    [InlineData("azw3", "empty", null)]
    [InlineData("mobi", "empty", null)]
    [InlineData("txt", "empty", null)]
    [InlineData("md", "empty", null)]
    public void ConvertingToAFormatWithoutPages_NamesTheHeadersAndFootersItLeavesOut(string format, string content, string? named)
    {
        using var fixture = new WordsFixture();
        var source = new Document();
        var builder = new DocumentBuilder(source);
        builder.Writeln("First section body.");
        builder.MoveToHeaderFooter(HeaderFooterType.HeaderPrimary);
        switch (content)
        {
            case "text":
                builder.Write("Running head");
                break;
            case "drawing":
                builder.InsertShape(Aspose.Words.Drawing.ShapeType.Rectangle, 40, 20);
                break;
        }
        builder.MoveToHeaderFooter(HeaderFooterType.FooterPrimary);
        if (content == "text")
        {
            builder.Write("First footer");
        }
        else
        {
            builder.InsertField("PAGE");
        }
        builder.MoveToDocumentEnd();
        builder.InsertBreak(BreakType.SectionBreakNewPage);
        builder.Write("Second section body.");
        if (content == "text")
        {
            // The second section shows the first one's header and has a footer of its own.
            builder.MoveToHeaderFooter(HeaderFooterType.FooterPrimary);
            builder.Write("Second footer");
        }
        bool withText = content == "text";
        string input = fixture.Temp.File($"ebook-{content}.docx");
        source.Save(input, SaveFormat.Docx);
        string output = fixture.Temp.File($"ebook-{content}.{format}");

        WordsConvertResult converted = WordsConvert.Run(fixture.Session, new WordsConvertRequest
        {
            Input = input,
            Output = TestOutput.At(output, format: format),
        });

        Warning[] leftOut = [.. (converted.Warnings ?? []).Where(static warning => warning.Code == WarningCodes.LossyConversion
            && warning.Message.Contains("leaves out", StringComparison.Ordinal))];
        string text = new Document(output).GetText();
        if (format is "txt" or "md")
        {
            // A page number would be a line of digits alone, such as the 1 of the first page.
            Assert.DoesNotContain(text.Split(['\r', '\n', '\v', '\f'], StringSplitOptions.TrimEntries),
                static line => line.Length > 0 && line.All(char.IsAsciiDigit));
        }
        if (named is null)
        {
            Assert.Empty(leftOut);
            Assert.True(!withText || text.Contains("Running head", StringComparison.Ordinal), text);
            return;
        }

        Warning warning = Assert.Single(leftOut);
        string reason = format is "txt" or "md" ? "a text file holds the body text alone, without pages" : "an e-book flows its text without pages";
        Assert.Equal($"The {format} output leaves out {named}: {reason}, so it has no headers or footers.", warning.Message);
        Assert.Equal("""{"content":["headers-footers"]}""", warning.Details?.ToJsonString());
        Assert.Contains("'aspose-cli words inspect <file> --detail sections'", warning.Hint, StringComparison.Ordinal);
        Assert.False(warning.AffectsCompleteness);
        Assert.DoesNotContain("Running head", text, StringComparison.Ordinal);
        Assert.DoesNotContain("First footer", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Second footer", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// An e-book leaves out only the headers and footers some page shows: a first-page one only
    /// when its section, or a later section that continues it, shows a different first page, and
    /// an even-page one only when odd and even pages differ. A primary header always shows.
    /// </summary>
    [Theory]
    [InlineData("first", false, null)]
    [InlineData("first", true, "the headers of section(s) 1")]
    [InlineData("even", false, null)]
    [InlineData("even", true, "the footers of section(s) 1")]
    [InlineData("continued", false, null)]
    [InlineData("continued", true, "the headers of section(s) 1")]
    [InlineData("primary", false, "the headers of section(s) 1")]
    public void ConvertingToAnEbook_NamesOnlyTheHeadersAndFootersAPageShows(string story, bool shown, string? named)
    {
        using var fixture = new WordsFixture();
        var source = new Document();
        var builder = new DocumentBuilder(source);
        builder.Write("First section body.");
        switch (story)
        {
            case "first" or "continued":
                builder.MoveToHeaderFooter(HeaderFooterType.HeaderFirst);
                builder.Write("Cover header");
                source.FirstSection.PageSetup.DifferentFirstPageHeaderFooter = story == "first" && shown;
                break;
            case "even":
                builder.MoveToHeaderFooter(HeaderFooterType.FooterEven);
                builder.Write("Even footer");
                source.FirstSection.PageSetup.OddAndEvenPagesHeaderFooter = shown;
                break;
            default:
                builder.MoveToHeaderFooter(HeaderFooterType.HeaderPrimary);
                builder.Write("Running head");
                break;
        }
        if (story == "continued")
        {
            // The second section defines no first-page header and continues the first one's.
            builder.MoveToDocumentEnd();
            builder.InsertBreak(BreakType.SectionBreakNewPage);
            builder.Write("Second section body.");
            source.LastSection.PageSetup.DifferentFirstPageHeaderFooter = shown;
            Assert.Null(source.LastSection.HeadersFooters[HeaderFooterType.HeaderFirst]);
        }
        string input = fixture.Temp.File($"shown-{story}-{shown}.docx");
        source.Save(input, SaveFormat.Docx);

        WordsConvertResult converted = WordsConvert.Run(fixture.Session, new WordsConvertRequest
        {
            Input = input,
            Output = TestOutput.At(fixture.Temp.File($"shown-{story}-{shown}.epub"), format: "epub"),
        });

        Warning[] leftOut = [.. (converted.Warnings ?? []).Where(static warning => warning.Code == WarningCodes.LossyConversion
            && warning.Message.Contains("leaves out", StringComparison.Ordinal))];
        if (named is null)
        {
            Assert.Empty(leftOut);
            return;
        }

        Assert.Contains($"leaves out {named}:", Assert.Single(leftOut).Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Each warning that names what a conversion leaves out gives its kind in
    /// <c>details.content</c>: the revisions a format cannot keep or mixes into the text, the
    /// editing restrictions, the headers and footers of an e-book and the comments a text output
    /// writes into the body. The warning that a format cannot keep every Word feature names none.
    /// </summary>
    [Theory]
    [InlineData("revised", "pdf", "cannot keep tracked changes", "revisions")]
    [InlineData("locked", "odt", "editing restrictions", "editing-restrictions")]
    [InlineData("headed", "epub", "an e-book flows its text", "headers-footers")]
    [InlineData("headed", "mobi", "an e-book flows its text", "headers-footers")]
    [InlineData("headed", "txt", "a text file holds the body text alone", "headers-footers")]
    [InlineData("headed", "md", "a text file holds the body text alone", "headers-footers")]
    [InlineData("commented", "pdf", "comment(s) in balloons", "comments")]
    [InlineData("commented", "pdf", "draws the tracked changes on its pages as markup", "revisions")]
    [InlineData("commented", "txt", "comment(s) into the body", "comments")]
    [InlineData("commented", "txt", "deleted and moved-from text", "revisions")]
    [InlineData("commented", "txt", "cannot preserve every Word feature", null)]
    public void ConversionWarnings_NameTheKindOfContentTheyLeaveOut(string source, string format, string fragment, string? kind)
    {
        using var fixture = new WordsFixture();
        string input = source switch
        {
            "revised" => RevisedDocument(fixture),
            "commented" => RevisedDocument(fixture, withComment: true),
            _ => Saved(fixture, source),
        };

        WordsConvertResult converted = WordsConvert.Run(fixture.Session, new WordsConvertRequest
        {
            Input = input,
            Output = TestOutput.At(fixture.Temp.File($"converted.{format}"), format: format),
        });

        Warning warning = Assert.Single(converted.Warnings ?? [], warning => warning.Code == WarningCodes.LossyConversion
            && warning.Message.Contains(fragment, StringComparison.Ordinal));
        Assert.Equal(kind is null ? null : $$"""{"content":["{{kind}}"]}""", warning.Details?.ToJsonString());

        // A document with read-only protection, or with a running header.
        static string Saved(WordsFixture fixture, string source)
        {
            var document = new Document();
            var builder = new DocumentBuilder(document);
            builder.Write("Body text");
            if (source == "locked")
            {
                document.Protect(ProtectionType.ReadOnly, "owner");
            }
            else
            {
                builder.MoveToHeaderFooter(HeaderFooterType.HeaderPrimary);
                builder.Write("Running head");
            }

            string path = fixture.Temp.File($"{source}.docx");
            document.Save(path, SaveFormat.Docx);
            return path;
        }
    }

    /// <summary>
    /// A document whose tracked change replaces "thirty" with "sixty", above a line "Other
    /// text."; <paramref name="withComment"/> adds a comment on the first paragraph.
    /// </summary>
    private static string RevisedDocument(WordsFixture fixture, bool withComment = false)
    {
        var source = new Document();
        var builder = new DocumentBuilder(source);
        builder.Writeln("The notice period is thirty days.");
        builder.Write("Other text.");
        if (withComment)
        {
            var comment = new Comment(source, "Ann", "A", DateTime.Now);
            comment.AppendChild(new Paragraph(source));
            comment.FirstParagraph!.AppendChild(new Run(source, "Reviewer note"));
            source.FirstSection.Body.FirstParagraph!.AppendChild(comment);
        }

        source.StartTrackRevisions("Ann", DateTime.Now);
        source.Range.Replace("thirty", "sixty");
        source.StopTrackRevisions();
        string input = fixture.Temp.File(withComment ? "commented-revision.docx" : "revised.docx");
        source.Save(input, SaveFormat.Docx);
        return input;
    }

    [Theory]
    [InlineData("docx", true)]
    [InlineData("pdf", true)]
    [InlineData("xps", true)]
    [InlineData("docm", false)]
    [InlineData("doc", false)]
    public void SavingAMacroDocument_DisclosesTheMacrosTheOutputDrops(string format, bool dropped)
    {
        using var fixture = new WordsFixture();
        string input = MacroDocument(fixture);

        WordsConvertResult converted = WordsConvert.Run(fixture.Session, new WordsConvertRequest
        {
            Input = input,
            Output = TestOutput.At(fixture.Temp.File("converted." + format), format: format),
        });
        Assert.Equal(dropped, (converted.Warnings ?? []).Any(static warning => warning.Code == WordsDiagnostics.MacrosDropped));
        if (Edits(format))
        {
            WordsEditResult edited = WordsEdit.Run(fixture.Session, new WordsEditRequest
            {
                Input = input,
                Batch = new WordsOpsBatch
                {
                    Ops = [new ReplaceTextOp { Find = "Macro", Replace = "Edited" }],
                },
                Output = TestOutput.At(fixture.Temp.File("edited." + format)),
            });
            Assert.Equal(dropped, (edited.Warnings ?? []).Any(static warning => warning.Code == WordsDiagnostics.MacrosDropped));
            Assert.Equal(!dropped, new Document(edited.Output!.Path).HasMacros);
        }
    }

    [Theory]
    [InlineData("dot", ".dot")]
    [InlineData("wordml", ".xml")]
    public void ConvertingAMacroDocument_ToALegacyWordFormat_KeepsTheMacros(string format, string extension)
    {
        using var fixture = new WordsFixture();
        string input = MacroDocument(fixture);

        WordsConvertResult converted = WordsConvert.Run(fixture.Session, new WordsConvertRequest
        {
            Input = input,
            Output = TestOutput.At(fixture.Temp.File("converted" + extension), format: format),
        });

        Assert.DoesNotContain(converted.Warnings ?? [], static warning => warning.Code == WordsDiagnostics.MacrosDropped);
        var reopened = new Document(converted.Outputs[0].Path);
        Assert.True(reopened.HasMacros);
        Assert.Contains("Sub Hello()", reopened.VbaProject.Modules["Module1"].SourceCode, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderingAMacroDocument_DoesNotReportDroppedMacros()
    {
        using var fixture = new WordsFixture();
        string input = MacroDocument(fixture);

        WordsRenderResult rendered = WordsRender.Run(fixture.Session, new WordsRenderRequest
        {
            Input = input,
            Output = TestOutput.At(fixture.Temp.File("page.png"), format: "png"),
        });

        Assert.DoesNotContain(rendered.Warnings ?? [], static warning => warning.Code == WordsDiagnostics.MacrosDropped);
    }

    [Fact]
    public void SplittingAndComparingAMacroDocument_DropTheMacrosOfADocxOutput()
    {
        using var fixture = new WordsFixture();
        string input = MacroDocument(fixture);

        WordsSplitResult split = WordsSplit.Run(fixture.Session, new WordsSplitRequest
        {
            Input = input,
            Output = new ResolvedDirectory(fixture.Temp.File("parts")),
            Plan = new Sdk.Addressing.SplitPlan(Sdk.Addressing.SplitBy.Sections, null, null, null),
        });
        WordsCompareResult compared = WordsCompare.Run(fixture.Session, new WordsCompareRequest
        {
            Left = input,
            Right = input,
            Output = TestOutput.At(fixture.Temp.File("redline.docx")),
        });

        Assert.Contains(split.Warnings ?? [], static warning => warning.Code == WordsDiagnostics.MacrosDropped);
        Assert.False(new Document(Assert.Single(split.Outputs).Path).HasMacros);
        Assert.Contains(compared.Warnings ?? [], static warning => warning.Code == WordsDiagnostics.MacrosDropped);
        Assert.False(new Document(compared.Output!.Path).HasMacros);
    }

    /// <summary>
    /// A redline written in a format without pages leaves the headers and footers out too, and
    /// compare names them as convert does; a Word redline keeps them and names none.
    /// </summary>
    [Theory]
    [InlineData("md", true)]
    [InlineData("mobi", true)]
    [InlineData("docx", false)]
    public void ComparingIntoAFormatWithoutPages_NamesTheHeadersAndFootersTheRedlineLeavesOut(string format, bool named)
    {
        using var fixture = new WordsFixture();
        string left = Headed("left.docx", "The notice period is thirty days.");
        string right = Headed("right.docx", "The notice period is sixty days.");

        WordsCompareResult compared = WordsCompare.Run(fixture.Session, new WordsCompareRequest
        {
            Left = left,
            Right = right,
            Output = TestOutput.At(fixture.Temp.File($"redline.{format}"), format: format),
        });

        Warning[] leftOut = [.. (compared.Warnings ?? []).Where(static warning => warning.Code == WarningCodes.LossyConversion
            && warning.Message.Contains("leaves out", StringComparison.Ordinal))];
        if (!named)
        {
            Assert.Empty(leftOut);
            return;
        }

        Warning warning = Assert.Single(leftOut);
        Assert.Equal("""{"content":["headers-footers"]}""", warning.Details?.ToJsonString());
        Assert.Contains("the headers of section(s) 1", warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Running head", new Document(compared.Output!.Path).GetText(), StringComparison.Ordinal);

        string Headed(string name, string body) => HeadedDocument(fixture, name, body);
    }

    /// <summary>
    /// A redline names what its format loses or mixes in as a convert of the same redline to that
    /// format does, the headers and footers once where the format leaves them out, but for the
    /// revisions it is written to show: a format that marks them (struck through, or as HTML
    /// del and ins) is not told it cannot keep them as revisions, as a convert is. Plain text and
    /// Markdown cannot mark them, whether the redline replaces, only inserts or only formats
    /// text, so they warn as a convert does, with a hint that names the formats that mark them
    /// instead of telling to accept or reject them.
    /// </summary>
    [Theory]
    [InlineData("md", "replace", 1)]
    [InlineData("txt", "replace", 1)]
    [InlineData("md", "insert", 1)]
    [InlineData("md", "format", 1)]
    [InlineData("mobi", "replace", 1)]
    [InlineData("epub", "replace", 1)]
    [InlineData("html", "replace", 0)]
    [InlineData("pdf", "replace", 0)]
    public void ComparingIntoAFormat_WarnsAsAConvertOfTheRedlineDoes_ButOfTheRevisionsItShows(string format, string change, int headersFooters)
    {
        const string RevisionsDetails = """{"content":["revisions"]}""";
        bool text = format is "md" or "txt";
        using var fixture = new WordsFixture();
        string left = HeadedDocument(fixture, "left.docx", "The notice period is thirty days.");
        string right = change switch
        {
            "replace" => HeadedDocument(fixture, "right.docx", "The notice period is sixty days."),
            "insert" => HeadedDocument(fixture, "right.docx", "The notice period is thirty days today."),
            _ => HeadedDocument(fixture, "right.docx", "The notice period is thirty days.", bold: true),
        };

        WordsCompareResult compared = Compared(fixture, left, right, format);

        Assert.Equal(
            change switch { "replace" => (true, true, false), "insert" => (true, false, false), _ => (false, false, true) },
            (compared.Revisions.InsertionCount > 0, compared.Revisions.DeletionCount > 0, compared.Revisions.FormatChangeCount > 0));
        string[] converted = Lossy(ConvertedRedline(fixture, left, right, format));
        string revisions = Assert.Single(converted, static warning => warning.EndsWith(RevisionsDetails, StringComparison.Ordinal));
        Assert.Equal(headersFooters, converted.Count(static warning => warning.EndsWith("""{"content":["headers-footers"]}""", StringComparison.Ordinal)));
        string[] expected = text ? converted : [.. converted.Where(warning => warning != revisions)];
        Assert.Equal(expected, Lossy(compared.Warnings));
        if (text)
        {
            Warning shown = Assert.Single(compared.Warnings!, static warning => warning.Details?.ToJsonString() == RevisionsDetails);
            Assert.Equal("To see what changed, write the redline as DOCX, PDF or HTML, which mark the changes.", shown.Hint);
        }
    }

    /// <summary>
    /// A header whose text a tracked change deletes still shows it struck through, so a convert
    /// to Markdown names it as left out; once the deletion is accepted the header is empty and
    /// nothing is named.
    /// </summary>
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void ConvertingAHeaderWithADeletion_NamesItUntilTheDeletionIsAccepted(bool accepted, bool named)
    {
        using var fixture = new WordsFixture();
        var source = new Document();
        var builder = new DocumentBuilder(source);
        builder.Write("Body text.");
        builder.MoveToHeaderFooter(HeaderFooterType.HeaderPrimary);
        builder.Write("Running head");
        source.StartTrackRevisions("Ann", DateTime.Now);
        source.FirstSection.HeadersFooters[HeaderFooterType.HeaderPrimary].Range.Replace("Running head", string.Empty);
        source.StopTrackRevisions();
        Assert.Equal(1, source.Revisions.Count);
        if (accepted)
        {
            source.AcceptAllRevisions();
        }

        string input = fixture.Temp.File("deleted-head.docx");
        source.Save(input, SaveFormat.Docx);

        WordsConvertResult converted = WordsConvert.Run(fixture.Session, new WordsConvertRequest
        {
            Input = input,
            Output = TestOutput.At(fixture.Temp.File("deleted-head.md"), format: "md"),
        });

        Assert.Equal(named ? 1 : 0, LeftOut(converted.Warnings).Length);
    }

    /// <summary>
    /// A comparison without a redline, a Word redline and a PDF one, which draws the revisions as
    /// markup as asked, lose nothing and warn of no loss.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("docx")]
    [InlineData("pdf")]
    public void ComparingWithoutARedlineOrIntoAWordOrPdfFormat_WarnsOfNoLoss(string? format)
    {
        using var fixture = new WordsFixture();
        string left = HeadedDocument(fixture, "left.docx", "The notice period is thirty days.");
        string right = HeadedDocument(fixture, "right.docx", "The notice period is sixty days.");

        WordsCompareResult compared = Compared(fixture, left, right, format);

        Assert.Empty(Lossy(compared.Warnings));
    }

    /// <summary>A document whose body is <paramref name="body"/>, in bold if <paramref name="bold"/>, under the primary header "Running head".</summary>
    private static string HeadedDocument(WordsFixture fixture, string name, string body, bool bold = false)
    {
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.Font.Bold = bold;
        builder.Write(body);
        builder.Font.Bold = false;
        builder.MoveToHeaderFooter(HeaderFooterType.HeaderPrimary);
        builder.Write("Running head");
        string path = fixture.Temp.File(name);
        document.Save(path, SaveFormat.Docx);
        return path;
    }

    private static WordsCompareResult Compared(WordsFixture fixture, string left, string right, string? format) =>
        WordsCompare.Run(fixture.Session, new WordsCompareRequest
        {
            Left = left,
            Right = right,
            Output = format is null ? null : TestOutput.At(fixture.Temp.File($"redline.{format}"), format: format),
        });

    /// <summary>The warnings of a convert to <paramref name="format"/> of the Word redline of two documents.</summary>
    private static IReadOnlyList<Warning>? ConvertedRedline(WordsFixture fixture, string left, string right, string format)
    {
        string redline = fixture.Temp.File("word-redline.docx");
        WordsCompare.Run(fixture.Session, new WordsCompareRequest { Left = left, Right = right, Output = TestOutput.At(redline) });
        return WordsConvert.Run(fixture.Session, new WordsConvertRequest
        {
            Input = redline,
            Output = TestOutput.At(fixture.Temp.File($"converted-redline.{format}"), format: format),
        }).Warnings;
    }

    /// <summary>The LOSSY_CONVERSION warnings by message and details, in order.</summary>
    private static string[] Lossy(IReadOnlyList<Warning>? warnings) =>
        [.. (warnings ?? []).Where(static warning => warning.Code == WarningCodes.LossyConversion)
            .Select(static warning => warning.Message + " " + warning.Details?.ToJsonString()).Order(StringComparer.Ordinal)];

    /// <summary>The LOSSY_CONVERSION warnings that name headers and footers left out.</summary>
    private static Warning[] LeftOut(IReadOnlyList<Warning>? warnings) =>
        [.. (warnings ?? []).Where(static warning => warning.Code == WarningCodes.LossyConversion
            && warning.Message.Contains("leaves out", StringComparison.Ordinal))];

    /// <summary>
    /// An edit's verification reports a resource that loading left out as an issue only when the
    /// batch left it out of the output: a picture the input links to and does not store stays a
    /// link in the output, so it is disclosed and passes, while a picture an operation's Markdown
    /// names on the network is missing from the output and fails.
    /// </summary>
    [Theory]
    [InlineData("linked", true)]
    [InlineData("inserted", false)]
    public void EditVerification_CountsOnlyTheResourcesTheBatchLeftOut(string example, bool ok)
    {
        using var fixture = new WordsFixture();
        const string link = "http://example.invalid/logo.png";
        var source = new Document();
        var builder = new DocumentBuilder(source);
        builder.Writeln("Clause one.");
        if (example == "linked")
        {
            var linked = new Aspose.Words.Drawing.Shape(source, Aspose.Words.Drawing.ShapeType.Image)
            {
                Width = 40,
                Height = 40,
                WrapType = Aspose.Words.Drawing.WrapType.Inline,
            };
            linked.ImageData.SourceFullName = link;
            builder.InsertNode(linked);
        }
        string input = fixture.Temp.File($"resources-{example}.docx");
        source.Save(input, SaveFormat.Docx);
        string output = fixture.Temp.File($"resources-{example}-out.docx");

        WordsEditResult result = WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = input,
            Batch = new WordsOpsBatch
            {
                Ops = example == "linked"
                    ? [new ReplaceTextOp { Find = "one", Replace = "two" }]
                    : [new SetFooterOp { Markdown = $"![logo]({link})" }],
            },
            Output = TestOutput.At(output),
            Verify = true,
        });

        Assert.Contains(result.Warnings ?? [], static warning => warning.Code == WarningCodes.RemoteResourcesBlocked);
        Assert.Equal(ok, result.Verification!.Ok);
        Assert.Equal(ok ? Array.Empty<string>() : ["REMOTE_RESOURCES_BLOCKED"], result.Verification.Issues.Select(static issue => issue.Code));
        if (ok)
        {
            Assert.Contains(new Document(output).GetChildNodes(NodeType.Shape, true).Cast<Aspose.Words.Drawing.Shape>(),
                static shape => shape.ImageData.SourceFullName == link);
        }
    }

    [Fact]
    public void InspectingAMacroDocument_ReportsItsMacros()
    {
        using var fixture = new WordsFixture();

        Assert.True(WordsInspect.Run(fixture.Session, new WordsInspectRequest { Input = MacroDocument(fixture) }).Document.HasMacros);
        Assert.False(WordsInspect.Run(fixture.Session, new WordsInspectRequest { Input = fixture.CreateReport() }).Document.HasMacros);
    }

    private static string MacroDocument(WordsFixture fixture)
    {
        var source = new Document();
        new DocumentBuilder(source).Write("Macro text");
        source.VbaProject = new VbaProject { Name = "Project" };
        source.VbaProject.Modules.Add(new VbaModule
        {
            Name = "Module1",
            Type = VbaModuleType.ProceduralModule,
            SourceCode = "Sub Hello()\r\nEnd Sub\r\n",
        });
        string input = fixture.Temp.File("macros.docm");
        source.Save(input, SaveFormat.Docm);
        return input;
    }

    [Fact]
    public void SplitByHeading_KeepsPageSetupAndHeaders()
    {
        using var fixture = new WordsFixture();
        var source = new Document();
        var builder = new DocumentBuilder(source);
        builder.PageSetup.PaperSize = PaperSize.A5;
        builder.MoveToHeaderFooter(HeaderFooterType.HeaderPrimary);
        builder.Write("Brand");
        builder.MoveToDocumentEnd();
        foreach (string title in new[] { "One", "Two" })
        {
            builder.ParagraphFormat.StyleIdentifier = StyleIdentifier.Heading1;
            builder.Writeln(title);
            builder.ParagraphFormat.StyleIdentifier = StyleIdentifier.Normal;
            builder.Writeln($"Body {title}");
        }

        string input = fixture.Temp.File("chapters.docx");
        source.Save(input, SaveFormat.Docx);

        WordsSplitResult result = WordsSplit.Run(fixture.Session, new WordsSplitRequest
        {
            Input = input,
            Output = new ResolvedDirectory(fixture.Temp.File("parts")),
            Plan = new Sdk.Addressing.SplitPlan(Sdk.Addressing.SplitBy.Heading1, null, null, null),
        });

        Assert.Equal(2, result.Outputs.Count);
        foreach (WordsPartOutput part in result.Outputs)
        {
            var document = new Document(part.Path);
            Assert.Equal(PaperSize.A5, document.FirstSection.PageSetup.PaperSize);
            Assert.Equal("Brand", document.FirstSection.HeadersFooters[HeaderFooterType.HeaderPrimary].GetText().Trim());
        }

        Assert.DoesNotContain("Body Two", new Document(result.Outputs[0].Path).GetText(), StringComparison.Ordinal);
    }

    [Fact]
    public void SplitByHeading_KeepsTheBlocksBeforeTheFirstHeadingInALeadingPart()
    {
        using var fixture = new WordsFixture();
        var source = new Document();
        var builder = new DocumentBuilder(source);
        builder.Writeln("Contents");
        foreach (string title in new[] { "One", "Two" })
        {
            builder.ParagraphFormat.StyleIdentifier = StyleIdentifier.Heading1;
            builder.Writeln(title);
            builder.ParagraphFormat.StyleIdentifier = StyleIdentifier.Normal;
            builder.Write($"Body {title}");
            if (title == "One")
            {
                builder.Writeln();
            }
        }

        string input = fixture.Temp.File("preamble.docx");
        source.Save(input, SaveFormat.Docx);

        WordsSplitResult result = WordsSplit.Run(fixture.Session, new WordsSplitRequest
        {
            Input = input,
            Output = new ResolvedDirectory(fixture.Temp.File("preamble-parts")),
            Plan = new Sdk.Addressing.SplitPlan(Sdk.Addressing.SplitBy.Heading1, null, null, null),
        });

        Assert.Equal(["1", "2-3", "4-5"], result.Outputs.Select(static part => part.Blocks));
        string[] texts = result.Outputs
            .Select(part => string.Join("|", WordsRead.Run(fixture.Session, new WordsReadRequest { Input = part.Path })
                .Blocks.Select(static block => block.Text)))
            .ToArray();
        Assert.Equal(["Contents", "One|Body One", "Two|Body Two"], texts);
    }

    // Reports a fixed license state; the real gate has already applied the SDK license.
    private sealed class FixedGate(LicenseState state) : ILicenseGate
    {
        public LicenseResolution Resolution => LicenseResolution.None;
        public string Identity => "fixed";
        public LicenseState EnsureApplied() => state;
    }

    // Whether edit writes the format; the others are written only by convert.
    private static bool Edits(string format) => WordsFormats.Editable.Any(written => written.Id == format);
}
