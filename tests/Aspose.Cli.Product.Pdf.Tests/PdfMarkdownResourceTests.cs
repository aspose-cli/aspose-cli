using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.TestKit;
using Aspose.Pdf;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>
/// The Aspose.PDF Markdown importer reads files with no resource hook, resolving the Markdown's
/// relative references against the working directory and a loaded file's against that file,
/// so the CLI proves before the import that every file it could read stays beneath the
/// Markdown file's directory.
/// </summary>
public sealed class PdfMarkdownResourceTests
{
    [Theory]
    [InlineData("![a](../outside.png)")]
    [InlineData("![a](%2e%2e/outside.png)")]
    [InlineData("![a](..%2Foutside.png)")]
    [InlineData("![a](&#46;&#46;/outside.png)")]
    [InlineData(@"![a](..\outside.png)")]
    [InlineData(@"![a](..\\outside.png)")]
    [InlineData("![a](<../outside.png> \"title\")")]
    [InlineData("![a](\n../outside.png\n)")]
    [InlineData("![a](sub/../../outside.png)")]
    [InlineData("![a][r]\n\n[r]: ../outside.png")]
    [InlineData("![R][]\n\n[r]:\n  <../outside.png>")]
    [InlineData("![shortcut]\n\n[SHORTCUT]: ../outside.png")]
    [InlineData("`![a](../outside.png)`")]
    [InlineData("<img src=\"../outside.png\">")]
    [InlineData("<IMG SRC='&#46;&#46;/outside.png'>")]
    [InlineData("<picture><source srcset=\"../outside.png 1x\"><img src=\"local.png\"></picture>")]
    [InlineData("<svg><image xlink:href=\"../outside.png\"/></svg>")]
    [InlineData("<object data=\"../outside.png\"></object>")]
    [InlineData("<link rel=\"stylesheet\" href=\"../outside.css\">")]
    [InlineData("<style>p { background: url('../outside.png'); }</style>")]
    [InlineData("<style>@import '../outside.css';</style>")]
    [InlineData("<p style=\"background: url(&quot;../outside.png&quot;)\">styled</p>")]
    [InlineData(@"<style>p { background: url('\2e\2e/outside.png'); }</style>")]
    [InlineData("![a]({outside})")]
    [InlineData("![a]({outsideSlash})")]
    [InlineData("![a]({outsideUri})")]
    [InlineData("![a](C:outside.png)")]
    [InlineData("![a](/outside.png)")]
    [InlineData(@"![a](\\?\C:\outside.png)")]
    [InlineData("![a](local.png:stream)")]
    [InlineData("![a](missing.png)")]
    [InlineData("![a](data:image/svg+xml,%3Csvg%3E%3C/svg%3E)")]
    public void Markdown_RefusesAReferenceOutsideTheDirectory(string reference)
    {
        using var fixture = new PdfEngineFixture();
        string directory = Directory.CreateDirectory(fixture.File("docs")).FullName;
        string outside = fixture.File("outside.png");
        File.WriteAllBytes(outside, ResourceHttpServer.Image);
        File.WriteAllText(fixture.File("outside.css"), "p { color: red; }");
        File.WriteAllBytes(Path.Combine(directory, "local.png"), ResourceHttpServer.Image);
        Directory.CreateDirectory(Path.Combine(directory, "sub"));
        string markdown = Path.Combine(directory, "input.md");
        File.WriteAllText(markdown, "# Report\n\n" + reference
            .Replace("{outsideSlash}", outside.Replace('\\', '/'), StringComparison.Ordinal)
            .Replace("{outsideUri}", new Uri(outside).AbsoluteUri, StringComparison.Ordinal)
            .Replace("{outside}", outside, StringComparison.Ordinal) + "\n");

        CliException refused = Assert.Throws<CliException>(() =>
            new MarkdownImportResources(markdown, ProductTestBudgets.Create<PdfModule>(), directory).Dispose());

        Assert.Equal(ErrorCodes.FeatureUnsupported, refused.Code);
    }

    [Theory]
    [InlineData("![a](local.png)")]
    [InlineData("![a](./sub/nested.png \"title\")")]
    [InlineData("![a](my%20image.png)")]
    [InlineData("![a](<my image.png>)")]
    [InlineData(@"![a](sub\nested.png)")]
    [InlineData("![a](local.png?v=1#frag)")]
    [InlineData("![a][logo]\n\n[Logo]: local.png")]
    [InlineData("<img src=\"local.png\"> <p style=\"background: url('sub/nested.png')\">x</p>")]
    [InlineData("<link rel=\"stylesheet\" href=\"sub/style.css\">")]
    [InlineData("![a](diagram.svg)")]
    [InlineData("![a](data:image/png;base64,iVBORw0KGgo=)")]
    [InlineData("![a](#fragment)")]
    [InlineData("[a hyperlink is never loaded](../outside.png) and [a definition only a link uses][d]\n\n[d]: ../outside.png")]
    public void Markdown_AcceptsReferencesBeneathTheDirectory(string reference)
    {
        Requires.Windows();
        using var fixture = new PdfEngineFixture();
        string directory = Directory.CreateDirectory(fixture.File("docs")).FullName;
        File.WriteAllBytes(fixture.File("outside.png"), ResourceHttpServer.Image);
        File.WriteAllBytes(Path.Combine(directory, "local.png"), ResourceHttpServer.Image);
        File.WriteAllBytes(Path.Combine(directory, "my image.png"), ResourceHttpServer.Image);
        string sub = Directory.CreateDirectory(Path.Combine(directory, "sub")).FullName;
        File.WriteAllBytes(Path.Combine(sub, "nested.png"), ResourceHttpServer.Image);
        // A stylesheet's references resolve against the stylesheet.
        File.WriteAllText(Path.Combine(sub, "style.css"), "p { background: url('nested.png'); } h1 { background: url(../local.png); }");
        File.WriteAllText(Path.Combine(directory, "diagram.svg"),
            """<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="9" height="9"><image xlink:href="sub/nested.png" width="9" height="9"/></svg>""");
        string markdown = Path.Combine(directory, "input.md");
        File.WriteAllText(markdown, "# Report\n\n" + reference + "\n");

        using var resources = new MarkdownImportResources(markdown, ProductTestBudgets.Create<PdfModule>(), directory);
    }

    // Files the importer loads are parsed for their own references, relative to themselves.
    [Theory]
    [InlineData("style.css", "p { background: url('../../outside.png'); }", "<link rel=\"stylesheet\" href=\"sub/style.css\">")]
    [InlineData("import.css", "@import url(\"../../outside.css\");", "<style>@import 'sub/import.css';</style>")]
    [InlineData("image.svg", "<svg xmlns=\"http://www.w3.org/2000/svg\"><image href=\"../../outside.png\"/></svg>", "![a](sub/image.svg)")]
    [InlineData("frame.svg", "<svg xmlns=\"http://www.w3.org/2000/svg\"><image href=\"../../outside.png\"/></svg>", "<object data=\"sub/frame.svg\"></object>")]
    [InlineData("remote.css", "p { background: url(http://127.0.0.1:9/remote.png); }", "<link rel=\"stylesheet\" href=\"sub/remote.css\">")]
    [InlineData("scripted.svg", "<svg xmlns=\"http://www.w3.org/2000/svg\"><script>fetch('x')</script></svg>", "![a](sub/scripted.svg)")]
    // A file loaded as a stylesheet is parsed as one whatever its first bytes are.
    [InlineData("polyglot.css", "GIF89a{} p { background: url('../../outside.png'); }", "<link rel=\"stylesheet\" href=\"sub/polyglot.css\">")]
    public void Markdown_RefusesALoadedFileThatReferencesOutside(string name, string content, string reference)
    {
        using var fixture = new PdfEngineFixture();
        string directory = Directory.CreateDirectory(fixture.File("docs")).FullName;
        string sub = Directory.CreateDirectory(Path.Combine(directory, "sub")).FullName;
        File.WriteAllBytes(fixture.File("outside.png"), ResourceHttpServer.Image);
        File.WriteAllText(fixture.File("outside.css"), "p { color: red; }");
        File.WriteAllText(Path.Combine(sub, name), content);
        string markdown = Path.Combine(directory, "input.md");
        File.WriteAllText(markdown, "# Report\n\n" + reference + "\n");

        CliException refused = Assert.Throws<CliException>(() =>
            new MarkdownImportResources(markdown, ProductTestBudgets.Create<PdfModule>(), directory).Dispose());

        Assert.Equal(ErrorCodes.FeatureUnsupported, refused.Code);
    }

    [Fact]
    public void Markdown_RefusesAJunctionThatLeavesTheDirectory()
    {
        Requires.Windows();
        using var fixture = new PdfEngineFixture();
        string directory = Directory.CreateDirectory(fixture.File("docs")).FullName;
        string elsewhere = Directory.CreateDirectory(fixture.File("elsewhere")).FullName;
        File.WriteAllBytes(Path.Combine(elsewhere, "secret.png"), ResourceHttpServer.Image);
        FileSystemLinks.CreateDirectoryLink(Path.Combine(directory, "linked"), elsewhere);
        string markdown = Path.Combine(directory, "input.md");
        File.WriteAllText(markdown, "# Report\n\n![a](linked/secret.png)\n");

        CliException refused = Assert.Throws<CliException>(() =>
            new MarkdownImportResources(markdown, ProductTestBudgets.Create<PdfModule>(), directory).Dispose());

        Assert.Equal(ErrorCodes.FeatureUnsupported, refused.Code);
        Assert.Contains("not an ordinary file", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Markdown_RefusesAFileSymbolicLinkThatLeavesTheDirectory()
    {
        Requires.Windows();
        using var fixture = new PdfEngineFixture();
        string directory = Directory.CreateDirectory(fixture.File("docs")).FullName;
        string secret = fixture.File("secret.png");
        File.WriteAllBytes(secret, ResourceHttpServer.Image);
        FileSystemLinks.CreateFileSymbolicLink(Path.Combine(directory, "image.png"), secret);
        string markdown = Path.Combine(directory, "input.md");
        File.WriteAllText(markdown, "# Report\n\n![a](image.png)\n");

        CliException refused = Assert.Throws<CliException>(() =>
            new MarkdownImportResources(markdown, ProductTestBudgets.Create<PdfModule>(), directory).Dispose());

        Assert.Equal(ErrorCodes.FeatureUnsupported, refused.Code);
    }

    // The importer resolves the Markdown's relative paths against the working directory, so
    // the same reference is outside the boundary when the command runs from elsewhere.
    [Fact]
    public void Markdown_ResolvesAgainstTheWorkingDirectoryAsTheImporterDoes()
    {
        using var fixture = new PdfEngineFixture();
        string directory = Directory.CreateDirectory(fixture.File("docs")).FullName;
        File.WriteAllBytes(Path.Combine(directory, "local.png"), ResourceHttpServer.Image);
        File.WriteAllBytes(fixture.File("local.png"), ResourceHttpServer.Image);
        string markdown = Path.Combine(directory, "input.md");
        File.WriteAllText(markdown, "# Report\n\n![a](local.png)\n");

        CliException refused = Assert.Throws<CliException>(() =>
            new MarkdownImportResources(markdown, ProductTestBudgets.Create<PdfModule>(), fixture.Temp.Path).Dispose());

        Assert.Equal(ErrorCodes.FeatureUnsupported, refused.Code);
        Assert.Contains("working directory", refused.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Markdown_ChargesAcceptedFilesToTheInputBudget()
    {
        Requires.Windows();
        using var fixture = new PdfEngineFixture();
        string directory = Directory.CreateDirectory(fixture.File("docs")).FullName;
        File.WriteAllBytes(Path.Combine(directory, "large.png"), [.. ResourceHttpServer.Image, .. new byte[4096]]);
        string markdown = Path.Combine(directory, "input.md");
        File.WriteAllText(markdown, "# Report\n\n![a](large.png)\n");
        var budgets = new ResourceBudgetLedger(
            Aspose.Cli.Sdk.Execution.OperationDeadline.Start(null),
            new Dictionary<string, long> { [ResourceBudgetKinds.InputBytes] = 2048 });

        CliException exceeded = Assert.Throws<CliException>(() =>
            new MarkdownImportResources(markdown, budgets, directory).Dispose());

        Assert.Equal(ErrorCodes.InputBudgetExceeded, exceeded.Code);
    }

    [Fact]
    public void MarkdownCreation_RefusesThroughTheCliAndKeepsImagesBeneathTheDirectory()
    {
        Requires.Windows();
        using var workspace = new TempWorkspace();
        File.WriteAllBytes(workspace.File("chart.png"), ResourceHttpServer.Image);
        string outside = Directory.CreateDirectory(workspace.File("..\\" + Path.GetFileName(workspace.Path) + "-outside")).FullName;
        try
        {
            File.WriteAllBytes(Path.Combine(outside, "secret.png"), ResourceHttpServer.Image);
            File.WriteAllText(workspace.File("report.md"), "# Report\n\n![chart](chart.png)\n");
            File.WriteAllText(workspace.File("escape.md"), $"# Report\n\n![secret](../{Path.GetFileName(outside)}/secret.png)\n");

            CliResult accepted = workspace.Run(["pdf", "create", "report.pdf", "--from-text", "report.md", "--output", "json"]);
            CliResult refused = workspace.Run(["pdf", "create", "escape.pdf", "--from-text", "escape.md", "--output", "json"]);

            Assert.True(accepted.ExitCode == 0, accepted.StdErr);
            using (var document = new Document(workspace.File("report.pdf")))
            {
                Assert.Contains(document.Pages[1].Resources.Images.Cast<XImage>(), image => image.Width == 1 && image.Height == 1);
            }

            Assert.Equal("FEATURE_UNSUPPORTED", JsonNode.Parse(refused.StdErr)!["error"]!["code"]!.GetValue<string>());
            Assert.False(File.Exists(workspace.File("escape.pdf")));
        }
        finally
        {
            Directory.Delete(outside, recursive: true);
        }
    }
}
