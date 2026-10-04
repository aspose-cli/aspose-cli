using System.Text.RegularExpressions;
using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.TestKit;
using Aspose.Words;
using Aspose.Words.Vba;
using Aspose.Words.Saving;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

public sealed class WordsDisclosureTests
{
    private const string BannerText =
        "Created with an evaluation copy of Aspose.Words. To remove all limitations, you can use Free Temporary License https://products.aspose.com/words/temporary-license/";

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
        builder.Writeln(BannerText);
        builder.Writeln("This document was truncated here because of the evaluation notice we quote.");
        builder.Write("Body");
        string input = fixture.Temp.File("quotes.docx");
        source.Save(input, SaveFormat.Docx);

        var loader = new WordsDocumentLoader(ProductTestBudgets.Create<WordsModule>(), new FixedGate(state));
        using LoadedDocument loaded = loader.Open(input, null);
        var index = new DocumentBlockIndex(loaded.Document, loaded.Evaluation);

        bool evaluation = state == LicenseState.Evaluation;
        Assert.Equal(evaluation, loaded.EvaluationInputTruncated);
        // Under evaluation the SDK may add its own banner; the quoted one is skipped only there.
        Assert.True(evaluation ? index.Count <= blocks : index.Count == blocks);
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

        WordsEditResult result = fixture.Engine.ApplyOps(input, new WordsOpsBatch
        {
            Ops = [new ReplaceTextOp { Find = "Locked", Replace = "Changed" }],
        }, new WordsEditRequest { OutputPath = output });

        Assert.Contains(result.Warnings ?? [], static warning => warning.Code == WarningCodes.ProtectionNotEnforced);
        Assert.Equal(ProtectionType.ReadOnly, new Document(output).ProtectionType);
    }

    [Fact]
    public void EditingIntoAFormatThatCannotHoldWordFeatures_DisclosesTheLoss()
    {
        using var fixture = new WordsFixture();
        var source = new Document();
        new DocumentBuilder(source).Write("Locked text");
        source.Protect(ProtectionType.ReadOnly, "owner");
        string input = fixture.Temp.File("locked.docx");
        source.Save(input, SaveFormat.Docx);
        var batch = new WordsOpsBatch { Ops = [new ReplaceTextOp { Find = "Locked", Replace = "Changed" }] };

        WordsEditResult text = fixture.Engine.ApplyOps(input, batch, new WordsEditRequest { OutputPath = fixture.Temp.File("edited.txt") });
        WordsEditResult word = fixture.Engine.ApplyOps(input, batch, new WordsEditRequest { OutputPath = fixture.Temp.File("edited.docx") });

        // One LOSSY_CONVERSION names the restrictions; the format's own one names other features.
        Warning lost = Assert.Single(text.Warnings ?? [], static warning => warning.Code == WarningCodes.LossyConversion
            && Regex.IsMatch(warning.Message + warning.Hint, "restrict|protect", RegexOptions.IgnoreCase));
        Assert.Contains("cannot keep the readOnly editing restrictions", lost.Message, StringComparison.Ordinal);
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

        WordsConvertResult converted = fixture.Engine.Convert(input, new WordsConvertRequest
        {
            TargetFormatId = format,
            OutputPath = fixture.Temp.File("converted." + format),
        });

        Assert.Equal(lost, (converted.Warnings ?? []).Any(static warning => warning.Code == WarningCodes.LossyConversion
            && warning.Message.Contains("cannot keep the readOnly editing restrictions", StringComparison.Ordinal)));
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

        WordsConvertResult fromPdf = fixture.Engine.Convert(pdf, new WordsConvertRequest { TargetFormatId = "docx", OutputPath = fixture.Temp.File("from-pdf.docx") });
        WordsConvertResult fromDocx = fixture.Engine.Convert(docx, new WordsConvertRequest { TargetFormatId = "docx", OutputPath = fixture.Temp.File("from-docx.docx") });

        WordsEditResult edited = fixture.Engine.ApplyOps(pdf, new WordsOpsBatch
        {
            Ops = [new ReplaceTextOp { Find = "one", Replace = "two" }],
        }, new WordsEditRequest { OutputPath = fixture.Temp.File("edited.docx") });

        Warning lossy = Assert.Single(fromPdf.Warnings ?? [], static warning => warning.Code == WarningCodes.LossyConversion);
        Assert.Contains("may become body text, or headers and footers in which a number", lossy.Message, StringComparison.Ordinal);
        Assert.Contains("--detail sections fields", lossy.Hint, StringComparison.Ordinal);
        Assert.Equal(lossy, Assert.Single(edited.Warnings ?? [], static warning => warning.Code == WarningCodes.LossyConversion));
        Assert.DoesNotContain(fromDocx.Warnings ?? [], static warning => warning.Code == WarningCodes.LossyConversion);
    }

    [Theory]
    [InlineData(true, "docx", false)]
    [InlineData(false, "docx", true)]
    [InlineData(false, "rtf", true)]
    [InlineData(false, "odt", true)]
    [InlineData(false, "txt", false)]
    public void EditingARevisedDocument_DisclosesTheRevisionsTheOutputKeeps(bool accept, string extension, bool disclosed)
    {
        using var fixture = new WordsFixture();
        var source = new Document();
        var builder = new DocumentBuilder(source);
        builder.Writeln("The notice period is thirty days.");
        builder.Write("Other text.");
        source.StartTrackRevisions("Ann", DateTime.Now);
        source.Range.Replace("thirty", "sixty");
        source.StopTrackRevisions();
        string input = fixture.Temp.File("revised.docx");
        source.Save(input, SaveFormat.Docx);
        string output = fixture.Temp.File("edited." + extension);

        WordsEditResult result = fixture.Engine.ApplyOps(input, new WordsOpsBatch
        {
            Ops = accept ? [new AcceptRevisionsOp()] : [new ReplaceTextOp { Find = "Other", Replace = "More" }],
        }, new WordsEditRequest { OutputPath = output });

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
    // A text output names the deleted text it mixes into the body instead.
    [InlineData("txt", false, false)]
    public void SavingARevisedDocument_DisclosesWhetherTheOutputKeepsTheRevisions(string format, bool kept, bool dropped)
    {
        using var fixture = new WordsFixture();
        var source = new Document();
        var builder = new DocumentBuilder(source);
        builder.Writeln("The notice period is thirty days.");
        builder.Write("Other text.");
        source.StartTrackRevisions("Ann", DateTime.Now);
        source.Range.Replace("thirty", "sixty");
        source.StopTrackRevisions();
        string input = fixture.Temp.File("revised.docx");
        source.Save(input, SaveFormat.Docx);

        WordsConvertResult converted = fixture.Engine.Convert(input, new WordsConvertRequest
        {
            TargetFormatId = format,
            OutputPath = fixture.Temp.File("converted." + format),
        });
        WordsEditResult edited = fixture.Engine.ApplyOps(input, new WordsOpsBatch
        {
            Ops = [new ReplaceTextOp { Find = "Other", Replace = "More" }],
        }, new WordsEditRequest { OutputPath = fixture.Temp.File("edited." + format) });

        foreach (IReadOnlyList<Warning>? warnings in new[] { converted.Warnings, edited.Warnings })
        {
            Assert.Equal(kept, (warnings ?? []).Any(static warning => warning.Code == WordsDiagnostics.TrackedChangesPresent));
            Assert.Equal(dropped, (warnings ?? []).Any(static warning => warning.Code == WarningCodes.LossyConversion
                && warning.Message.Contains("cannot keep tracked changes", StringComparison.Ordinal)));
        }
    }

    [Fact]
    public void RenderingARevisedDocument_DoesNotReportLostRevisions()
    {
        using var fixture = new WordsFixture();
        var source = new Document();
        var builder = new DocumentBuilder(source);
        builder.Write("The notice period is thirty days.");
        source.StartTrackRevisions("Ann", DateTime.Now);
        source.Range.Replace("thirty", "sixty");
        source.StopTrackRevisions();
        string input = fixture.Temp.File("revised.docx");
        source.Save(input, SaveFormat.Docx);

        // A render shows the document as it looks; the source keeps its revisions.
        WordsRenderResult rendered = fixture.Engine.Render(input, new WordsRenderRequest
        {
            TargetFormatId = "png",
            OutputPath = fixture.Temp.File("page.png"),
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
        string input = CommentedRevision(fixture);

        WordsConvertResult converted = fixture.Engine.Convert(input, new WordsConvertRequest
        {
            TargetFormatId = format,
            OutputPath = fixture.Temp.File("converted." + format),
        });
        WordsEditResult edited = fixture.Engine.ApplyOps(input, new WordsOpsBatch
        {
            Ops = [new ReplaceTextOp { Find = "Other", Replace = "More" }],
        }, new WordsEditRequest { OutputPath = fixture.Temp.File("edited." + format) });

        foreach (IReadOnlyList<Warning>? warnings in new[] { converted.Warnings, edited.Warnings })
        {
            string[] messages = (warnings ?? []).Where(static warning => warning.Code == WarningCodes.LossyConversion)
                .Select(static warning => warning.Message).ToArray();
            Assert.Equal(mixed, messages.Any(message => message.Contains($"{format} output writes the text of 1 comment(s) into the body", StringComparison.Ordinal)));
            Assert.Equal(mixed, messages.Any(message => message.Contains("deleted and moved-from text", StringComparison.Ordinal)));
        }
    }

    [Fact]
    public void SavingToText_AfterRemovingCommentsAndAcceptingRevisions_MixesNothing()
    {
        using var fixture = new WordsFixture();
        string input = CommentedRevision(fixture);
        string output = fixture.Temp.File("clean.txt");

        WordsEditResult result = fixture.Engine.ApplyOps(input, new WordsOpsBatch
        {
            Ops = [new RemoveCommentsOp(), new AcceptRevisionsOp()],
        }, new WordsEditRequest { OutputPath = output });

        Warning lossy = Assert.Single(result.Warnings ?? [], static warning => warning.Code == WarningCodes.LossyConversion);
        Assert.Equal("Conversion to txt cannot preserve every Word feature.", lossy.Message);
        string text = File.ReadAllText(output);
        Assert.DoesNotContain("Reviewer note", text, StringComparison.Ordinal);
        Assert.Contains("sixty days", text, StringComparison.Ordinal);
        Assert.DoesNotContain("thirty", text, StringComparison.Ordinal);
    }

    private static string CommentedRevision(WordsFixture fixture)
    {
        var source = new Document();
        var builder = new DocumentBuilder(source);
        builder.Writeln("The notice period is thirty days.");
        builder.Write("Other text.");
        var comment = new Comment(source, "Ann", "A", DateTime.Now);
        comment.AppendChild(new Paragraph(source));
        comment.FirstParagraph!.AppendChild(new Run(source, "Reviewer note"));
        source.FirstSection.Body.FirstParagraph!.AppendChild(comment);
        source.StartTrackRevisions("Ann", DateTime.Now);
        source.Range.Replace("thirty", "sixty");
        source.StopTrackRevisions();
        string input = fixture.Temp.File("commented-revision.docx");
        source.Save(input, SaveFormat.Docx);
        return input;
    }

    [Theory]
    [InlineData("docx", true)]
    [InlineData("pdf", true)]
    [InlineData("docm", false)]
    [InlineData("doc", false)]
    public void SavingAMacroDocument_DisclosesTheMacrosTheOutputDrops(string format, bool dropped)
    {
        using var fixture = new WordsFixture();
        string input = MacroDocument(fixture);

        WordsConvertResult converted = fixture.Engine.Convert(input, new WordsConvertRequest
        {
            TargetFormatId = format,
            OutputPath = fixture.Temp.File("converted." + format),
        });
        WordsEditResult edited = fixture.Engine.ApplyOps(input, new WordsOpsBatch
        {
            Ops = [new ReplaceTextOp { Find = "Macro", Replace = "Edited" }],
        }, new WordsEditRequest { OutputPath = fixture.Temp.File("edited." + format) });

        Assert.Equal(dropped, (converted.Warnings ?? []).Any(static warning => warning.Code == WordsDiagnostics.MacrosDropped));
        Assert.Equal(dropped, (edited.Warnings ?? []).Any(static warning => warning.Code == WordsDiagnostics.MacrosDropped));
        if (format is "docx" or "docm" or "doc")
        {
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

        WordsConvertResult converted = fixture.Engine.Convert(input, new WordsConvertRequest
        {
            TargetFormatId = format,
            OutputPath = fixture.Temp.File("converted" + extension),
        });

        Assert.DoesNotContain(converted.Warnings ?? [], static warning => warning.Code == WordsDiagnostics.MacrosDropped);
        var reopened = new Document(converted.Output!.Path);
        Assert.True(reopened.HasMacros);
        Assert.Contains("Sub Hello()", reopened.VbaProject.Modules["Module1"].SourceCode, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderingAMacroDocument_DoesNotReportDroppedMacros()
    {
        using var fixture = new WordsFixture();
        string input = MacroDocument(fixture);

        WordsRenderResult rendered = fixture.Engine.Render(input, new WordsRenderRequest
        {
            TargetFormatId = "png",
            OutputPath = fixture.Temp.File("page.png"),
        });

        Assert.DoesNotContain(rendered.Warnings ?? [], static warning => warning.Code == WordsDiagnostics.MacrosDropped);
    }

    [Fact]
    public void SplittingAndComparingAMacroDocument_DropTheMacrosOfADocxOutput()
    {
        using var fixture = new WordsFixture();
        string input = MacroDocument(fixture);

        WordsSplitResult split = fixture.Engine.Split(input, new WordsSplitRequest
        {
            By = "section",
            OutputDirectory = fixture.Temp.File("parts"),
        });
        WordsCompareResult compared = fixture.Engine.Compare(input, input, new WordsCompareRequest
        {
            OutputPath = fixture.Temp.File("redline.docx"),
        });

        Assert.Contains(split.Warnings ?? [], static warning => warning.Code == WordsDiagnostics.MacrosDropped);
        Assert.False(new Document(Assert.Single(split.Outputs).Output.Path).HasMacros);
        Assert.Contains(compared.Warnings ?? [], static warning => warning.Code == WordsDiagnostics.MacrosDropped);
        Assert.False(new Document(compared.Output!.Path).HasMacros);
    }

    [Fact]
    public void InspectingAMacroDocument_ReportsItsMacros()
    {
        using var fixture = new WordsFixture();

        Assert.True(fixture.Engine.GetInfo(MacroDocument(fixture), new DocumentInfoRequest()).Document.HasMacros);
        Assert.False(fixture.Engine.GetInfo(fixture.CreateReport(), new DocumentInfoRequest()).Document.HasMacros);
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

        WordsSplitResult result = fixture.Engine.Split(input, new WordsSplitRequest
        {
            By = "heading1",
            OutputDirectory = fixture.Temp.File("parts"),
        });

        Assert.Equal(2, result.Outputs.Count);
        foreach (SplitOutput part in result.Outputs)
        {
            var document = new Document(part.Output.Path);
            Assert.Equal(PaperSize.A5, document.FirstSection.PageSetup.PaperSize);
            Assert.Equal("Brand", document.FirstSection.HeadersFooters[HeaderFooterType.HeaderPrimary].GetText().Trim());
        }

        Assert.DoesNotContain("Body Two", new Document(result.Outputs[0].Output.Path).GetText(), StringComparison.Ordinal);
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

        WordsSplitResult result = fixture.Engine.Split(input, new WordsSplitRequest
        {
            By = "heading1",
            OutputDirectory = fixture.Temp.File("preamble-parts"),
        });

        Assert.Equal(["blocks-1-1", "blocks-2-3", "blocks-4-5"], result.Outputs.Select(static part => part.Source));
        string[] texts = result.Outputs
            .Select(part => string.Join("|", fixture.Engine.Read(part.Output.Path, new DocumentReadRequest())
                .Blocks.Select(static block => block.Text)))
            .ToArray();
        Assert.Equal(["Contents", "One|Body One", "Two|Body Two"], texts);
    }

    // Reports a fixed license state; the real gate has already applied the SDK license.
    private sealed class FixedGate(LicenseState state) : ILicenseGate
    {
        public bool IsApplicable => true;
        public LicenseResolution Resolution => throw new NotSupportedException();
        public string Identity => "fixed";
        public LicenseState EnsureApplied() => state;
    }
}
