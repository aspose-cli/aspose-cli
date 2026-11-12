using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Aspose.Pdf;
using Aspose.Pdf.Facades;
using Aspose.Pdf.Forms;
using Aspose.Pdf.Text;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>
/// A form field's value is not page text: a conversion to text or Markdown leaves the values out
/// and names them as <c>form-values</c>, pointing to flattening the fields first or to
/// <c>pdf inspect --detail forms</c>, and <c>pdf read</c>, <c>pdf search</c> and
/// <c>pdf extract --what text</c> warn that the text they read holds none of them, pointing to
/// that inspect.
/// </summary>
public sealed class PdfFormValueTextTests
{
    // Letters and digits only, which Markdown writes as they are.
    private const string Applicant = "Zebra42";
    private const string City = "Springfield";

    /// <summary>
    /// From the command line: every command, option and operation the hints of the conversion and
    /// the read name exists (the workspace checks the hints of each JSON result), and the
    /// flattened copy the conversion's hint describes converts with the values.
    /// </summary>
    [Fact]
    public void ConvertAndRead_OfAFilledForm_GiveHintsThatRun()
    {
        using var fixture = new PdfEngineFixture();
        using var workspace = new TempWorkspace();
        File.Copy(Form(fixture, "filled.pdf", Applicant, City), workspace.File("filled.pdf"));

        CliResult converted = workspace.Run("pdf", "convert", "filled.pdf", "--to", "txt", "--out", "filled.txt", "--output-format", "json");
        CliResult read = workspace.Run("pdf", "read", "filled.pdf", "--output-format", "json");
        CliResult flattened = workspace.Run("pdf", "edit", "filled.pdf", "--ops", """[{"op":"flatten_forms"}]""", "--out", "flattened.pdf", "--output-format", "json");
        CliResult kept = workspace.Run("pdf", "convert", "flattened.pdf", "--to", "txt", "--out", "flattened.txt", "--output-format", "json");

        Assert.True(converted.ExitCode == 0, converted.StdOut + converted.StdErr);
        JsonNode lost = Assert.Single(converted.Json()["warnings"]!.AsArray(), static warning => warning!["details"]?["content"] is not null)!;
        Assert.Equal("""["form-values"]""", lost["details"]!["content"]!.ToJsonString());
        Assert.True(read.ExitCode == 0, read.StdOut + read.StdErr);
        Assert.Contains(read.Json()["warnings"]!.AsArray(), static warning => warning!["code"]!.GetValue<string>() == "FORM_VALUES_NOT_IN_TEXT");
        Assert.True(flattened.ExitCode == 0, flattened.StdOut + flattened.StdErr);
        Assert.True(kept.ExitCode == 0, kept.StdOut + kept.StdErr);
        Assert.Contains(Applicant, File.ReadAllText(workspace.File("flattened.txt")), StringComparison.Ordinal);
    }

    /// <summary>
    /// On an encrypted form at a path with a space, opened with <c>--password-env</c>, the
    /// conversion to text, the read, the search and the text extraction each give one command that
    /// reads the values: it names the input and its password variable, and run as written it reads them.
    /// </summary>
    [Fact]
    public void PageTextCommands_OfAnEncryptedForm_GiveAnInspectThatReadsTheValuesAsWritten()
    {
        using var fixture = new PdfEngineFixture();
        using var workspace = new TempWorkspace();
        using (var document = new Document(Form(fixture, "filled.pdf", Applicant, City)))
        {
            document.Encrypt("user-secret", "owner-secret", DocumentPrivilege.ForbidAll, CryptoAlgorithm.AESx256, usePdf20: false);
            document.Save(workspace.File("locked form.pdf"));
        }

        var variables = new Dictionary<string, string?>(StringComparer.Ordinal) { ["PDF_USER"] = "user-secret" };
        CliResult converted = null!, read = null!, searched = null!, extracted = null!;
        Parallel.Invoke(
            () => converted = workspace.RunWithEnv(variables, "pdf", "convert", "locked form.pdf", "--password-env", "PDF_USER", "--to", "txt", "--out", "locked.txt", "--output-format", "json"),
            () => read = workspace.RunWithEnv(variables, "pdf", "read", "locked form.pdf", "--password-env", "PDF_USER", "--output-format", "json"),
            () => searched = workspace.RunWithEnv(variables, "pdf", "search", "locked form.pdf", "--password-env", "PDF_USER", "--pattern", Applicant, "--output-format", "json"),
            () => extracted = workspace.RunWithEnv(variables, "pdf", "extract", "locked form.pdf", "--password-env", "PDF_USER", "--what", "text", "--out-dir", "text", "--output-format", "json"));

        string[] commands =
        [
            InspectionIn(Assert.Single(Succeeded(converted)["warnings"]!.AsArray(), static warning => warning!["details"]?["content"] is not null)!),
            .. new[] { read, searched, extracted }.Select(static result => InspectionIn(Assert.Single(Succeeded(result)["warnings"]?.AsArray() ?? [],
                static warning => warning!["code"]!.GetValue<string>() == "FORM_VALUES_NOT_IN_TEXT")!)),
        ];
        // All name the same command, which is run once as written.
        string command = Assert.Single(commands.Distinct(StringComparer.Ordinal));
        Assert.Contains(" --password-env PDF_USER", command, StringComparison.Ordinal);
        Assert.EndsWith(" --output-format json", command, StringComparison.Ordinal);
        CliResult inspected = workspace.RunCommandLine(command, variables);
        Assert.True(inspected.ExitCode == 0, inspected.StdOut + inspected.StdErr);
        JsonArray fields = inspected.Json()["forms"]!["fields"]!.AsArray();
        Assert.Equal(Applicant, fields.Single(static field => field!["name"]!.GetValue<string>() == "applicant")!["value"]!.GetValue<string>());
        Assert.Equal(City, fields.Single(static field => field!["name"]!.GetValue<string>() == "city")!["value"]!.GetValue<string>());
    }

    private static JsonNode Succeeded(CliResult result)
    {
        Assert.True(result.ExitCode == 0, result.StdOut + result.StdErr);
        return result.Json();
    }

    // The one runnable command a warning's hint quotes.
    private static string InspectionIn(JsonNode warning) =>
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(warning["hint"]!.GetValue<string>(), "'(aspose-cli [^']+)'")).Groups[1].Value;

    /// <summary>
    /// The conversion names the two filled fields, not the empty one or the unchecked check box,
    /// and following its hint, flattening the fields first, writes their values into the output.
    /// </summary>
    [Theory]
    [InlineData("txt")]
    [InlineData("md")]
    public void Convert_ToPageText_NamesTheFilledFieldsAndFlatteningFirstKeepsTheirValues(string format)
    {
        using var fixture = new PdfEngineFixture();
        string input = Form(fixture, "filled.pdf", Applicant, City);
        string output = fixture.File($"filled.{format}");

        PdfConvertResult result = PdfConvert.Run(fixture.Session, new PdfConvertRequest { Input = input, Output = TestOutput.At(output, format: format) });

        Warning lost = Assert.Single(result.Warnings!, static warning => warning.Details?["content"] is not null);
        Assert.Equal(WarningCodes.LossyConversion, lost.Code);
        Assert.Equal("""["form-values"]""", lost.Details!["content"]!.ToJsonString());
        Assert.Contains("2 filled form fields: 'applicant', 'city'.", lost.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("notes", lost.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("agree", lost.Message, StringComparison.Ordinal);
        Assert.Contains("flatten_forms", lost.Hint, StringComparison.Ordinal);
        Assert.Contains("'pdf inspect --detail forms'", lost.Hint, StringComparison.Ordinal);
        Assert.Null(lost.Location);
        Assert.DoesNotContain(Applicant, File.ReadAllText(output), StringComparison.Ordinal);

        string flattened = fixture.File("flattened.pdf");
        PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch { Ops = [new FlattenFormsOp()] },
            Output = TestOutput.At(flattened),
        });
        string kept = fixture.File($"flattened.{format}");
        PdfConvertResult again = PdfConvert.Run(fixture.Session, new PdfConvertRequest { Input = flattened, Output = TestOutput.At(kept, format: format) });

        string text = File.ReadAllText(kept);
        Assert.Contains(Applicant, text, StringComparison.Ordinal);
        Assert.Contains(City, text, StringComparison.Ordinal);
        Assert.DoesNotContain(again.Warnings ?? [], static warning => warning.Details?["content"] is not null);
    }

    /// <summary>One filled field is named with its address; a field on a page --pages left out is not counted.</summary>
    [Fact]
    public void Convert_ToText_LocatesASingleFilledFieldOfTheSelectedPages()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("two-pages.pdf");
        using (var document = new Document())
        {
            Page first = document.Pages.Add();
            first.Paragraphs.Add(new TextFragment("First page"));
            Page second = document.Pages.Add();
            second.Paragraphs.Add(new TextFragment("Second page"));
            document.Form.Add(new TextBoxField(first, new Rectangle(100, 700, 300, 720)) { PartialName = "first name", Value = Applicant }, 1);
            document.Form.Add(new TextBoxField(second, new Rectangle(100, 700, 300, 720)) { PartialName = "second", Value = City }, 2);
            document.Save(input);
        }

        PdfConvertResult result = PdfConvert.Run(fixture.Session, new PdfConvertRequest
        {
            Input = input,
            Pages = Sdk.Addressing.PageRange.Parse("1"),
            Output = TestOutput.At(fixture.File("first.txt"), format: "txt"),
        });

        Warning lost = Assert.Single(result.Warnings!, static warning => warning.Details?["content"] is not null);
        Assert.Contains("1 filled form field: 'first name'.", lost.Message, StringComparison.Ordinal);
        Assert.Equal("field/first name", lost.Location);
    }

    /// <summary>
    /// A signature field holds no value a reader filled in, and flattening it would invalidate the
    /// signature: a signed form's conversion names only the text field.
    /// </summary>
    [Fact]
    public void Convert_OfASignedForm_DoesNotNameTheSignature()
    {
        using var fixture = new PdfEngineFixture();
        string signed = fixture.File("signed.pdf");
        PdfSign.Run(fixture.Session, new PdfSignRequest
        {
            Input = Form(fixture, "filled.pdf", Applicant, city: null),
            Output = TestOutput.At(signed),
            CertificatePath = fixture.CreateCertificate("pw"),
            CertificatePassword = new Secret("pw"),
        });
        PdfInspectResult inspected = PdfInfo.Run(fixture.Session, new PdfInspectRequest { Input = signed, Details = ["forms"] });
        PdfFormField signature = Assert.Single(inspected.Forms!.Fields!, static field => field.Kind == "signature");

        PdfConvertResult result = PdfConvert.Run(fixture.Session, new PdfConvertRequest { Input = signed, Output = TestOutput.At(fixture.File("signed.txt"), format: "txt") });

        Warning lost = Assert.Single(result.Warnings!, static warning => warning.Details?["content"] is not null);
        Assert.Contains("1 filled form field: 'applicant'.", lost.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(signature.Name, lost.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A form with XFA beside its AcroForm fields is read-only and its fields cannot be flattened:
    /// the hint of its conversion gives only the command that reads the values.
    /// </summary>
    [Fact]
    public void Convert_OfAnXfaForm_GivesNoFlatteningInItsHint()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("xfa.pdf");
        using (var document = new Document())
        {
            Page page = document.Pages.Add();
            document.Form.Add(new TextBoxField(page, new Rectangle(72, 700, 200, 730)) { PartialName = "applicant", Value = Applicant }, 1);
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
            document.Save(input);
        }

        PdfConvertResult result = PdfConvert.Run(fixture.Session, new PdfConvertRequest { Input = input, Output = TestOutput.At(fixture.File("xfa.txt"), format: "txt") });

        Warning lost = Assert.Single(result.Warnings!, static warning => warning.Details?["content"] is not null);
        Assert.Contains("'applicant'", lost.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("flatten_forms", lost.Hint, StringComparison.Ordinal);
        Assert.Contains("'pdf inspect --detail forms'", lost.Hint, StringComparison.Ordinal);
    }

    /// <summary>
    /// A field without a widget on any page shows its value nowhere, so the text of a page leaves
    /// nothing of it out: the conversion and the read name only the field whose widget is on the page.
    /// </summary>
    [Fact]
    public void ConvertAndRead_NameOnlyTheFieldsWithAWidgetOnTheirPages()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("orphan.pdf");
        const string content = "BT /F1 12 Tf 20 300 Td (Applicant:) Tj ET";
        PdfEngineFixture.WriteRawPdf(input,
        [
            "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [6 0 R 7 0 R] >> >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 400 400] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R /Annots [6 0 R] >>",
            $"<< /Length {content.Length} >>\nstream\n{content}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Type /Annot /Subtype /Widget /FT /Tx /T (applicant) /V ({Applicant}) /Rect [100 290 300 310] /P 3 0 R >>",
            $"<< /FT /Tx /T (orphan) /V ({City}) >>",
        ]);

        PdfConvertResult converted = PdfConvert.Run(fixture.Session, new PdfConvertRequest { Input = input, Output = TestOutput.At(fixture.File("orphan.txt"), format: "txt") });
        PdfReadResult read = PdfRead.Run(fixture.Session, new PdfReadRequest { Input = input });

        Warning lost = Assert.Single(converted.Warnings!, static warning => warning.Details?["content"] is not null);
        Assert.Contains("1 filled form field: 'applicant'.", lost.Message, StringComparison.Ordinal);
        Warning unread = Assert.Single(read.Warnings ?? [], static warning => warning.Code.Name == "FORM_VALUES_NOT_IN_TEXT");
        Assert.Contains("1 filled form field on the pages read: 'applicant'.", unread.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Without a filled field, a conversion to text or Markdown gives only the general warning, as
    /// before; a format that keeps the values, such as DOCX or HTML, names none.
    /// </summary>
    [Theory]
    [InlineData("empty.pdf", "txt")]
    [InlineData("empty.pdf", "md")]
    [InlineData("plain.pdf", "txt")]
    [InlineData("filled.pdf", "docx")]
    [InlineData("filled.pdf", "html")]
    public void Convert_WithoutValuesLeftOut_GivesOnlyTheGeneralWarning(string name, string format)
    {
        using var fixture = new PdfEngineFixture();
        string input = name switch
        {
            "empty.pdf" => Form(fixture, name, applicant: null, city: null),
            "filled.pdf" => Form(fixture, name, Applicant, City),
            _ => Plain(fixture, name),
        };

        PdfConvertResult result = PdfConvert.Run(fixture.Session, new PdfConvertRequest { Input = input, Output = TestOutput.At(fixture.File($"out.{format}"), format: format) });

        Warning general = Assert.Single(result.Warnings!, static warning => warning.Code == WarningCodes.LossyConversion);
        Assert.Equal($"PDF conversion to {format} may not preserve every layout or interactive feature.", general.Message);
        Assert.Null(general.Details);
    }

    /// <summary>
    /// <c>pdf read</c> returns page text, which holds no field value: it names the filled fields
    /// and points to <c>pdf inspect --detail forms</c>, which reads their values.
    /// </summary>
    [Fact]
    public void Read_OfAFilledForm_NamesTheFieldsAndInspectReadsTheirValues()
    {
        using var fixture = new PdfEngineFixture();
        string input = Form(fixture, "filled.pdf", Applicant, City);

        PdfReadResult read = PdfRead.Run(fixture.Session, new PdfReadRequest { Input = input });

        Assert.DoesNotContain(Applicant, read.Pages[0].Text, StringComparison.Ordinal);
        Warning warning = Assert.Single(read.Warnings ?? [], static warning => warning.Code.Name == "FORM_VALUES_NOT_IN_TEXT");
        Assert.Contains("2 filled form fields on the pages read: 'applicant', 'city'.", warning.Message, StringComparison.Ordinal);
        Assert.Contains("'pdf inspect --detail forms'", warning.Hint, StringComparison.Ordinal);
        Assert.False(warning.AffectsCompleteness);

        PdfInspectResult inspected = PdfInfo.Run(fixture.Session, new PdfInspectRequest { Input = input, Details = ["forms"] });
        Assert.Equal(Applicant, Assert.Single(inspected.Forms!.Fields!, static field => field.Name == "applicant").Value);
    }

    /// <summary>A form whose fields are empty or unchecked, and a page without fields, read without the warning.</summary>
    [Fact]
    public void Read_WithoutAFilledFieldOnThePagesRead_GivesNoFormWarning()
    {
        using var fixture = new PdfEngineFixture();
        string empty = Form(fixture, "empty.pdf", applicant: null, city: null);
        string input = fixture.File("two-pages.pdf");
        using (var document = new Document())
        {
            document.Pages.Add().Paragraphs.Add(new TextFragment("First page"));
            Page second = document.Pages.Add();
            document.Form.Add(new TextBoxField(second, new Rectangle(100, 700, 300, 720)) { PartialName = "second", Value = City }, 2);
            document.Save(input);
        }

        PdfReadResult blank = PdfRead.Run(fixture.Session, new PdfReadRequest { Input = empty });
        PdfReadResult first = PdfRead.Run(fixture.Session, new PdfReadRequest { Input = input, Pages = Sdk.Addressing.PageRange.Parse("1") });

        Assert.DoesNotContain(blank.Warnings ?? [], static warning => warning.Code.Name == "FORM_VALUES_NOT_IN_TEXT");
        Assert.DoesNotContain(first.Warnings ?? [], static warning => warning.Code.Name == "FORM_VALUES_NOT_IN_TEXT");
    }

    /// <summary>
    /// A one-page form with the text fields <c>applicant</c>, <c>city</c> and <c>notes</c>, the
    /// last always empty, and an unchecked check box <c>agree</c>.
    /// </summary>
    private static string Form(PdfEngineFixture fixture, string name, string? applicant, string? city)
    {
        string path = fixture.File(name);
        using var document = new Document();
        Page page = document.Pages.Add();
        page.Paragraphs.Add(new TextFragment("Application"));
        document.Form.Add(new TextBoxField(page, new Rectangle(100, 700, 300, 720)) { PartialName = "applicant", Value = applicant ?? string.Empty }, 1);
        document.Form.Add(new TextBoxField(page, new Rectangle(100, 650, 300, 670)) { PartialName = "city", Value = city ?? string.Empty }, 1);
        document.Form.Add(new TextBoxField(page, new Rectangle(100, 600, 300, 620)) { PartialName = "notes" }, 1);
        document.Form.Add(new CheckboxField(page, new Rectangle(100, 550, 115, 565)) { PartialName = "agree" }, 1);
        document.Save(path);
        return path;
    }

    private static string Plain(PdfEngineFixture fixture, string name)
    {
        string path = fixture.File(name);
        using var document = new Document();
        document.Pages.Add().Paragraphs.Add(new TextFragment("Plain text"));
        document.Save(path);
        return path;
    }
}
