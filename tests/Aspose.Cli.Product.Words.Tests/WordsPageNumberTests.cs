using System.Drawing;
using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Words;
using Aspose.Words.Fields;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

public sealed class WordsPageNumberTests : IClassFixture<WordsFixture>
{
    private readonly WordsFixture _fixture;

    public WordsPageNumberTests(WordsFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData("header", false)]
    [InlineData("header", true)]
    [InlineData("footer", false)]
    [InlineData("footer", true)]
    public void SetPageNumbers_PreservesContentAndReusesPageField(string location, bool existingPage)
    {
        string input = _fixture.Temp.File($"numbering-{location}-{existingPage}.docx");
        string output = _fixture.Temp.File($"numbering-{location}-{existingPage}-changed.docx");
        HeaderFooterType type = location == "header" ? HeaderFooterType.HeaderPrimary : HeaderFooterType.FooterPrimary;
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.Write("First section");
        builder.InsertBreak(BreakType.SectionBreakNewPage);
        builder.Write("Second section");
        for (int index = 0; index < document.Sections.Count; index++)
        {
            // Evaluation mode has already written a header holding its banner.
            HeaderFooter? container = document.Sections[index].HeadersFooters[type];
            if (container is null)
            {
                container = new HeaderFooter(document, type);
                document.Sections[index].HeadersFooters.Add(container);
            }
            var label = new Paragraph(document);
            var run = new Run(document, $"CLIENT CONFIDENTIAL | SECTION {index + 1}");
            run.Font.Name = "Arial";
            run.Font.Size = 9.5;
            run.Font.Bold = true;
            run.Font.Color = Color.Navy;
            label.AppendChild(run);
            container.AppendChild(label);
            var fieldParagraph = new Paragraph(document);
            container.AppendChild(fieldParagraph);
            builder.MoveTo(fieldParagraph);
            if (existingPage)
            {
                builder.Write("Page ");
                builder.InsertField("PAGE");
                builder.Write(" of ");
            }
            else
            {
                builder.Write("Total pages: ");
            }
            builder.InsertField("NUMPAGES");
        }
        document.Sections[0].PageSetup.RestartPageNumbering = true;
        document.Sections[0].PageSetup.PageStartingNumber = 3;
        document.Sections[0].PageSetup.PageNumberStyle = NumberStyle.LowercaseLetter;
        document.UpdateFields();
        document.Save(input);

        var original = new Document(input);
        HeaderFooter originalTarget = original.Sections[1].HeadersFooters[type];
        string firstSectionText = original.Sections[0].HeadersFooters[type].GetText();
        int retainedOtherFields = originalTarget.Range.Fields.Cast<Field>().Count(field => field.Type != FieldType.FieldPage);
        bool labelPresent = originalTarget.GetText().Contains("CLIENT CONFIDENTIAL", StringComparison.Ordinal);
        if (_fixture.LicenseState == LicenseState.Licensed)
        {
            Assert.True(labelPresent);
        }

        _fixture.Engine.ApplyOps(input, new WordsOpsBatch
        {
            Ops =
            [
                new SetPageNumbersOp { Location = location, Alignment = "right", Section = 2, Start = 7, Format = "lowerRoman" },
                new SetPageNumbersOp { Location = location, Alignment = "center", Section = 2, Start = 9, Format = "upperRoman" },
                new UpdateFieldsOp(),
            ],
        }, new WordsEditRequest { Output = TestOutput.At(output) });

        var changed = new Document(output);
        HeaderFooter target = changed.Sections[1].HeadersFooters[type];
        if (labelPresent)
        {
            Assert.Contains("CLIENT CONFIDENTIAL | SECTION 2", target.GetText(), StringComparison.Ordinal);
            Run retained = Assert.Single(target.GetChildNodes(NodeType.Run, true).Cast<Run>(),
                run => run.Text.Contains("CLIENT CONFIDENTIAL", StringComparison.Ordinal));
            Assert.Equal("Arial", retained.Font.Name);
            Assert.Equal(9.5, retained.Font.Size);
            Assert.True(retained.Font.Bold);
            Assert.Equal(Color.Navy.ToArgb(), retained.Font.Color.ToArgb());
        }
        Assert.Equal(retainedOtherFields, target.Range.Fields.Cast<Field>().Count(field => field.Type != FieldType.FieldPage));
        Field page = Assert.Single(target.Range.Fields.Cast<Field>(), field => field.Type == FieldType.FieldPage);
        Paragraph pageParagraph = (Paragraph)page.Start.GetAncestor(NodeType.Paragraph);
        Assert.Equal(ParagraphAlignment.Center, pageParagraph.ParagraphFormat.Alignment);
        Assert.True(changed.Sections[1].PageSetup.RestartPageNumbering);
        Assert.Equal(9, changed.Sections[1].PageSetup.PageStartingNumber);
        Assert.Equal(NumberStyle.UppercaseRoman, changed.Sections[1].PageSetup.PageNumberStyle);
        Assert.Equal("IX", page.Result);
        Assert.Equal(firstSectionText, changed.Sections[0].HeadersFooters[type].GetText());
        Assert.Equal(3, changed.Sections[0].PageSetup.PageStartingNumber);
        Assert.Equal(NumberStyle.LowercaseLetter, changed.Sections[0].PageSetup.PageNumberStyle);
    }
}
