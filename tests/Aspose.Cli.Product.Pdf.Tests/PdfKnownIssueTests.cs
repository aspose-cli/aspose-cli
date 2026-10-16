using System.Collections.Concurrent;
using System.Text;
using Aspose.Cli.TestKit;
using Aspose.Pdf;
using Aspose.Pdf.Annotations;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>
/// The Aspose.PDF defects in KNOWN-ISSUES.md, reproduced with the SDK alone. Each passes while
/// the pinned SDK still has its defect.
/// </summary>
public sealed class PdfKnownIssueTests
{
    [LicensedFact]
    public async Task HtmlAndMarkdownImports_FetchNetworkResourcesPastTheLoader()
    {
        using var fixture = new PdfEngineFixture();
        await using var server = new ResourceHttpServer();
        string html = fixture.File("input.html");
        File.WriteAllText(html, $"""
            <html><head><link rel="stylesheet" href="{server.Url}/style.css"></head>
            <body><p>Local content</p><img src="{server.Url}/image.png"></body></html>
            """);
        var loaderCalls = new ConcurrentQueue<string>();
        var options = new HtmlLoadOptions(fixture.Temp.Path + Path.DirectorySeparatorChar)
        {
            // An empty result that is not cancelled keeps the SDK's own loader off.
            CustomLoaderOfExternalResources = uri =>
            {
                loaderCalls.Enqueue(uri);
                return new LoadOptions.ResourceLoadingResult([]) { LoadingCancelled = false };
            },
        };
        using (var document = new Document(html, options))
        {
            document.Save(fixture.File("html.pdf"));
        }
        int htmlRequests = server.RequestCount;
        Assert.NotEmpty(loaderCalls);

        // MdLoadOptions has no resource hook to install.
        string markdown = fixture.File("input.md");
        File.WriteAllText(markdown, $"# Local content\n\n![image]({server.Url}/markdown.png)\n");
        using (var document = new Document(markdown, new MdLoadOptions()))
        {
            document.Save(fixture.File("markdown.pdf"));
        }
        int markdownRequests = server.RequestCount - htmlRequests;

        KnownIssue.Reproduces(
            "PDF-HTML-EGRESS",
            htmlRequests > 0 && markdownRequests > 0,
            $"HTML made {htmlRequests} request(s) past a refusing loader, Markdown {markdownRequests}");
    }

    [LicensedFact]
    public void Destinations_ReadAnOmittedCoordinateAsZero()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("destinations.pdf");
        WriteDestinationDocument(input);

        using var document = new Document(input);
        var omitted = (XYZExplicitDestination)document.Outlines.Single(static item => item.Title == "Omitted").Destination;
        var zero = (XYZExplicitDestination)document.Outlines.Single(static item => item.Title == "Zero").Destination;

        KnownIssue.Reproduces(
            "PDF-MOVE-BOOKMARK",
            (omitted.Left, omitted.Top, omitted.Zoom) == (zero.Left, zero.Top, zero.Zoom),
            $"/XYZ null null null reads as {omitted.Left} {omitted.Top} {omitted.Zoom}");
    }

    [LicensedFact]
    public void OutlineDelete_RemovesBookmarksByTitle()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("same-titles.pdf");
        using (var document = new Document())
        {
            Page first = document.Pages.Add();
            Page second = document.Pages.Add();
            document.Outlines.Add(new OutlineItemCollection(document.Outlines)
            {
                Title = "Results",
                Destination = new FitExplicitDestination(first),
            });
            document.Outlines.Add(new OutlineItemCollection(document.Outlines)
            {
                Title = "Results",
                Destination = new FitExplicitDestination(second),
            });
            document.Save(input);
        }

        string output = fixture.File("same-titles.out.pdf");
        using (var document = new Document(input))
        {
            document.Outlines.Skip(1).First().Delete();
            document.Save(output);
        }

        using var reopened = new Document(output);
        int keptPage = ((ExplicitDestination)reopened.Outlines.Single().Destination).PageNumber;
        KnownIssue.Reproduces(
            "PDF-OUTLINE-DELETE-TITLE",
            keptPage == 2,
            $"deleting the second of two bookmarks titled Results kept the one on page {keptPage}");
    }

    [LicensedFact]
    public void PdfaConversion_TypesAnUntypedAttachmentAsPdf()
    {
        using var fixture = new PdfEngineFixture();
        string input = PdfNavigationTests.CreateAttachmentOnlyNameTree(fixture, "attachments.pdf");
        string output = fixture.File("archive.pdf");
        using (var document = new Document(input))
        {
            using var log = new MemoryStream();
            document.Convert(log, PdfFormat.PDF_A_3B, ConvertErrorAction.Delete);
            document.Save(output);
        }

        using var archived = new Document(output);
        string? type = archived.EmbeddedFiles.Cast<FileSpecification>().Single().MIMEType;

        KnownIssue.Reproduces(
            "PDF-PDFA-ATTACHMENT-TYPE",
            type == "application/pdf",
            $"converting an untyped CSV attachment to PDF/A-3B typed it '{type}'");
    }

    [LicensedFact]
    public void EmbeddedFilesAdd_DropsTheMediaTypeOfAnotherDocumentsAttachment()
    {
        using var fixture = new PdfEngineFixture();
        _ = fixture.LicenseState;
        string input = fixture.File("typed.pdf");
        using (var created = new Document())
        {
            created.Pages.Add();
            created.EmbeddedFiles.Add("schedule.xml", new FileSpecification(new MemoryStream("<x/>"u8.ToArray()), "schedule.xml", "Schedule")
            {
                MIMEType = "application/xml",
            });
            created.Save(input);
        }

        using var target = new Document();
        target.Pages.Add();
        // The same attachment, added once untouched and once after reading its type.
        using var untouchedSource = new Document(input);
        FileSpecification untouched = untouchedSource.EmbeddedFiles[1];
        target.EmbeddedFiles.Add("untouched.xml", untouched);
        using var readSource = new Document(input);
        FileSpecification read = readSource.EmbeddedFiles[1];
        string? sourceType = read.MIMEType;
        target.EmbeddedFiles.Add("read.xml", read);

        string? untouchedAfter = untouched.MIMEType;
        string? readAfter = read.MIMEType;
        string? untouchedAdded = target.EmbeddedFiles["untouched.xml"].MIMEType;
        string? readAdded = target.EmbeddedFiles["read.xml"].MIMEType;

        KnownIssue.Reproduces(
            "PDF-ATTACHMENT-COPY-TYPE",
            sourceType == "application/xml" && readAfter == sourceType && string.IsNullOrEmpty(untouchedAfter)
                && string.IsNullOrEmpty(untouchedAdded) && string.IsNullOrEmpty(readAdded),
            $"an attachment typed '{sourceType}' is typed '{untouchedAdded}' and '{readAdded}' once added to another document; "
            + $"afterwards the source's specification reads '{untouchedAfter}' when its type was not read before the add, and '{readAfter}' when it was");
    }

    [LicensedFact]
    public void DocxSave_AsksForEmbeddedFontsItDoesNotEmbed()
    {
        using var fixture = new PdfEngineFixture();
        _ = fixture.LicenseState;
        string docx = fixture.File("record.docx");
        using (var document = new Document())
        {
            document.Pages.Add().Paragraphs.Add(new Aspose.Pdf.Text.TextFragment("Employee record"));
            document.Save(docx, SaveFormat.DocX);
        }

        using var package = System.IO.Compression.ZipFile.OpenRead(docx);
        using var reader = new StreamReader(package.GetEntry("word/settings.xml")!.Open());
        string settings = reader.ReadToEnd();
        bool trueType = settings.Contains("<w:embedTrueTypeFonts", StringComparison.Ordinal);
        bool system = settings.Contains("<w:embedSystemFonts", StringComparison.Ordinal);
        int fonts = package.Entries.Count(static entry => entry.FullName.StartsWith("word/fonts/", StringComparison.Ordinal));

        KnownIssue.Reproduces(
            "PDF-DOCX-EMBED-FONTS",
            trueType && system && fonts == 0,
            $"the saved docx asks to embed TrueType fonts: {trueType}, system fonts: {system}, and embeds {fonts} font part(s)");
    }

    [LicensedFact]
    public void NamedDestinations_ThrowForANameTreeWithoutDests()
    {
        using var fixture = new PdfEngineFixture();
        string input = PdfNavigationTests.CreateAttachmentOnlyNameTree(fixture, "attachments.pdf");

        using var document = new Document(input);
        Exception? names = Record.Exception(() => document.NamedDestinations.Names);
        Exception? count = Record.Exception(() => document.NamedDestinations.Count);
        Exception? lookup = Record.Exception(() => document.NamedDestinations["appendix"]);

        KnownIssue.Reproduces(
            "PDF-NAMES-WITHOUT-DESTS",
            names is NullReferenceException && count is NullReferenceException && lookup is NullReferenceException,
            $"for a name tree with only EmbeddedFiles, Names threw {names?.GetType().Name ?? "nothing"}, Count threw {count?.GetType().Name ?? "nothing"} and the name lookup threw {lookup?.GetType().Name ?? "nothing"}");
    }

    [LicensedFact]
    public void TaggedContent_RewritesTheDocumentWhenRead()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("plain.pdf", pages: 1);
        string archive = fixture.File("archive.pdf");
        using (var document = new Document(input))
        {
            using var log = new MemoryStream();
            document.Convert(log, PdfFormat.PDF_A_2B, ConvertErrorAction.Delete);
            document.Save(archive);
        }

        using var reopened = new Document(archive);
        bool declaredBefore = reopened.IsPdfaCompliant;
        _ = reopened.TaggedContent.RootElement.ChildElements.Count;

        KnownIssue.Reproduces(
            "PDF-TAGGED-CONTENT-WRITES",
            declaredBefore && !reopened.IsPdfaCompliant && reopened.Info.Title == "Tagged PDF",
            $"reading TaggedContent changed IsPdfaCompliant from {declaredBefore} to {reopened.IsPdfaCompliant} and the title to '{reopened.Info.Title}'");
    }

    [LicensedFact]
    public void HtmlAndMarkdownImports_SetAPlaceholderTitleAuthorAndSubject()
    {
        using var fixture = new PdfEngineFixture();
        string html = fixture.File("titled.html");
        File.WriteAllText(html, """
            <html><head><title>Real title</title><meta name="author" content="Jane"></head>
            <body><p>Body</p></body></html>
            """);
        string markdown = fixture.File("notes.md");
        File.WriteAllText(markdown, "# Notes\n\nBody\n");

        using var fromHtml = new Document(html, new HtmlLoadOptions(fixture.Temp.Path + Path.DirectorySeparatorChar));
        using var fromMarkdown = new Document(markdown, new MdLoadOptions());
        string[] values =
        [
            fromHtml.Info.Title, fromHtml.Info.Author, fromHtml.Info.Subject,
            fromMarkdown.Info.Title, fromMarkdown.Info.Author, fromMarkdown.Info.Subject,
        ];

        KnownIssue.Reproduces(
            "PDF-IMPORT-INFO-PLACEHOLDER",
            values.All(static value => value == "Aspose"),
            $"the imports set title, author and subject to [{string.Join(", ", values)}]");
    }

    [LicensedFact]
    public void HtmlImport_NamesFormControlsOtherThanTextInputsItself()
    {
        using var fixture = new PdfEngineFixture();
        string html = fixture.File("form.html");
        File.WriteAllText(html, """
            <html><body><form>
            <input type="text" name="company"/>
            <input type="radio" name="kind" value="maker"/> Maker
            <input type="radio" name="kind" value="seller"/> Seller
            <input type="checkbox" name="iso9001" value="yes"/> ISO 9001
            <select name="region"><option value="east">East</option></select>
            </form></body></html>
            """);

        using var document = new Document(html, new HtmlLoadOptions(fixture.Temp.Path + Path.DirectorySeparatorChar));
        string[] names = [.. document.Form.Fields.Select(static field => field.FullName).Distinct()];
        string[] radioValues = [.. document.Form.Fields.OfType<Aspose.Pdf.Forms.RadioButtonOptionField>()
            .Select(static button => button.OptionName)];

        KnownIssue.Reproduces(
            "PDF-HTML-FORM-NAMES",
            names.Contains("company") && !names.Intersect(["kind", "iso9001", "region"]).Any()
                && !radioValues.Intersect(["maker", "seller"]).Any(),
            $"the import named the fields [{string.Join(", ", names)}] and gave the radio buttons the values [{string.Join(", ", radioValues)}]");
    }

    [LicensedFact]
    public void HtmlImport_DropsSomeInputTypesWithoutAField()
    {
        using var fixture = new PdfEngineFixture();
        string html = fixture.File("contact.html");
        string[] types = ["email", "tel", "url", "time", "datetime-local", "month", "week", "color", "range", "file", "image", "hidden"];
        File.WriteAllText(html, "<html><body><form><p><input type=\"text\" name=\"company\"/></p>"
            + string.Concat(types.Select(static type => $"<p><input type=\"{type}\" name=\"{type}\" value=\"x\"/></p>"))
            + "</form></body></html>");

        using var document = new Document(html, new HtmlLoadOptions(fixture.Temp.Path + Path.DirectorySeparatorChar));
        string[] names = [.. document.Form.Fields.Select(static field => field.FullName)];

        KnownIssue.Reproduces(
            "PDF-HTML-FORM-INPUTS",
            names.SequenceEqual(["company"]),
            $"the import of a text input and inputs of type {string.Join(", ", types)} made the fields [{string.Join(", ", names)}]");
    }

    [LicensedFact]
    public void AttachmentName_OpensTheFileItNames()
    {
        using var fixture = new PdfEngineFixture();
        string attachment = fixture.File("vendor-bank.csv");
        File.WriteAllText(attachment, "bank,account\n");

        using var stream = new FileStream(attachment, FileMode.Open, FileAccess.Read, FileShare.Read);
        var specification = new FileSpecification(stream, "vendor-bank.csv", string.Empty);
        // An absolute name stands for a relative one resolved against the working directory.
        Exception? error = Record.Exception(() => specification.Name = attachment);

        KnownIssue.Reproduces(
            "PDF-ATTACHMENT-NAME-OPENS-FILE",
            error is IOException,
            $"setting Name to a file opened for reading threw {error?.GetType().Name ?? "nothing"}");
    }

    [LicensedFact]
    public void HtmlImport_DrawsNoBoxForACheckBox()
    {
        using var fixture = new PdfEngineFixture();
        string html = fixture.File("checkbox.html");
        File.WriteAllText(html, """<html><body><form><input type="checkbox" name="iso9001"/> ISO 9001</form></body></html>""");

        using var document = new Document(html, new HtmlLoadOptions(fixture.Temp.Path + Path.DirectorySeparatorChar));
        var box = document.Form.Fields.OfType<Aspose.Pdf.Forms.CheckboxField>().Single();
        string[] drawn = [.. box.Appearance["N.Off"].Contents.Select(static drawing => drawing.GetType().Name)];

        KnownIssue.Reproduces(
            "PDF-HTML-CHECKBOX-BOX",
            box.Border is null && box.Characteristics.Border.IsEmpty && !drawn.Contains("ClosePathStroke") && !drawn.Contains("Stroke"),
            $"the check box has border {box.Border?.Width.ToString() ?? "none"}, border colour {box.Characteristics.Border} and its unchecked appearance draws [{string.Join(" ", drawn)}]");
    }

    [LicensedFact]
    public void PdfaConversion_KeepsTheRadioGroupAppearanceOfAnHtmlImport()
    {
        using var fixture = new PdfEngineFixture();
        string html = fixture.File("radio.html");
        File.WriteAllText(html, """
            <html><body><form>
            <input type="radio" name="kind" value="maker"/> Maker <input type="radio" name="kind" value="seller"/> Seller
            </form></body></html>
            """);
        string archive = fixture.File("radio.pdfa.pdf");
        bool converted;
        using (var document = new Document(html, new HtmlLoadOptions(fixture.Temp.Path + Path.DirectorySeparatorChar)))
        {
            using var log = new MemoryStream();
            converted = document.Convert(log, PdfFormat.PDF_A_2B, ConvertErrorAction.Delete);
            document.Save(archive);
        }

        using var reopened = new Document(archive);
        using var validation = new MemoryStream();
        bool valid = reopened.Validate(validation, PdfFormat.PDF_A_2B);
        string problems = Encoding.UTF8.GetString(validation.ToArray());

        KnownIssue.Reproduces(
            "PDF-PDFA-RADIO-APPEARANCE",
            converted && !valid && problems.Contains("Clause=\"6.3.3\"", StringComparison.Ordinal),
            $"the conversion returned {converted} and validation of the saved file returned {valid}");
    }

    [LicensedFact]
    public void PdfaValidationLog_RepeatsAProblemAndMarksItBothConvertibleAndNot()
    {
        using var fixture = new PdfEngineFixture();
        using var document = new Document(fixture.CreateDocument(pages: 1));
        using var log = new MemoryStream();
        document.Validate(log, PdfFormat.PDF_A_2B);
        log.Position = 0;
        var entries = System.Xml.Linq.XDocument.Load(log).Descendants("Problem")
            .Select(static problem => (Problem: $"{(string?)problem.Attribute("Clause")}|{(string?)problem.Attribute("Page")}|{problem.Value}", Convertible: (string?)problem.Attribute("Convertable")))
            .ToArray();
        string[] contradicted = [.. entries.GroupBy(static entry => entry.Problem)
            .Where(static group => group.Any(static entry => entry.Convertible == "True") && group.Any(static entry => entry.Convertible == "False"))
            .Select(static group => group.Key)];
        KnownIssue.Reproduces(
            "PDF-PDFA-LOG-REPEATS",
            contradicted.Length > 0,
            $"the log has {entries.Length} entries, of which no problem is marked both convertible and not");
    }

    [LicensedFact]
    public void TextSearch_ReadsAGapBetweenRunsAsASpace()
    {
        using var fixture = new PdfEngineFixture();
        string input = PdfMutateTests.WriteSpacedRuns(fixture, "autospace.pdf", "2026", "年", "10", "月");

        using var document = new Document(input);
        var absorber = new Aspose.Pdf.Text.TextAbsorber();
        document.Pages[1].Accept(absorber);
        var search = new Aspose.Pdf.Text.TextFragmentAbsorber("2026年10月");
        document.Pages[1].Accept(search);

        KnownIssue.Reproduces(
            "PDF-TEXT-GAP-SPACE",
            absorber.Text.Contains("2026 年", StringComparison.Ordinal) && search.TextFragments.Count == 0,
            $"runs 3 points apart extract as '{absorber.Text.Trim()}', and a search for them without spaces found {search.TextFragments.Count}");
    }

    [LicensedFact]
    public void Redaction_MovesTheRunsThatFollowTheRemovedText()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateRawDocument("runs.pdf", pages: 1, textContent: PdfMutateTests.ConsecutiveRuns);

        using var document = new Document(input);
        Page page = document.Pages[1];
        double before = RunStart(page, "signed");
        var secret = new Aspose.Pdf.Text.TextFragmentAbsorber("Jane Roe");
        page.Accept(secret);
        Rectangle removed = secret.TextFragments[1].Rectangle;
        var redaction = new RedactionAnnotation(page, removed);
        page.Annotations.Add(redaction);
        redaction.Redact();
        double after = RunStart(page, "signed");

        KnownIssue.Reproduces(
            "PDF-REDACT-TEXT-SHIFT",
            Math.Abs(before - after - removed.Width) < 1,
            $"the run after the {removed.Width:F1} points removed moved from x {before:F1} to x {after:F1}");

        static double RunStart(Page page, string text)
        {
            var absorber = new Aspose.Pdf.Text.TextFragmentAbsorber(text);
            page.Accept(absorber);
            return absorber.TextFragments[1].Rectangle.LLX;
        }
    }

    [LicensedFact]
    public void EncryptedSave_GarblesDocumentInformationBeyondLatin1()
    {
        using var fixture = new PdfEngineFixture();
        string output = fixture.File("encrypted.pdf");
        using (var document = new Document())
        {
            document.Pages.Add();
            document.Info.Subject = "采购申请";
            document.Encrypt(string.Empty, "owner", Permissions.PrintDocument, CryptoAlgorithm.AESx256);
            document.Save(output);
        }

        using var reopened = new Document(output, "owner");

        KnownIssue.Reproduces(
            "PDF-ENCRYPTED-INFO-TEXT",
            reopened.Info.Subject != "采购申请",
            $"the subject '采购申请' reads '{reopened.Info.Subject}'");
    }

    [LicensedFact]
    public void Save_GarblesADocumentInformationNameBeyondLatin1()
    {
        using var fixture = new PdfEngineFixture();
        string output = fixture.File("names.pdf");
        using (var document = new Document())
        {
            document.Pages.Add();
            document.Info["项目编号"] = "A-1";
            document.Info["Café"] = "B-2";
            document.Save(output);
        }

        using var reopened = new Document(output);
        string[] keys = [.. reopened.Info.Keys];

        KnownIssue.Reproduces(
            "PDF-INFO-KEY-BEYOND-LATIN1",
            !keys.Contains("项目编号") && keys.Contains("Café"),
            $"the names read back as {string.Join(", ", keys.Select(static key => $"'{key}'"))}");
    }

    [LicensedFact]
    public void Signing_EmbedsTheWholeFontOfTheSignatureAppearance()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("unsigned.pdf", pages: 1);
        string certificate = fixture.CreateCertificate("pw");
        string output = fixture.File("signed.pdf");
        using (var document = new Document(input))
        using (var facade = new Aspose.Pdf.Facades.PdfFileSignature(document))
        {
            facade.Sign(1, false, new System.Drawing.Rectangle(0, 0, 0, 0),
                new Aspose.Pdf.Forms.PKCS7(certificate, "pw") { Reason = "Approved", Location = "Shanghai" });
            facade.Save(output);
        }

        long growth = new FileInfo(output).Length - new FileInfo(input).Length;
        using var signed = new Document(output);
        string[] whole = [.. signed.FontUtilities.GetAllFonts().Where(static font => font.IsEmbedded && !font.IsSubset).Select(static font => font.FontName)];

        KnownIssue.Reproduces(
            "PDF-SIGN-FULL-FONT",
            whole.Length > 0 && growth > 200_000,
            $"an invisible signature added {growth} bytes and embedded the whole font(s) {string.Join(", ", whole)}");
    }

    [LicensedFact]
    public void Signing_LeavesOutAChineseSignerNameWhenNoOtherTextIsChinese()
    {
        using var fixture = new PdfEngineFixture();
        _ = fixture.LicenseState;
        string input = fixture.CreateDocument("unsigned.pdf", pages: 1);
        using var key = System.Security.Cryptography.RSA.Create(2048);
        var request = new System.Security.Cryptography.X509Certificates.CertificateRequest(
            "CN=张伟", key, System.Security.Cryptography.HashAlgorithmName.SHA256, System.Security.Cryptography.RSASignaturePadding.Pkcs1);
        using (var created = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(2)))
        {
            File.WriteAllBytes(fixture.File("signer.pfx"), created.Export(System.Security.Cryptography.X509Certificates.X509ContentType.Pfx, "pw"));
        }

        (string latinLine, string latinFont) = SignerLine("Café");
        (string chineseLine, string chineseFont) = SignerLine("采购申请");

        KnownIssue.Reproduces(
            "PDF-SIGN-CJK-SIGNER-FONT",
            latinLine == "Digitally signed by 'CN='" && chineseLine == "Digitally signed by 'CN=张伟'",
            $"with the reason 'Café' the signer line reads \"{latinLine}\" in {latinFont}; with '采购申请' it reads \"{chineseLine}\" in {chineseFont}");

        (string Line, string Font) SignerLine(string reason)
        {
            string output = fixture.File($"signed-{reason.Length}.pdf");
            using (var document = new Document(input))
            using (var facade = new Aspose.Pdf.Facades.PdfFileSignature(document))
            {
                facade.Sign(1, true, new System.Drawing.Rectangle(100, 500, 300, 80),
                    new Aspose.Pdf.Forms.PKCS7(fixture.File("signer.pfx"), "pw") { Reason = reason });
                facade.Save(output);
            }

            using var signed = new Document(output);
            signed.Form.Flatten();
            var absorber = new Aspose.Pdf.Text.TextFragmentAbsorber();
            signed.Pages[1].Accept(absorber);
            Aspose.Pdf.Text.TextFragment line = absorber.TextFragments.First(static fragment => fragment.Text.StartsWith("Digitally signed by", StringComparison.Ordinal));
            return (line.Text, line.TextState.Font.FontName);
        }
    }

    [LicensedFact]
    public void Rendering_LeavesOutThinGlyphStrokesBelow300Dpi()
    {
        using var fixture = new PdfEngineFixture();
        string path = fixture.File("underscores.pdf");
        using (var document = new Document())
        {
            // The same line at six sub-pixel offsets.
            for (int index = 0; index < 6; index++)
            {
                var stamp = new TextStamp("签字 ________") { XIndent = 24, YIndent = 20 + index * 0.17 };
                stamp.TextState.Font = Aspose.Pdf.Text.FontRepository.FindFont("SimSun");
                document.Pages.Add().AddStamp(stamp);
            }
            document.Save(path);
        }

        using var reopened = new Document(path);
        int missing150 = MissingUnderscores(reopened, 150);
        int missing300 = MissingUnderscores(reopened, 300);

        KnownIssue.Reproduces(
            "PDF-RENDER-THIN-GLYPH",
            missing150 > 0 && missing300 == 0,
            $"the underscores are missing on {missing150} of 6 pages at 150 DPI and {missing300} at 300 DPI");

        static int MissingUnderscores(Document document, int dpi)
        {
            int missing = 0;
            foreach (Page page in document.Pages)
            {
                var absorber = new Aspose.Pdf.Text.TextFragmentAbsorber("________");
                page.Accept(absorber);
                Rectangle box = absorber.TextFragments[1].Rectangle;
                using var png = new MemoryStream();
                new Aspose.Pdf.Devices.PngDevice(new Aspose.Pdf.Devices.Resolution(dpi)).Process(page, png);
                png.Position = 0;
                using SkiaSharp.SKBitmap bitmap = SkiaSharp.SKBitmap.Decode(png);
                missing += PdfEngineFixture.DarkestUnderscorePixel(bitmap, page, box, dpi / 72d) < 250 ? 0 : 1;
            }

            return missing;
        }
    }

    [LicensedFact]
    public void Sign_SwapsTheRectangleOfAPageRotatedByAQuarterTurn()
    {
        using var fixture = new PdfEngineFixture();
        string certificate = fixture.CreateCertificate("pw");
        var placed = new Dictionary<int, Rectangle>();
        foreach ((int degrees, Rotation rotation) in new[] { (0, Rotation.None), (90, Rotation.on90), (180, Rotation.on180) })
        {
            string input = fixture.File($"page-{degrees}.pdf");
            using (var document = new Document())
            {
                Page page = document.Pages.Add();
                page.SetPageSize(400, 600);
                page.Rotate = rotation;
                document.Save(input);
            }

            string output = fixture.File($"signed-{degrees}.pdf");
            using (var document = new Document(input))
            using (var facade = new Aspose.Pdf.Facades.PdfFileSignature(document))
            {
                facade.Sign(1, true, new System.Drawing.Rectangle(10, 20, 100, 30), new Aspose.Pdf.Forms.PKCS7(certificate, "pw"));
                facade.Save(output);
            }

            using var signed = new Document(output);
            placed[degrees] = signed.Form.Fields[0].Rect;
        }

        static string Describe(Rectangle rect) => $"{rect.LLX},{rect.LLY},{rect.Width},{rect.Height}";
        KnownIssue.Reproduces(
            "PDF-SIGN-RECT-ROTATED",
            Describe(placed[0]) == "10,20,100,30" && Describe(placed[180]) == "10,20,100,30" && Describe(placed[90]) == "20,10,30,100",
            $"the rectangle 10,20,100,30 lands at {Describe(placed[0])} unrotated, {Describe(placed[90])} at 90 degrees and {Describe(placed[180])} at 180 degrees");
    }

    [LicensedFact]
    public void TextRectangles_OnARotatedCroppedPage_StartFromAnOriginThatMovesWithTheRotation()
    {
        using var fixture = new PdfEngineFixture();
        var found = new Dictionary<int, string>();
        foreach ((int degrees, Rotation rotation) in new[] { (0, Rotation.None), (90, Rotation.on90), (180, Rotation.on180), (270, Rotation.on270) })
        {
            string path = TextPage(fixture, $"page-{degrees}.pdf");
            using (var turned = new Document(path))
            {
                turned.Pages[1].CropBox = new Rectangle(20, 30, 400, 600);
                turned.Pages[1].Rotate = rotation;
                turned.Save(path);
            }

            using var document = new Document(path);
            Page page = document.Pages[1];
            var absorber = new Aspose.Pdf.Text.TextFragmentAbsorber("SECRET");
            page.Accept(absorber);
            Rectangle rect = absorber.TextFragments[1].Rectangle;
            found[degrees] = $"{Math.Round(rect.LLX)},{Math.Round(rect.URY)}";
        }

        // The crop shows 380 x 570 points of the page, 570 x 380 turned a quarter. Measured from
        // the lower-left corner of what is seen, the text's top-left corner lies at 470,320 at
        // 90 degrees, 239,100 at 180 and 78,141 at 270; upright the engine measures from the
        // page's own origin, at 80,522.
        string reported = string.Join(" ", found.Select(static pair => $"{pair.Key}:{pair.Value}"));
        KnownIssue.Reproduces(
            "PDF-TEXT-RECT-ROTATED-CROP",
            reported == "0:80,522 90:500,320 180:239,100 270:78,161",
            $"the corner is reported at {reported}");
    }

    [LicensedFact]
    public void Redact_OnARotatedPageLeavesTheTextUnderItsCover()
    {
        using var fixture = new PdfEngineFixture();
        string input = TextPage(fixture, "page.pdf");
        Rectangle drawn;
        using (var document = new Document(input))
        {
            var absorber = new Aspose.Pdf.Text.TextFragmentAbsorber("SECRET");
            document.Pages[1].Accept(absorber);
            drawn = absorber.TextFragments[1].Rectangle;
        }

        bool Removed(Rotation rotation)
        {
            using var document = new Document(input);
            Page page = document.Pages[1];
            page.Rotate = rotation;
            var cover = new RedactionAnnotation(page, new Rectangle(drawn.LLX - 1, drawn.LLY - 1, drawn.URX + 1, drawn.URY + 1));
            page.Annotations.Add(cover);
            cover.Redact();
            var absorber = new Aspose.Pdf.Text.TextFragmentAbsorber("SECRET");
            page.Accept(absorber);
            return absorber.TextFragments.Count == 0;
        }

        bool upright = Removed(Rotation.None);
        bool turned = Removed(Rotation.on90);
        KnownIssue.Reproduces(
            "PDF-REDACT-ROTATED",
            upright && !turned,
            $"a cover over the text removes it {(upright ? "upright" : "not even upright")} and {(turned ? "also" : "not")} on a page rotated by 90 degrees");
    }

    [LicensedFact]
    public void TextSearch_OnAStampedPageWrapsItsContentInASavedGraphicsState()
    {
        using var fixture = new PdfEngineFixture();
        string path = TextPage(fixture, "stamped.pdf");
        using (var document = new Document(path))
        {
            document.Pages[1].AddStamp(new Aspose.Pdf.TextStamp("Internal") { Background = false });
            document.Save(path);
        }

        using var reopened = new Document(path);
        Page page = reopened.Pages[1];
        string[] before = [.. page.Contents.Select(static op => op.ToString() ?? string.Empty)];
        page.Accept(new Aspose.Pdf.Text.TextFragmentAbsorber("absent"));
        string[] after = [.. page.Contents.Select(static op => op.ToString() ?? string.Empty)];
        KnownIssue.Reproduces(
            "PDF-TEXT-SEARCH-WRAPS-CONTENT",
            after.Length == before.Length + 2 && after[0] == "q" && after[^1] == "Q" && after[1..^1].SequenceEqual(before),
            $"a search left {after.Length} operators where there were {before.Length}");
    }

    /// <summary>A 595 x 842 pt page with SECRET drawn upright at 80,500 pt in 20 pt type.</summary>
    private static string TextPage(PdfEngineFixture fixture, string fileName)
    {
        string path = fixture.File(fileName);
        using var document = new Document();
        Page page = document.Pages.Add();
        page.SetPageSize(595, 842);
        var fragment = new Aspose.Pdf.Text.TextFragment("SECRET") { Position = new Aspose.Pdf.Text.Position(80, 500) };
        fragment.TextState.FontSize = 20;
        page.Paragraphs.Add(fragment);
        document.Save(path);
        return path;
    }

    [LicensedFact]
    public void ConcurrentSaves_LeaveTheModificationDateOut()
    {
        using var fixture = new PdfEngineFixture();
        string[] inputs = [.. Enumerable.Range(0, 16).Select(index =>
        {
            string path = fixture.File($"input-{index}.pdf");
            using var document = new Document();
            document.Pages.Add();
            document.Info.Title = "Quarterly";
            document.Save(path);
            return path;
        })];

        static bool Stamped(string path)
        {
            using var document = new Document(path);
            return document.Info.TryGetValue("ModDate", out string? value) && !string.IsNullOrWhiteSpace(value);
        }

        // Saved one at a time, as the PdfSaveStampTests collection does, every output has the date;
        // other tests of this process may save at the same time, so this test checks only the
        // concurrent saves.
        var together = new ConcurrentBag<bool>();
        Parallel.ForEach(inputs, input =>
        {
            string output = input.Replace("input-", "together-", StringComparison.Ordinal);
            using (var document = new Document(input))
            {
                document.Info.Clear();
                document.Save(output);
            }

            together.Add(Stamped(output));
        });

        KnownIssue.Reproduces(
            "PDF-CONCURRENT-SAVE-MODDATE",
            together.Contains(false),
            $"saved at the same time, {together.Count(static stamped => !stamped)} of {together.Count} outputs have no ModDate");
    }

    /// <summary>One page and two bookmarks: one omits every coordinate, one names 0.</summary>
    private static void WriteDestinationDocument(string path)
    {
        string[] objects =
        [
            "<< /Type /Catalog /Pages 2 0 R /Outlines 5 0 R >>",
            "<< /Type /Pages /Count 1 /Kids [3 0 R] >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R >>",
            "<< /Length 0 >>\nstream\n\nendstream",
            "<< /Type /Outlines /First 6 0 R /Last 7 0 R /Count 2 >>",
            "<< /Title (Omitted) /Parent 5 0 R /Next 7 0 R /Dest [3 0 R /XYZ null null null] >>",
            "<< /Title (Zero) /Parent 5 0 R /Prev 6 0 R /Dest [3 0 R /XYZ 0 0 0] >>",
        ];
        PdfEngineFixture.WriteRawPdf(path, objects);
    }
}
