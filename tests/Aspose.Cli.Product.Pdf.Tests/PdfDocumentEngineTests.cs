using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Pdf;
using Aspose.Pdf.Annotations;
using Aspose.Pdf.Facades;
using Aspose.Pdf.Forms;
using Aspose.Pdf.Text;
using Xunit;
using CliPageRange = Aspose.Cli.Sdk.Addressing.PageRange;

namespace Aspose.Cli.Product.Pdf.Tests;

public sealed class PdfDocumentEngineTests
{
    [Fact]
    public void Info_ProjectsAllM1Details()
    {
        using var fixture = new PdfEngineFixture();
        string path = fixture.CreateDocument(pages: 2);
        using (var document = new Document(path))
        {
            document.Pages[2].SetPageSize(792, 612);
            document.PageLabels.UpdateLabel(0, new PageLabel
            {
                NumberingStyle = NumberingStyle.NumeralsRomanLowercase,
                Prefix = "A-",
                StartingValue = 1,
            });
            document.Outlines.Add(new OutlineItemCollection(document.Outlines)
            {
                Title = "First page",
                Destination = new FitExplicitDestination(document.Pages[1]),
            });
            document.Save(path);
        }

        var result = fixture.Engine.GetInfo(path, new PdfInfoRequest
        {
            IncludePreview = true,
            Details = ["outline", "forms", "attachments", "fonts", "permissions", "signatures", "layers", "metadata"],
        });

        Assert.Equal("pdf", result.Kind);
        Assert.Equal("pdf", result.Source.Format);
        Assert.Equal(2, result.Pdf.Pages);
        Assert.Equal("none", result.Pdf.FormType);
        Assert.Equal(2, result.Pdf.DistinctPageSizes.Count);
        Assert.Equal(2, result.Pages!.Count);
        Assert.Collection(
            result.PageLabels!,
            label =>
            {
                Assert.Equal(1, label.StartPage);
                Assert.Equal("roman-lower", label.NumberingStyle);
                Assert.Equal("A-", label.Prefix);
                Assert.Equal(1, label.StartingValue);
            });
        Assert.Collection(
            result.Outline!,
            item =>
            {
                Assert.Equal("First page", item.Title);
                Assert.Equal("page:1", item.Destination);
            });
        Assert.Equal("none", result.Forms!.Type);
        Assert.Empty(result.Attachments!);
        Assert.NotEmpty(result.Fonts!);
        Assert.True(result.Permissions!.OwnerAccess);
        Assert.Empty(result.Signatures!);
        Assert.Empty(result.Layers!);
        Assert.Contains("producer", result.Metadata!.Keys);
    }

    [Theory]
    [InlineData("plain")]
    [InlineData("layout")]
    public void Read_ProjectsSelectedPagesInBothModes(string mode)
    {
        using var fixture = new PdfEngineFixture();
        string path = fixture.CreateDocument(pages: 3);

        var result = fixture.Engine.Read(path, new PdfReadRequest
        {
            Pages = CliPageRange.Parse("2-3"),
            Mode = mode,
            MaxCharacters = 20_000,
        });

        Assert.Equal(mode, result.Mode);
        Assert.Equal("2,3", result.Window.Pages);
        Assert.Equal(3, result.Window.Of);
        Assert.False(result.Window.Truncated);
        Assert.Contains("Portable PDF page 2", result.Pages[0].Text, StringComparison.Ordinal);
        Assert.Contains("Portable PDF page 3", result.Pages[1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_StopsAtCharacterBudgetAndReturnsDeterministicNextCommand()
    {
        using var fixture = new PdfEngineFixture();
        string path = fixture.CreateDocument(pages: 3);

        var result = fixture.Engine.Read(path, new PdfReadRequest { MaxCharacters = 24 });

        Assert.True(result.Window.Truncated);
        Assert.Equal(24, result.Pages.Sum(static page => page.Text.Length));
        Assert.NotNull(result.Next);
        Assert.Contains("--pages ", result.Next, StringComparison.Ordinal);
    }

    [Fact]
    public void Passwords_DistinguishRequiredInvalidUserAndOwnerAccess()
    {
        using var fixture = new PdfEngineFixture();
        string path = fixture.File("protected.pdf");
        using (var document = new Document())
        {
            Page page = document.Pages.Add();
            page.Paragraphs.Add(new TextFragment("Protected"));
            document.Encrypt(
                "reader-secret",
                "owner-secret",
                DocumentPrivilege.ForbidAll,
                CryptoAlgorithm.AESx256,
                usePdf20: false);
            document.Save(path);
        }

        CliException required = Assert.Throws<CliException>(
            () => fixture.Engine.GetInfo(path, new PdfInfoRequest()));
        Assert.Equal(ErrorCodes.PasswordRequired, required.Code);
        CliException invalid = Assert.Throws<CliException>(
            () => fixture.Engine.GetInfo(path, new PdfInfoRequest { Password = "wrong" }));
        Assert.Equal(ErrorCodes.PasswordInvalid, invalid.Code);

        var user = fixture.Engine.GetInfo(path, new PdfInfoRequest
        {
            Password = "reader-secret",
            Details = ["permissions"],
        });
        Assert.Equal("user", user.Pdf.PasswordType);
        Assert.False(user.Permissions!.OwnerAccess);
        Assert.False(user.Permissions.Copy);

        var owner = fixture.Engine.GetInfo(path, new PdfInfoRequest
        {
            Password = "owner-secret",
            Details = ["permissions"],
        });
        Assert.Equal("owner", owner.Pdf.PasswordType);
        Assert.True(owner.Permissions!.OwnerAccess);
        Assert.True(owner.Permissions.Copy);
    }

    [Fact]
    public void Passwords_OwnerOnlyFixtureOpensNormallyAndRecognizesOwnerAccess()
    {
        using var fixture = new PdfEngineFixture();
        string path = fixture.CreateEncryptedDocument(
            string.Empty,
            "owner-secret",
            "owner-password-only.pdf");

        var normal = fixture.Engine.GetInfo(
            path,
            new PdfInfoRequest { Details = ["permissions"] });
        var owner = fixture.Engine.GetInfo(
            path,
            new PdfInfoRequest { Password = "owner-secret", Details = ["permissions"] });

        Assert.True(normal.Pdf.Encrypted);
        Assert.Equal("user", normal.Pdf.PasswordType);
        Assert.False(normal.Permissions!.OwnerAccess);
        Assert.Equal("owner", owner.Pdf.PasswordType);
        Assert.True(owner.Permissions!.OwnerAccess);
    }

    [Fact]
    public void Load_RejectsRenamedNonPdfBytes()
    {
        using var fixture = new PdfEngineFixture();
        string path = fixture.File("renamed.pdf");
        File.WriteAllText(path, "not really a PDF");

        CliException exception = Assert.Throws<CliException>(
            () => fixture.Engine.GetInfo(path, new PdfInfoRequest()));

        Assert.Equal(ErrorCodes.FileCorrupt, exception.Code);
    }

    [Fact]
    public void Read_OneHundredPageFixtureRemainsWindowed()
    {
        using var fixture = new PdfEngineFixture();
        string path = fixture.CreateRawDocument("hundred-pages.pdf", pages: 100);

        var result = fixture.Engine.Read(path, new PdfReadRequest { MaxCharacters = 1 });

        Assert.Equal(100, result.Window.Of);
        Assert.Single(result.Pages);
        Assert.True(result.Window.Truncated);
        Assert.Contains("--pages 2,3,4", result.Next, StringComparison.Ordinal);
    }

    [Fact]
    public void Info_XfaIsExplicitlyFlaggedReadOnly()
    {
        using var fixture = new PdfEngineFixture();
        string path = fixture.File("xfa.pdf");
        using (var document = new Document())
        {
            Page page = document.Pages.Add();
            document.Form.Add(new TextBoxField(page, new Rectangle(72, 700, 200, 730))
            {
                PartialName = "seed",
            });
            var xfa = new System.Xml.XmlDocument();
            xfa.LoadXml(
                """
                <xdp:xdp xmlns:xdp="http://ns.adobe.com/xdp/">
                  <template xmlns="http://www.xfa.org/schema/xfa-template/3.3/">
                    <subform name="form1"/>
                  </template>
                  <datasets xmlns="http://www.xfa.org/schema/xfa-data/1.0/">
                    <xfa:data xmlns:xfa="http://www.xfa.org/schema/xfa-data/1.0/"/>
                  </datasets>
                </xdp:xdp>
                """);
            document.Form.AssignXfa(xfa);
            document.Save(path);
        }

        var result = fixture.Engine.GetInfo(path, new PdfInfoRequest { Details = ["forms"] });

        Assert.Equal("xfa", result.Pdf.FormType);
        Assert.Equal("xfa", result.Forms!.Type);
        Assert.True(result.Forms.ReadOnly);
    }

    [Fact]
    public void Convert_HtmlIsOneAtomicSelfContainedArtifact()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("html-source.pdf", 1);
        string output = fixture.File("self-contained.html");

        var result = fixture.Engine.Convert(input, new PdfConvertRequest
        {
            TargetFormatId = "html",
            OutputPath = output,
        });

        Assert.Single(result.Outputs);
        Assert.True(File.Exists(output));
        Assert.Equal(
            ["html-source.pdf", "self-contained.html"],
            Directory.GetFiles(fixture.Temp.Path).Select(Path.GetFileName).Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("docx")]
    [InlineData("xlsx")]
    [InlineData("pptx")]
    [InlineData("epub")]
    [InlineData("txt")]
    [InlineData("md")]
    [InlineData("svg")]
    [InlineData("xps")]
    [InlineData("pdfa-1b")]
    [InlineData("pdfa-2b")]
    [InlineData("pdfa-3b")]
    [InlineData("png")]
    [InlineData("jpeg")]
    [InlineData("tiff")]
    public void Convert_ProducesEveryFrozenM1Target(string format)
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument(pages: 1);
        string output = fixture.File("output" + PdfFormats.Extension(format));

        var result = fixture.Engine.Convert(input, new PdfConvertRequest
        {
            TargetFormatId = format,
            OutputPath = output,
        });

        Assert.NotEmpty(result.Outputs);
        Assert.All(result.Outputs, item =>
        {
            Assert.True(File.Exists(item.Path), item.Path);
            Assert.True(item.SizeBytes > 0);
        });
        if (fixture.LicenseState != Sdk.Licensing.LicenseState.Licensed)
        {
            Assert.Contains(result.Warnings!, static warning => warning.Code == WarningCodes.EvalMode);
        }
    }
}
