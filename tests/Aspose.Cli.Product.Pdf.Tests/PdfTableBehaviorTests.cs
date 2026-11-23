using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Tables;
using Aspose.Pdf;
using Aspose.Pdf.Annotations;
using Aspose.Pdf.Forms;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>
/// What the PDF table does beyond one row: the file an edit saves is the one the engine alone saves
/// for the same change; a widget and its field are two objects; the encryption actions decide how the
/// output is saved and read back; a refused batch writes nothing; read lists every object the
/// inventory counts. The engine runs in this process, so the cases run licensed only.
/// </summary>
public sealed partial class PdfTableBehaviorTests
{
    [Fact]
    public void Edit_SavesWhatTheEngineAloneSavesForTheSameChange()
    {
        PdfEngineFixture.RequireLicense();
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateRich();
        string edited = fixture.File("edited.pdf");
        string alone = fixture.File("alone.pdf");

        PdfEdit.Run(fixture.Session, PdfEngineFixture.Edit(input, edited,
            """{"ops":[{"set":"document","props":{"info.title":"Changed"}},{"set":"field/name","props":{"value":"Bob"}},{"set":"page/5","props":{"rotate":"on180"}},{"set":"bookmark/2","props":{"title":"Bee"}},{"set":"attachment/2","props":{"mimeType":"text/plain"}},{"set":"pageLabel/2","props":{"prefix":"B-"}},{"set":"info/Custom","props":{"value":"changed"}},{"set":"page/2/annotation/6","props":{"contents":"moved"}}]}"""));
        using (var document = new Document(input))
        {
            document.Info.Title = "Changed";
            ((Field)document.Form["name"]).Value = "Bob";
            document.Pages[5].Rotate = Rotation.on180;
            document.Outlines[2].Title = "Bee";
            document.EmbeddedFiles[2].MIMEType = "text/plain";
            PageLabel label = document.PageLabels.GetLabel(2);
            label.Prefix = "B-";
            document.PageLabels.UpdateLabel(2, label);
            document.Info["Custom"] = "changed";
            document.Pages[2].Annotations[6].Contents = "moved";
            document.Save(alone);
        }

        Assert.Equal(WithoutTimeOfSave(alone), WithoutTimeOfSave(edited));
    }

    [Fact]
    public void AWidgetAndItsField_AreTwoObjects_AndASetOfOneLeavesWhatItDoesNotName()
    {
        PdfEngineFixture.RequireLicense();
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateRich();
        string output = fixture.File("out.pdf");

        PdfEditResult result = PdfEdit.Run(fixture.Session, PdfEngineFixture.Edit(input, output,
            """{"ops":[{"set":"page/1/annotation/1","props":{"rect":"40,690,240,710"}},{"set":"field/total","props":{"value":"7"}},{"set":"field/color","props":{"value":"green"}}]}"""));

        using var saved = new Document(output);
        Assert.Equal("Ann", ((Field)saved.Form["name"]).Value);
        Assert.Equal("7", ((Field)saved.Form["total"]).Value);
        Assert.Equal("Yes", ((Field)saved.Form["ok"]).Value);
        Assert.Equal("green", ((Field)saved.Form["color"]).Value);
        Assert.Equal("Rich", saved.Info.Title);
        // The radio buttons and the other page's widget keep their rectangles.
        Assert.Equal([50, 80], RadioButtons(saved).Select(static rect => (int)rect.LLX).Distinct().Order());
        Assert.Equal("300,700,450,720", Rect(saved.Pages[2].Annotations.Cast<Annotation>().First(static a => a.FullName == "total")));
        Assert.Equal("40,690,240,710", result.Ops[0].Now!["rect"]!.GetValue<string>());
    }

    [Fact]
    public void Encrypt_SavesAndReadsBackWithItsPassword_AndDecryptWithout()
    {
        PdfEngineFixture.RequireLicense();
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument();
        string encrypted = fixture.File("encrypted.pdf");
        string decrypted = fixture.File("decrypted.pdf");
        Environment.SetEnvironmentVariable("PDF_TABLE_USER", "user-pass");
        Environment.SetEnvironmentVariable("PDF_TABLE_OWNER", "owner-pass");
        try
        {
            PdfEditResult locked = PdfEdit.Run(fixture.Session, PdfEngineFixture.Edit(input, encrypted,
                """{"ops":[{"do":"encrypt","args":{"userPasswordEnv":"PDF_TABLE_USER","ownerPasswordEnv":"PDF_TABLE_OWNER","permissions":["PrintDocument"],"cryptoAlgorithm":"AESx256"}}]}"""));
            PdfEditResult unlocked = PdfEdit.Run(fixture.Session, PdfEngineFixture.Edit(encrypted, decrypted, """{"ops":[{"do":"decrypt"}]}""", password: new Secret("owner-pass")));

            Assert.True(locked.Output.Encrypted);
            Assert.True(locked.Ops[0].Now!["isEncrypted"]!.GetValue<bool>());
            Assert.Throws<InvalidPasswordException>(() => new Document(encrypted).Dispose());
            using (var opened = new Document(encrypted, "user-pass"))
            {
                Assert.True(opened.IsEncrypted);
            }

            Assert.False(unlocked.Output.Encrypted);
            Assert.False(unlocked.Ops[0].Now!["isEncrypted"]!.GetValue<bool>());
            using var plain = new Document(decrypted);
            Assert.False(plain.IsEncrypted);
            Assert.DoesNotContain("user-pass", Encoding.Latin1.GetString(File.ReadAllBytes(encrypted)), StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PDF_TABLE_USER", null);
            Environment.SetEnvironmentVariable("PDF_TABLE_OWNER", null);
        }
    }

    [Theory]
    [InlineData("""{"do":"decrypt"}""")]
    [InlineData("""{"do":"encrypt","args":{"ownerPasswordEnv":"PDF_TABLE_ONLY_OWNER","permissions":[],"cryptoAlgorithm":"AESx256"}}""")]
    public void AnEncryptionAction_WithEncrypt_IsRefusedAndWritesNothing(string action)
    {
        PdfEngineFixture.RequireLicense();
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument();
        string output = fixture.File("out.pdf");
        Environment.SetEnvironmentVariable("PDF_TABLE_ONLY_OWNER", "owner-pass");
        try
        {
            CliException refused = Assert.Throws<CliException>(() =>
                PdfEdit.Run(fixture.Session, PdfEngineFixture.Edit(input, output, $$"""{"ops":[{{action}}]}""", encrypt: new Secret("new-pass"))));

            Assert.Equal(ErrorCodes.OpsInvalid, refused.Code);
            Assert.False(File.Exists(output));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PDF_TABLE_ONLY_OWNER", null);
        }
    }

    [Theory]
    [InlineData("""{"set":"field/nope","props":{"value":"x"}}""", "ADDRESS_NOT_FOUND")]
    [InlineData("""{"delete":"page/9"}""", "ADDRESS_NOT_FOUND")]
    [InlineData("""{"set":"page/1/annotation/1","props":{"value":"x"}}""", "OPS_INVALID")]
    [InlineData("""{"set":"page/1/annotation/1","props":{"action.uri":"https://example.com/"}}""", "OPS_INVALID")]
    [InlineData("""{"set":"page/2/annotation/4","props":{"action.uri":"https://example.com/"}}""", "OPS_INVALID")]
    [InlineData("""{"add":"page","args":{"page":"field/name"}}""", "OPS_INVALID")]
    [InlineData("""{"do":"redact_area","args":{"page":"page/1","rect":"1,2,3"}}""", "OPS_INVALID")]
    [InlineData("""{"set":"attachment/9","props":{"mimeType":"text/plain"}}""", "ADDRESS_NOT_FOUND")]
    [InlineData("""{"set":"info/Custom","props":{"value":"x"}},{"delete":"bookmark/9"}""", "ADDRESS_NOT_FOUND")]
    public void ARefusedOperation_WritesNothing(string ops, string code)
    {
        PdfEngineFixture.RequireLicense();
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateRich();
        string output = fixture.File("out.pdf");

        CliException refused = Assert.Throws<CliException>(() => PdfEdit.Run(fixture.Session, PdfEngineFixture.Edit(input, output, $$"""{"ops":[{{ops}}]}""")));

        Assert.Equal(code, refused.Code.Name);
        Assert.False(File.Exists(output));
        Assert.Empty(Directory.GetFiles(fixture.Temp.Path, "*.aspose-publication-*", SearchOption.AllDirectories));
    }

    [Fact]
    public void Read_ListsEveryObjectTheInventoryCounts()
    {
        PdfEngineFixture.RequireLicense();
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateRich();
        string[] kinds = ["page", "annotation", "font", "layer", "bookmark", "field", "attachment", "pageLabel", "info", "xmp"];

        PdfReadResult read = PdfRead.Run(fixture.Session, new TableReadRequest { Input = input, Kinds = kinds, MaxObjects = TableRead.MaximumObjects });

        var listed = read.Objects.GroupBy(static item => item.Kind).ToDictionary(static group => group.Key, static group => (long)group.Count());
        foreach ((string key, string kind) in new[] { ("pages", "page"), ("annotations", "annotation"), ("fonts", "font"), ("layers", "layer"), ("bookmarks", "bookmark"), ("fields", "field"), ("attachments", "attachment"), ("pageLabels", "pageLabel"), ("info", "info"), ("xmp", "xmp") })
        {
            Assert.True(read.Inventory.Read[key] == listed.GetValueOrDefault(kind), $"{kind}: listed {listed.GetValueOrDefault(kind)}, counted {read.Inventory.Read[key]}");
        }

        Assert.Equal(read.Objects.Count, read.Objects.Select(static item => item.At).Distinct().Count());
        Assert.Contains(read.Objects, static item => item.At == "bookmark/1/bookmark/1/bookmark/1");
        Assert.Single(read.Objects, static item => item.At == "field/color");
    }

    [Fact]
    public void AKeyWithASlash_IsWrittenEscaped_AndFindsItsObject()
    {
        PdfEngineFixture.RequireLicense();
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument();
        string output = fixture.File("out.pdf");

        PdfEditResult added = PdfEdit.Run(fixture.Session, PdfEngineFixture.Edit(input, output,
            """{"ops":[{"add":"info","args":{"key":"dept/team\\x","value":"ops"}}]}"""));
        PdfReadResult read = PdfRead.Run(fixture.Session, new TableReadRequest { Input = output, At = """info/dept\/team\\x""" });

        Assert.Equal("""info/dept\/team\\x""", added.Ops[0].At);
        Assert.Equal("ops", Assert.Single(read.Objects).Props["value"]!.GetValue<string>());
    }

    [Fact]
    public void TheStructureTree_IsReadOnlyWhereItIsNamed()
    {
        PdfEngineFixture.RequireLicense();
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument();
        string output = fixture.File("out.pdf");

        PdfReadResult listed = PdfRead.Run(fixture.Session, new TableReadRequest { Input = input });
        PdfEditResult edited = PdfEdit.Run(fixture.Session, PdfEngineFixture.Edit(input, output,
            """{"ops":[{"set":"document","props":{"info.title":"One"}},{"set":"document","props":{"info.author":"Two"}}]}"""));
        PdfReadResult tree = PdfRead.Run(fixture.Session, new TableReadRequest { Input = input, At = "taggedContent" });

        Assert.DoesNotContain(listed.Objects, static item => item.Kind == "taggedContent");
        Assert.All(edited.Ops, static op => Assert.Equal("One", op.Now!["info.title"]!.GetValue<string>()));
        Assert.Equal(0, Assert.Single(tree.Objects).Props["rootElement.childElements.count"]!.GetValue<int>());
        using var saved = new Document(output);
        Assert.Equal("One", saved.Info.Title);
    }

    [Fact]
    public void SearchAndRedactArea_RemoveTheTextTheHitNames()
    {
        PdfEngineFixture.RequireLicense();
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateRawDocument("id.pdf", pages: 1, textContent: "BT /F1 12 Tf 72 720 Td (ID: 110101199003074518) Tj ET");
        string output = fixture.File("out.pdf");

        PdfSearchResult search = PdfSearch.Run(fixture.Session, new TableSearchRequest
        {
            Input = input,
            Query = new Aspose.Cli.Sdk.Text.SearchQuery(Aspose.Cli.Sdk.Text.TextSearch.Create("110101199003074518", regex: false, caseSensitive: true), 20),
        });
        TableHit hit = Assert.Single(search.Hits);
        PdfEditResult redacted = PdfEdit.Run(fixture.Session, PdfEngineFixture.Edit(input, output,
            $$$"""{"ops":[{"do":"redact_area","args":{"page":"page/1","rect":"{{{hit.Rect}}}"}}]}"""));

        Assert.Equal("page/1/text", hit.At);
        Assert.Equal("text", hit.Row);
        Assert.Equal($"page/1/area/{hit.Rect}", redacted.Ops[0].At);
        Assert.Equal(string.Empty, redacted.Ops[0].Now!["text"]!.GetValue<string>());
        using var saved = new Document(output);
        Assert.Contains("ID:", PdfTable.PageText(saved.Pages[1], null), StringComparison.Ordinal);
        Assert.DoesNotContain("1101011990", PdfTable.PageText(saved.Pages[1], null), StringComparison.Ordinal);
    }

    [Fact]
    public void AnEmptyPage_HasNoFont_AndIsListedAndCounted()
    {
        PdfEngineFixture.RequireLicense();
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument(pages: 1);
        string output = fixture.File("out.pdf");

        PdfEditResult added = PdfEdit.Run(fixture.Session, PdfEngineFixture.Edit(input, output, """{"ops":[{"add":"page"}]}"""));
        PdfReadResult fonts = PdfRead.Run(fixture.Session, new TableReadRequest { Input = output, At = "page/2", Kinds = ["font"] });

        Assert.Equal("page/2", added.Ops[0].At);
        Assert.Equal([1, 2], added.Counts["pages"]);
        Assert.Empty(fonts.Objects);
    }

    private static IEnumerable<Rectangle> RadioButtons(Document document) =>
        document.Pages[1].Annotations.Cast<Annotation>().Where(static a => a.FullName == "color" && a.Rect.Width > 0).Select(static a => a.Rect);

    private static string Rect(Annotation annotation) => annotation.Rect.ToString();

    /// <summary>A saved PDF's bytes without what changes with the time of the save: its dates and its file identifier.</summary>
    private static string WithoutTimeOfSave(string path) =>
        TimeOfSave().Replace(Encoding.Latin1.GetString(File.ReadAllBytes(path)), "#");

    [GeneratedRegex(@"D:\d{14}[^)>]*|/ID\s*\[[^\]]*\]|\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d[^<""]*")]
    private static partial Regex TimeOfSave();
}
