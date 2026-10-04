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
    public void NamedDestinations_ThrowForANameTreeWithoutDests()
    {
        using var fixture = new PdfEngineFixture();
        string input = PdfNavigationTests.CreateAttachmentOnlyNameTree(fixture, "attachments.pdf");

        using var document = new Document(input);
        Exception? names = Record.Exception(() => document.NamedDestinations.Names);
        Exception? count = Record.Exception(() => document.NamedDestinations.Count);

        KnownIssue.Reproduces(
            "PDF-NAMES-WITHOUT-DESTS",
            names is NullReferenceException && count is NullReferenceException,
            $"for a name tree with only EmbeddedFiles, Names threw {names?.GetType().Name ?? "nothing"} and Count threw {count?.GetType().Name ?? "nothing"}");
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
        string[] types = ["email", "tel", "url", "time", "datetime-local", "month", "week", "color", "range", "file"];
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
        var pdf = new StringBuilder("%PDF-1.7\n");
        var offsets = new List<int>();
        for (int index = 0; index < objects.Length; index++)
        {
            offsets.Add(pdf.Length);
            pdf.Append($"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
        }
        int xref = pdf.Length;
        pdf.Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (int offset in offsets)
        {
            pdf.Append($"{offset:0000000000} 00000 n \n");
        }
        pdf.Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        File.WriteAllBytes(path, Encoding.ASCII.GetBytes(pdf.ToString()));
    }
}
