using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>The Words ops schema and the parser give the same answer for block addresses and operation rules.</summary>
public sealed class WordsOpContractTests
{
    [Theory]
    [InlineData("""{"op":"delete_blocks","target":{"blocks":"0"}}""")]
    [InlineData("""{"op":"set_text","target":{"block":1,"find":"Intro"},"text":"a"}""")]
    [InlineData("""{"op":"set_text","target":{"find":""},"text":"a"}""")]
    [InlineData("""{"op":"insert_markdown","at":{"block":1},"position":"after","markdown":""}""")]
    [InlineData("""{"op":"set_header","paragraphs":[{"text":"a"}],"markdown":"b"}""")]
    [InlineData("""{"op":"accept_revisions","author":""}""")]
    [InlineData("""{"op":"format_text","target":{"block":1},"fontSize":null}""")]
    [InlineData("""{"op":"set_watermark","path":"logo.png","color":"#FF0000"}""")]
    [InlineData("""{"op":"insert_section","at":1}""")]
    [InlineData("""{"op":"insert_section","position":"after"}""")]
    [InlineData("""{"op":"insert_section","at":0,"position":"before"}""")]
    [InlineData("""{"op":"set_page_setup","section":1}""")]
    [InlineData("""{"op":"set_page_setup","pageSize":{"width":500}}""")]
    [InlineData("""{"op":"format_text","target":{"block":1},"color":"#12345"}""")]
    [InlineData("""{"op":"insert_hyperlink","at":{"block":1},"position":"after","text":"a","url":"docs/a"}""")]
    [InlineData("""{"op":"insert_hyperlink","at":{"block":1},"position":"after","text":"a","url":"file:///c:/a.txt"}""")]
    [InlineData("""{"op":"insert_hyperlink","at":{"block":1},"position":"after","text":"a","url":"javascript:alert(1)"}""")]
    [InlineData("""{"op":"import_sections","path":" "}""")]
    [InlineData("""{"op":"repeat_table_row","target":{"block":1}}""")]
    [InlineData("""{"op":"repeat_table_row","target":{"block":1},"items":[],"path":"items.csv"}""")]
    [InlineData("""{"op":"repeat_table_row","target":{"block":1},"row":0,"items":[]}""")]
    [InlineData("""{"op":"repeat_table_row","target":{"block":1},"items":[{"code":{"a":1}}]}""")]
    [InlineData("""{"op":"mail_merge","items":[{"Name":["Ava"]}]}""")]
    [InlineData("""{"op":"set_table_pagination","target":{"block":1}}""")]
    [InlineData("""{"op":"set_default_font","fontSize":11}""")]
    [InlineData("""{"op":"set_table_pagination","target":{"block":1},"headerRowCount":-1}""")]
    [InlineData("""{"op":"set_table_pagination","target":{"block":1},"keepTogether":"yes"}""")]
    public void ParserAndSchema_RejectTheSameInvalidOperation(string operation)
    {
        string batch = $$"""{"ops":[{{operation}}]}""";

        CliException error = Assert.Throws<CliException>(() => Parse(batch));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.False(OpsSchema<WordsModule>.Accepts(batch), "The schema accepted an operation the parser rejects.");
    }

    [Theory]
    [InlineData("""{"op":"delete_blocks","target":{"blocks":"1-3,7"}}""")]
    [InlineData("""{"op":"set_footer","markdown":"_Confidential_"}""")]
    [InlineData("""{"op":"set_properties","custom":{"Reviewer":null}}""")]
    [InlineData("""{"op":"mail_merge","items":[{"Name":null}]}""")]
    [InlineData("""{"op":"mail_merge","items":[{"Salary":28000,"Active":true}]}""")]
    [InlineData("""{"op":"repeat_table_row","target":{"block":1},"items":[{"code":null,"price":1}]}""")]
    [InlineData("""{"op":"insert_section","at":1,"position":"after"}""")]
    [InlineData("""{"op":"insert_section","at":2,"position":"before","pageSetup":{"pageSize":{"width":500,"height":700}}}""")]
    [InlineData("""{"op":"set_page_setup","pageSize":{"preset":"tabloid"},"orientation":"landscape"}""")]
    [InlineData("""{"op":"set_default_font","eastAsianFont":"SimSun"}""")]
    [InlineData("""{"op":"format_text","target":{"blocks":"2-3"},"latinFont":"Arial","eastAsianFont":"SimSun"}""")]
    [InlineData("""{"op":"repeat_table_row","target":{"find":"{{code}}"},"items":[]}""")]
    [InlineData("""{"op":"repeat_table_row","target":{"block":2},"row":2,"items":[{"code":"A-1","extra":""}]}""")]
    [InlineData("""{"op":"repeat_table_row","target":{"block":2},"path":"items.csv"}""")]
    [InlineData("""{"op":"insert_hyperlink","at":{"block":1},"position":"after","text":"a","url":"https://example.com/a"}""")]
    [InlineData("""{"op":"insert_hyperlink","at":{"block":1},"position":"after","text":"a","url":"mailto:team@example.com"}""")]
    [InlineData("""{"op":"set_table_pagination","target":{"find":"Agenda"},"keepTogether":true}""")]
    [InlineData("""{"op":"set_table_pagination","target":{"block":2},"headerRowCount":0,"allowRowBreakAcrossPages":true,"keepWithNext":false}""")]
    [InlineData("""{"op":"accept_revisions","revisions":[2,1]}""")]
    [InlineData("""{"op":"insert_bookmark","target":{"block":1},"name":"Mark"}""")]
    [InlineData("""{"op":"repeat_table_row","target":{"block":1},"items":[]}""")]
    public void ParserAndSchema_AcceptTheSameValidOperation(string operation)
    {
        string batch = $$"""{"ops":[{{operation}}]}""";

        _ = Parse(batch);

        Assert.True(OpsSchema<WordsModule>.Accepts(batch), "The schema rejected an operation the parser accepts.");
    }

    [Theory]
    [InlineData("""{"op":"insert_table","at":{"block":1},"position":"after","rowCount":1,"columnCount":2,"values":[["a"],["b"]]}""", "more rows")]
    [InlineData("""{"op":"set_table_pagination","target":{"block":1},"keepTogether":true,"allowRowBreakAcrossPages":true}""", "cannot be combined")]
    [InlineData("""{"op":"reject_revisions","author":"Ann","revisions":[1]}""", "cannot be combined")]
    [InlineData("""{"op":"accept_revisions","revisions":[0]}""", "revisions")]
    public void Parser_RejectsRulesTheRecordsStateInTheirSummaries(string operation, string reason)
    {
        CliException error = Assert.Throws<CliException>(() => Parse($$"""{"ops":[{{operation}}]}"""));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Contains(reason, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InsertSection_WithABlockAddressAsAt_IsRefusedWithWhatAtTakes()
    {
        CliException error = Assert.Throws<CliException>(() => Parse("""{"ops":[{"op":"insert_section","at":{"block":3},"position":"after"}]}"""));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Contains("'at' is the 1-based section number", error.Hint, StringComparison.Ordinal);
        Assert.Contains("position", error.Hint, StringComparison.Ordinal);
    }

    /// <summary>
    /// A highlight is a color of Word's palette, so a #RRGGBB highlight suggests the palette
    /// color closest to it, the same one for an exact match, and the hint names the field that
    /// takes any #RRGGBB.
    /// </summary>
    [Theory]
    [InlineData("#FFFF00", "yellow")]
    [InlineData("#FFC000", "yellow")]
    [InlineData("#000081", "dark-blue")]
    [InlineData("#c0c0c0", "light-gray")]
    public void FormatText_HexHighlight_SuggestsTheClosestPaletteColorAndNamesColor(string highlight, string closest)
    {
        CliException error = Assert.Throws<CliException>(() => Parse(
            $$"""{"ops":[{"op":"format_text","target":{"block":1},"highlight":"{{highlight}}"}]}"""));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Equal(closest, error.Details!["suggestions"]![0]!.GetValue<string>());
        Assert.Contains("'color'", error.Hint, StringComparison.Ordinal);
    }

    /// <summary>
    /// An import that names its file with source, as other tools do, is refused with path as the
    /// first suggestion and a hint that names it.
    /// </summary>
    [Theory]
    [InlineData("""{"op":"import_sections","source":"a.docx"}""")]
    public void AnImportThatNamesItsFileSource_SuggestsPath(string operation)
    {
        CliException error = Assert.Throws<CliException>(() => Parse($$"""{"ops":[{{operation}}]}"""));

        Assert.Equal("path", error.Details!["suggestions"]!.AsArray()[0]!.GetValue<string>());
        Assert.StartsWith("Did you mean 'path'", error.Hint, StringComparison.Ordinal);
    }

    private static WordsOpsBatch Parse(string json) =>
        WordsOp.Catalog.Parse<WordsOpsBatch>(json, Aspose.Cli.Generated.ProductJsonContext.Definition);
}
