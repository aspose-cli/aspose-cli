using Aspose.Pdf;
using Aspose.Pdf.Annotations;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>
/// Each action of the PDF table makes its one engine call: run on the rich document, the saved file
/// holds what the engine's call leaves, read back with the engine itself. The engine runs in this
/// process, so the cases run licensed only.
/// </summary>
public sealed class PdfTableActionTests
{
    public static TheoryData<string> Actions() =>
    [
        "stamp_text", "stamp_image", "redact_area", "flatten_form", "flatten_field", "resize_page", "import_pages",
        "clear_info", "clear_xmp", "optimize", "encrypt", "decrypt",
    ];

    [Theory]
    [MemberData(nameof(Actions))]
    public void Action_LeavesWhatItsEngineCallLeaves(string action)
    {
        PdfEngineFixture.RequireLicense();
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateRich();
        string other = fixture.CreateDocument("other.pdf", pages: 3).Replace("\\", "\\\\", StringComparison.Ordinal);
        string png = fixture.File("mark.png");
        File.WriteAllBytes(png, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=="));
        string image = png.Replace("\\", "\\\\", StringComparison.Ordinal);
        Environment.SetEnvironmentVariable("PDF_ACTION_OWNER", "owner-pass");
        (string ops, Func<Document, bool> holds) = action switch
        {
            "stamp_text" => Case("""{"do":"stamp_text","args":{"page":"page/1","value":"- 1 -","horizontalAlignment":"Center","verticalAlignment":"Bottom"}}""",
                static d => PdfTable.PageText(d.Pages[1], new Rectangle(250, 0, 350, 20)).Contains("- 1 -", StringComparison.Ordinal)),
            "stamp_image" => Case($$$"""{"do":"stamp_image","args":{"page":"page/2","image":"{{{image}}}","xIndent":100,"yIndent":100,"width":20,"height":20}}""",
                static d => d.Pages[2].Resources.Images.Count == 1),
            "redact_area" => Case("""{"do":"redact_area","args":{"page":"page/1","rect":"0,0,595,842"}}""",
                static d => !PdfTable.PageText(d.Pages[1], null).Contains("PAGE-1", StringComparison.Ordinal)),
            "flatten_form" => Case("""{"do":"flatten_form"}""", static d => d.Form.Count == 0),
            "flatten_field" => Case("""{"do":"flatten_field","args":{"field":"field/name"}}""", static d => !d.Form.HasField("name") && d.Form.HasField("total")),
            "resize_page" => Case("""{"do":"resize_page","args":{"page":"page/1","width":612,"height":792}}""", static d => d.Pages[1].MediaBox.Width == 612),
            "import_pages" => Case($$$"""{"do":"import_pages","args":{"file":"{{{other}}}","pages":[3,1],"pageNumber":2}}""",
                static d => d.Pages.Count == 8 && PdfTable.PageText(d.Pages[2], null).Contains("page 3", StringComparison.Ordinal)),
            "clear_info" => Case("""{"do":"clear_info"}""", static d => string.IsNullOrEmpty(d.Info.Author) && !d.Info.ContainsKey("Custom")),
            "clear_xmp" => Case("""{"do":"clear_xmp"}""", static d => !d.Metadata.ContainsKey("xmp:CreatorTool")),
            "optimize" => Case("""{"do":"optimize","args":{"removeUnusedObjects":true,"linkDuplicateStreams":true}}""", static d => d.Pages.Count == 6),
            "encrypt" => Case("""{"do":"encrypt","args":{"ownerPasswordEnv":"PDF_ACTION_OWNER","permissions":["PrintDocument"],"cryptoAlgorithm":"AESx128"}}""",
                static d => d.IsEncrypted),
            _ => Case("""{"do":"encrypt","args":{"ownerPasswordEnv":"PDF_ACTION_OWNER","permissions":[],"cryptoAlgorithm":"AESx256"}},{"do":"decrypt"}""",
                static d => !d.IsEncrypted),
        };
        string output = fixture.File("out.pdf");

        try
        {
            PdfEditResult result = PdfEdit.Run(fixture.Session, PdfEngineFixture.Edit(input, output, $$"""{"ops":[{{ops}}]}"""));

            Assert.Equal(action, result.Ops[^1].Do);
            using var saved = new Document(output);
            Assert.True(holds(saved), $"{action} did not leave what its engine call leaves");
        }
        finally
        {
            Environment.SetEnvironmentVariable("PDF_ACTION_OWNER", null);
        }
    }

    [Fact]
    public void StampText_StatesTheMembersItWasGiven_AndLeavesTheEnginesDefaultsForTheOthers()
    {
        PdfEngineFixture.RequireLicense();
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument(pages: 1);
        string output = fixture.File("out.pdf");

        PdfEdit.Run(fixture.Session, PdfEngineFixture.Edit(input, output,
            """{"ops":[{"do":"stamp_text","args":{"page":"page/1","value":"DRAFT","xIndent":100,"yIndent":200,"textState.fontSize":30}}]}"""));

        using var saved = new Document(output);
        var absorber = new Aspose.Pdf.Text.TextFragmentAbsorber("DRAFT");
        saved.Pages[1].Accept(absorber);
        Aspose.Pdf.Text.TextFragment fragment = Assert.Single(absorber.TextFragments.Cast<Aspose.Pdf.Text.TextFragment>());
        Assert.Equal(100, fragment.Rectangle.LLX, 0);
        Assert.Equal(30, fragment.TextState.FontSize, 0);
        Assert.Equal("Helvetica", fragment.TextState.Font.FontName);
    }

    [Fact]
    public void AddPage_WithAPage_CopiesThatPageWhere_PageNumberSays()
    {
        PdfEngineFixture.RequireLicense();
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument(pages: 3);
        string output = fixture.File("out.pdf");

        PdfEditResult result = PdfEdit.Run(fixture.Session, PdfEngineFixture.Edit(input, output,
            """{"ops":[{"add":"page","in":"document","args":{"pageNumber":1,"page":"page/3"}},{"delete":"page/4"}]}"""));

        using var saved = new Document(output);
        Assert.Equal(["page 3", "page 1", "page 2"], saved.Pages.Cast<Page>().Select(static page => PdfTable.PageText(page, null).Trim()[^6..]));
        Assert.Equal("page/1", result.Ops[0].At);
        Assert.False(result.Ops[1].Exists);
    }

    [Fact]
    public void AddLinkBookmarkAttachmentLabelAndInfo_ReadBackWhatTheEngineSaved()
    {
        PdfEngineFixture.RequireLicense();
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument(pages: 2);
        string file = fixture.File("note.txt");
        File.WriteAllText(file, "note");
        string output = fixture.File("out.pdf");
        string escaped = file.Replace("\\", "\\\\", StringComparison.Ordinal);

        PdfEditResult result = PdfEdit.Run(fixture.Session, PdfEngineFixture.Edit(input, output, $$$"""
            {"ops":[
              {"add":"link","in":"page/2","args":{"rect":"10,10,100,30","uri":"https://example.com/"}},
              {"add":"bookmark","in":"document","args":{"title":"Two","page":"page/2"}},
              {"add":"bookmark","in":"bookmark/1","args":{"title":"Two.a"}},
              {"add":"attachment","in":"document","args":{"file":"{{{escaped}}}","name":"note.txt","description":"d"}},
              {"add":"pageLabel","in":"document","args":{"pageIndex":1},"props":{"prefix":"B-"}},
              {"add":"info","in":"document","args":{"key":"Department","value":"Ops"}}
            ]}
            """));

        Assert.Equal(["page/2/annotation/1", "bookmark/1", "bookmark/1/bookmark/1", "attachment/1", "pageLabel/1", "info/Department"], result.Ops.Select(static op => op.At));
        Assert.All(result.Ops, static op => Assert.True(op.Exists));
        using var saved = new Document(output);
        Assert.Equal("https://example.com/", ((GoToURIAction)((LinkAnnotation)saved.Pages[2].Annotations[1]).Action).URI);
        Assert.Equal(2, ((ExplicitDestination)((GoToAction)saved.Outlines[1].Destination).Destination).PageNumber);
        Assert.Equal("Two.a", saved.Outlines[1][1].Title);
        Assert.Equal("note.txt", saved.EmbeddedFiles[1].Name);
        Assert.Equal("B-", saved.PageLabels.GetLabel(1).Prefix);
        Assert.Equal("Ops", saved.Info["Department"]);
    }

    private static (string, Func<Document, bool>) Case(string ops, Func<Document, bool> holds) => (ops, holds);
}
