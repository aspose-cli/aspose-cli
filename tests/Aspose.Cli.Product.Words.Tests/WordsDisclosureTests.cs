using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.TestKit;
using Aspose.Words;
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

        Assert.Contains(result.Warnings ?? [], static warning => warning.Code == WordsDiagnostics.ProtectionNotEnforced);
        Assert.Equal(ProtectionType.ReadOnly, new Document(output).ProtectionType);
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
