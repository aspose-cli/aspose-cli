using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Errors;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>Signing verifies what it added; PDF/A results state what the SDK found.</summary>
public sealed class PdfConformanceTests
{
    [Fact]
    public void Sign_VerifiesTheSignatureItAddedRatherThanAnEarlierOne()
    {
        using var fixture = new PdfEngineFixture();
        const string password = "test-certificate-password";
        string certificate = fixture.CreateCertificate(password);
        string input = fixture.CreateDocument("twice.pdf", pages: 1);
        PdfSignResult first = Sign(fixture, input, certificate, password, "first.pdf");

        PdfSignResult second = Sign(fixture, first.Output.Path, certificate, password, "second.pdf");

        Assert.True(second.Signature.Signed);
        Assert.NotEqual(first.Signature.Name, second.Signature.Name);
        PdfInfoResult info = fixture.Engine.GetInfo(second.Output.Path, new PdfInfoRequest { Details = ["signatures"] });
        Assert.Contains(info.Signatures!, signature => signature.Name == second.Signature.Name && signature.Signed);
    }

    [Fact]
    public void Validate_ReportsClauseSeverityAndMessageForEachProblem()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("plain.pdf", pages: 1);

        PdfValidateResult result = fixture.Engine.Validate(input, new PdfValidateRequest { Profile = "pdfa-1b" });

        Assert.False(result.Valid);
        Assert.NotEmpty(result.Issues);
        Assert.All(result.Issues, static issue =>
        {
            Assert.DoesNotContain("<", issue, StringComparison.Ordinal);
            Assert.Matches(new Regex(@"^[\d.]+ \((error|warning)(, page \d+)?\): \S"), issue);
        });
    }

    [Fact]
    public void AnUnsuccessfulPdfaConversion_IsRejectedWithTheProblemsTheSdkCouldNotFix()
    {
        // The SDK cannot convert an encrypted document in place; its own result and log
        // drive the decision. (The convert command decrypts the document before converting it.)
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateEncryptedDocument("user-secret", "owner-secret", "encrypted.pdf");
        using var document = new Aspose.Pdf.Document(input, "owner-secret");
        using var log = new MemoryStream();
        bool converted = document.Convert(log, Aspose.Pdf.PdfFormat.PDF_A_1B, Aspose.Pdf.ConvertErrorAction.Delete);

        CliException error = Assert.Throws<CliException>(() => PdfComplianceLog.EnsureConverted(converted, log, "pdfa-1b"));

        Assert.Equal("PDFA_CONVERSION_FAILED", error.Code.Name);
        Assert.Contains("1 problem(s) cannot be fixed automatically", error.Message, StringComparison.Ordinal);
        Assert.Single(error.Details!["problems"]!.AsArray());
    }

    [Fact]
    public void ASuccessfulPdfaConversion_IsPublished()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("convertible.pdf", pages: 1);

        PdfConvertResult result = fixture.Engine.Convert(input, new PdfConvertRequest
        {
            Output = TestOutput.At(fixture.File("convertible.pdfa.pdf"), format: "pdfa-2b"),
        });

        Assert.True(File.Exists(Assert.Single(result.Outputs).Path));
    }

    [Fact]
    public void APdfaConversionWhoseOutputDoesNotValidate_NamesTheRadioGroupsAndIsNotPublished()
    {
        using var fixture = new PdfEngineFixture();
        string html = fixture.File("form.html");
        File.WriteAllText(html, """
            <html><body><form>
            <input type="text" name="company"/>
            <input type="radio" name="kind" value="maker"/> Maker <input type="radio" name="kind" value="seller"/> Seller
            <input type="checkbox" name="urgent"/> Urgent
            </form></body></html>
            """);
        string form = fixture.Engine.Create(new NewPdfRequest { Output = TestOutput.At(fixture.File("form.pdf")), HtmlPath = html }).Output.Path;
        string output = fixture.File("form.pdfa.pdf");

        CliException error = Assert.Throws<CliException>(() => fixture.Engine.Convert(form, new PdfConvertRequest
        {
            Output = TestOutput.At(output, format: "pdfa-2b"),
        }));

        Assert.Equal("PDFA_CONVERSION_FAILED", error.Code.Name);
        Assert.Contains("6.3.3", Assert.Single(error.Details!["problems"]!.AsArray())!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Contains("'radio'", error.Hint, StringComparison.Ordinal);
        // Text fields and check boxes, whose appearances have a state each, conform as they are.
        Assert.DoesNotContain("'company'", error.Hint, StringComparison.Ordinal);
        Assert.DoesNotContain("'field_", error.Hint, StringComparison.Ordinal);
        Assert.Contains("flatten_forms", error.Hint, StringComparison.Ordinal);
        Assert.False(File.Exists(output));

        // The remedy the hint names, flattening only the named fields, makes the conversion conform.
        string flattened = fixture.File("form.flat.pdf");
        fixture.Engine.ApplyOps(form, new PdfOpsBatch { Ops = [new FlattenFormsOp { Fields = ["radio"] }] }, new PdfEditRequest { Output = TestOutput.At(flattened) });
        fixture.Engine.Convert(flattened, new PdfConvertRequest { Output = TestOutput.At(output, format: "pdfa-2b") });
        Assert.True(fixture.Engine.Validate(output, new PdfValidateRequest { Profile = "pdfa-2b" }).Valid);
    }

    [Theory]
    [InlineData("pdfa-1b")]
    [InlineData("pdfa-2b")]
    [InlineData("pdfa-3b")]
    public void PdfaConversion_KeepsTheDocumentPartsTheProfileAllows(string profile)
    {
        using var fixture = new PdfEngineFixture();
        string input = CreateArchivable(fixture);

        PdfConvertResult result = fixture.Engine.Convert(input, new PdfConvertRequest
        {
            Output = TestOutput.At(fixture.File("archive.pdf"), format: profile),
        });

        string output = Assert.Single(result.Outputs).Path;
        Assert.True(fixture.Engine.Validate(output, new PdfValidateRequest { Profile = profile }).Valid);
        using var archived = new Aspose.Pdf.Document(output);
        Assert.Equal(["Results", "Detail", "Appendix"], OutlineTitles(archived.Outlines));
        Assert.Equal("Quarterly results", archived.Info.Title);
        Assert.Equal("A-", archived.PageLabels.GetLabel(0).Prefix);
        Assert.DoesNotContain(result.Warnings ?? [], static warning => warning.Code == "NAVIGATION_DEGRADED");

        // PDF/A-1 allows no attachment, PDF/A-2 only PDF/A ones, PDF/A-3 any.
        string[] kept = profile switch
        {
            "pdfa-1b" => [],
            "pdfa-2b" => ["appendix.pdf"],
            _ => ["appendix.pdf", "figures.csv", "notes.bin"],
        };
        Assert.Equal(kept, archived.EmbeddedFiles.Cast<Aspose.Pdf.FileSpecification>().Select(static file => file.Name).Order(StringComparer.Ordinal));
        string[] removed = ["appendix.pdf", "figures.csv", "notes.bin"];
        foreach (string name in removed.Except(kept))
        {
            Warning warning = Assert.Single(result.Warnings!, warning => warning.Location == $"attachment {name}");
            Assert.Equal(WarningCodes.LossyConversion, warning.Code);
            Assert.Contains(profile == "pdfa-1b" ? "PDF/A-1 does not allow attachments" : "PDF/A-2 allows only attachments that are PDF/A documents", warning.Message, StringComparison.Ordinal);
            Assert.Contains("pdfa-3b", warning.Hint, StringComparison.Ordinal);
        }

        Assert.DoesNotContain(result.Warnings ?? [], warning => kept.Any(name => warning.Location == $"attachment {name}"));

        // The engine labels every attachment application/pdf (PDF-PDFA-ATTACHMENT-TYPE).
        Dictionary<string, string> types = archived.EmbeddedFiles.Cast<Aspose.Pdf.FileSpecification>()
            .ToDictionary(static file => file.Name, static file => file.MIMEType, StringComparer.Ordinal);
        if (profile == "pdfa-3b")
        {
            Assert.Equal("text/csv", types["figures.csv"]);
            Assert.Equal("application/octet-stream", types["notes.bin"]);
            Assert.Equal("application/pdf", types["appendix.pdf"]);
        }
    }

    /// <summary>The media type add_attachment declares is stored and kept by PDF/A-3.</summary>
    [Fact]
    public void AddAttachment_DeclaresAMediaTypeThatPdfa3Keeps()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("plain.pdf", pages: 1);
        string scan = fixture.File("license-scan.png");
        File.WriteAllBytes(scan, [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]);
        string attached = fixture.File("attached.pdf");
        fixture.Engine.ApplyOps(input, new PdfOpsBatch
        {
            Ops =
            [
                new AddAttachmentOp { Path = scan, MimeType = "image/png" },
                new AddAttachmentOp { Path = scan, Name = "untyped.png" },
            ],
        }, new PdfEditRequest { Output = TestOutput.At(attached) });

        Assert.Equal(
            [("license-scan.png", "image/png"), ("untyped.png", null)],
            MediaTypes(attached));

        string archive = fixture.File("archive.pdf");
        fixture.Engine.Convert(attached, new PdfConvertRequest { Output = TestOutput.At(archive, format: "pdfa-3b") });
        Assert.Equal(
            [("license-scan.png", "image/png"), ("untyped.png", "application/octet-stream")],
            MediaTypes(archive));

        (string, string?)[] MediaTypes(string path) => [.. fixture.Engine
            .GetInfo(path, new PdfInfoRequest { Details = ["attachments"] }).Attachments!
            .Select(static item => (item.Name, item.MimeType))];
    }

    [Fact]
    public void PdfaConversionLog_GroupsTheIdentificationAndTheAttachments()
    {
        using var fixture = new PdfEngineFixture();
        using var document = new Aspose.Pdf.Document(CreateArchivable(fixture));
        using var log = new MemoryStream();

        Assert.True(document.Convert(log, Aspose.Pdf.PdfFormat.PDF_A_2B, Aspose.Pdf.ConvertErrorAction.Delete));

        // ConvertPdfa leaves these two sections out of its count of other changes.
        IReadOnlyList<PdfComplianceProblem> problems = PdfComplianceLog.Parse(log);
        Assert.Contains(problems, static problem => problem.Section == "Metadata" && problem.Message.Contains("pdfaid", StringComparison.Ordinal));
        Assert.Contains(problems, static problem => problem.Section == "EmbeddedFiles" && problem.Message.Contains("figures.csv", StringComparison.Ordinal));
    }

    [Fact]
    public void PdfaConversion_OfEveryPage_DisclosesNoNavigationLoss()
    {
        using var fixture = new PdfEngineFixture();
        string input = CreateArchivable(fixture);

        PdfConvertResult result = fixture.Engine.Convert(input, new PdfConvertRequest
        {
            Output = TestOutput.At(fixture.File("every-page.pdf"), format: "pdfa-2b"),
            Pages = Sdk.Addressing.PageRange.Parse("1-2"),
        });

        Assert.DoesNotContain(result.Warnings ?? [], static warning => warning.Code == "NAVIGATION_DEGRADED");
    }

    [Fact]
    public void PdfaConversion_OfSelectedPages_CountsTheBookmarksThatLostTheirPage()
    {
        using var fixture = new PdfEngineFixture();
        string input = CreateArchivable(fixture);

        PdfConvertResult result = fixture.Engine.Convert(input, new PdfConvertRequest
        {
            Output = TestOutput.At(fixture.File("page-two.pdf"), format: "pdfa-2b"),
            Pages = Sdk.Addressing.PageRange.Parse("2"),
        });

        string output = Assert.Single(result.Outputs).Path;
        Assert.True(fixture.Engine.Validate(output, new PdfValidateRequest { Profile = "pdfa-2b" }).Valid);
        using var archived = new Aspose.Pdf.Document(output);
        Assert.Single(archived.Pages);
        Assert.Contains("Appendix", OutlineTitles(archived.Outlines));
        Assert.Contains(result.Warnings!, static warning => warning.Code == "NAVIGATION_DEGRADED");
    }

    [Fact]
    public void PdfaConversion_OfAnEncryptedDocument_WritesAnUnencryptedArchive()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateEncryptedDocument("user-secret", "owner-secret", "encrypted.pdf");

        PdfConvertResult result = fixture.Engine.Convert(input, new PdfConvertRequest
        {
            Output = TestOutput.At(fixture.File("encrypted.pdfa.pdf"), format: "pdfa-2b"),
            Password = "user-secret",
        });

        string output = Assert.Single(result.Outputs).Path;
        Assert.True(fixture.Engine.Validate(output, new PdfValidateRequest { Profile = "pdfa-2b" }).Valid);
    }

    [Fact]
    public void Inspect_OfAnArchive_ReportsTheFileAsItIs()
    {
        using var fixture = new PdfEngineFixture();
        string output = Assert.Single(fixture.Engine.Convert(CreateArchivable(fixture), new PdfConvertRequest
        {
            Output = TestOutput.At(fixture.File("archive.pdf"), format: "pdfa-2b"),
        }).Outputs).Path;
        var request = new PdfInfoRequest { Details = ["metadata"] };

        PdfInfoResult first = fixture.Engine.GetInfo(output, request);
        PdfInfoResult second = fixture.Engine.GetInfo(output, request);

        Assert.Equal("pdfa-2b", first.Pdf.PdfaProfile);
        Assert.False(first.Pdf.Tagged);
        Assert.Equal("2", first.Metadata!["xmp:pdfaid:part"]);
        Assert.Equal("B", first.Metadata["xmp:pdfaid:conformance"]);
        Assert.DoesNotContain("xmp:pdfuaid:part", first.Metadata.Keys);
        Assert.Equal("Quarterly results", first.Metadata["title"]);
        Assert.Equal(first.Metadata, second.Metadata);
    }

    [Fact]
    public void Inspect_OfADocumentWithoutXmp_ReportsNoXmpProperties()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("plain.pdf", pages: 1);

        PdfInfoResult result = fixture.Engine.GetInfo(input, new PdfInfoRequest { Details = ["metadata"] });

        Assert.DoesNotContain(result.Metadata!.Keys, static key => key.StartsWith("xmp:", StringComparison.Ordinal));
        Assert.NotEqual("Tagged PDF", result.Metadata["title"]);
        Assert.Null(result.Pdf.PdfaProfile);
    }

    /// <summary>
    /// Two pages, three bookmarks (one nested), a page label, a title and three attachments:
    /// a typed CSV, an untyped binary and an untyped PDF/A document.
    /// </summary>
    private static string CreateArchivable(PdfEngineFixture fixture)
    {
        string path = fixture.File("archivable.pdf");
        using var document = new Aspose.Pdf.Document();
        Aspose.Pdf.Page first = document.Pages.Add();
        first.Paragraphs.Add(new Aspose.Pdf.Text.TextFragment("Results"));
        Aspose.Pdf.Page second = document.Pages.Add();
        second.Paragraphs.Add(new Aspose.Pdf.Text.TextFragment("Appendix"));
        var results = new Aspose.Pdf.OutlineItemCollection(document.Outlines)
        {
            Title = "Results",
            Destination = new Aspose.Pdf.Annotations.FitExplicitDestination(first),
        };
        results.Add(new Aspose.Pdf.OutlineItemCollection(document.Outlines)
        {
            Title = "Detail",
            Destination = new Aspose.Pdf.Annotations.FitExplicitDestination(first),
        });
        document.Outlines.Add(results);
        document.Outlines.Add(new Aspose.Pdf.OutlineItemCollection(document.Outlines)
        {
            Title = "Appendix",
            Destination = new Aspose.Pdf.Annotations.FitExplicitDestination(second),
        });
        Attach(document, "figures.csv", "region,total\neast,1\n"u8.ToArray(), "text/csv");
        Attach(document, "notes.bin", [1, 2, 3], mimeType: null);
        string appendix = fixture.File("appendix.pdf");
        using (var archive = new Aspose.Pdf.Document())
        {
            archive.Pages.Add().Paragraphs.Add(new Aspose.Pdf.Text.TextFragment("Archived appendix"));
            using var log = new MemoryStream();
            archive.Convert(log, Aspose.Pdf.PdfFormat.PDF_A_2B, Aspose.Pdf.ConvertErrorAction.Delete);
            archive.Save(appendix);
        }

        Attach(document, "appendix.pdf", File.ReadAllBytes(appendix), mimeType: null);
        document.PageLabels.UpdateLabel(0, new Aspose.Pdf.PageLabel
        {
            Prefix = "A-",
            StartingValue = 1,
            NumberingStyle = Aspose.Pdf.NumberingStyle.NumeralsArabic,
        });
        document.Info.Title = "Quarterly results";
        document.Save(path);
        return path;
    }

    private static void Attach(Aspose.Pdf.Document document, string name, byte[] content, string? mimeType)
    {
        var file = new Aspose.Pdf.FileSpecification(new MemoryStream(content), name, name)
        {
            Name = name,
            UnicodeName = name,
        };
        if (mimeType is not null)
        {
            file.MIMEType = mimeType;
        }

        document.EmbeddedFiles.Add(name, file);
    }

    private static List<string> OutlineTitles(IEnumerable<Aspose.Pdf.OutlineItemCollection> items) =>
        items.SelectMany(static item => OutlineTitles(item).Prepend(item.Title)).ToList();

    private static PdfSignResult Sign(PdfEngineFixture fixture, string input, string certificate, string password, string output) =>
        fixture.Engine.Sign(input, new PdfSignRequest
        {
            Output = TestOutput.At(fixture.File(output)),
            CertificatePath = certificate,
            CertificatePassword = password,
            Page = 1,
        });
}
