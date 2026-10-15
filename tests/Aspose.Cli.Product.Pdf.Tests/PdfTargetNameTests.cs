using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Product.Pdf.Engine.Editing;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Pdf;
using Aspose.Pdf.Forms;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>
/// An attachment or form field addressed by its name holds the name in its target escaped as
/// every product escapes one: a slash, a percent sign, a control character and white space at
/// the start or end as UTF-8 %XX, any other character as it is. An operation that failed
/// attempts the same address an applied one changes.
/// </summary>
public sealed class PdfTargetNameTests
{
    private const string Name = " Q3 100%/x.txt ";
    private const string Escaped = "%20Q3 100%25%2Fx.txt%20";

    [Fact]
    public void Attachments_EscapeTheirNameInTheTargetsOfAppliedAndFailedOperations()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("named.pdf", pages: 1);
        string source = fixture.File("source.txt");
        File.WriteAllText(source, "east,1\n");

        PdfEditResult result = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch
            {
                Ops =
                [
                    new AddAttachmentOp { Path = source, Name = Name },
                    new DeleteAttachmentOp { Name = "missing 50%/y " },
                ],
            },
            Output = TestOutput.At(fixture.File("named.out.pdf")),
            Options = new EditCommandOptions { BestEffort = true },
        });

        Assert.Equal([OpStatuses.Ok, OpStatuses.Failed], result.Applied.Select(static item => item.Status));
        Assert.Equal([$"attachment/{Escaped}"], result.Applied[0].Targets);
        Assert.Equal(["attachment/missing 50%25%2Fy%20"], result.Applied[1].Targets);
    }

    [Fact]
    public void Attachments_WithTheSameContentAreTargetedByTheirOwnName()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("same-content.pdf", pages: 1);
        string b = fixture.File("b.txt");
        string a = fixture.File("a.txt");
        File.WriteAllText(b, "same");
        File.WriteAllText(a, "same");

        PdfEditResult result = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch { Ops = [new AddAttachmentOp { Path = b }, new AddAttachmentOp { Path = a }] },
            Output = TestOutput.At(fixture.File("same-content.out.pdf")),
        });

        Assert.Equal(["attachment/b.txt"], result.Applied[0].Targets);
        Assert.Equal(["attachment/a.txt"], result.Applied[1].Targets);
    }

    [Fact]
    public void FormFields_EscapeTheirNameInTheTargetsOfAppliedAndFailedOperations()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("named-form.pdf");
        using (var document = new Document())
        {
            Page page = document.Pages.Add();
            document.Form.Add(new TextBoxField(page, new Rectangle(72, 650, 280, 680)) { PartialName = Name, Value = "Initial" });
            document.Save(input);
        }

        PdfEditResult result = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch
            {
                Ops =
                [
                    new SetFormFieldOp { Name = Name, Value = "Contoso" },
                    new SetFormFieldOp { Name = "missing 50%/y ", Value = "x" },
                    new FlattenFormsOp { Fields = ["missing 50%/y "] },
                ],
            },
            Output = TestOutput.At(fixture.File("named-form.out.pdf")),
            Options = new EditCommandOptions { BestEffort = true },
        });

        Assert.Equal([OpStatuses.Ok, OpStatuses.Failed, OpStatuses.Failed], result.Applied.Select(static item => item.Status));
        Assert.Equal([$"field/{Escaped}"], result.Applied[0].Targets);
        Assert.Equal(["field/missing 50%25%2Fy%20"], result.Applied[1].Targets);
        Assert.Equal(["field/missing 50%25%2Fy%20"], result.Applied[2].Targets);
    }

    /// <summary>
    /// The targets of an edit and the locations of its verification name a field, an attachment
    /// and a custom property with a name to escape exactly as the escaped name a read of the output
    /// reports for the same object.
    /// </summary>
    [Fact]
    public void TargetsAndVerification_NameObjectsAsTheReadOfTheOutputDoes()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("read-named.pdf");
        using (var document = new Document())
        {
            Page page = document.Pages.Add();
            document.Form.Add(new TextBoxField(page, new Rectangle(72, 650, 280, 680)) { PartialName = Name, Value = "Initial" });
            document.Save(input);
        }

        string source = fixture.File("source.txt");
        File.WriteAllText(source, "east,1\n");
        PdfOp[] ops =
        [
            new SetFormFieldOp { Id = "op-0001", Name = Name, Value = "Contoso" },
            new AddAttachmentOp { Id = "op-0002", Path = source, Name = Name },
            new SetPropertiesOp { Id = "op-0003", Custom = new Dictionary<string, string> { [Name] = "v" } },
        ];
        string output = fixture.File("read-named.out.pdf");
        PdfEditResult result = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch { Ops = ops },
            Output = TestOutput.At(output),
            Verify = true,
        });

        PdfInspectResult read = PdfInfo.Run(fixture.Session, new PdfInspectRequest
        {
            Input = output,
            Details = [PdfInspectDetails.Forms, PdfInspectDetails.Attachments, PdfInspectDetails.Properties],
        });
        string field = "field/" + TargetName.Escape(Assert.Single(read.Forms!.Fields).Name);
        string attachment = "attachment/" + TargetName.Escape(Assert.Single(read.Attachments!).Name);
        string property = "property/" + TargetName.Escape(Assert.Single(read.Properties!, static entry => entry.Value == "v").Key);
        Assert.Equal([[field], [attachment], [property]], result.Applied.Select(static item => item.Targets));
        Assert.True(result.Verification!.Ok, string.Join("; ", result.Verification.Issues.Select(static issue => issue.Message)));

        // An output without these effects locates each issue under the same address.
        using var unchanged = new Document(input);
        var verifier = new PdfEditVerifier(unchanged);
        foreach (PdfOp op in ops)
        {
            verifier.Record(op, AppliedOperations.Changed(PdfOp.Catalog, op, 1));
        }

        Assert.Equal(
            new[] { attachment, field, property }.Order(StringComparer.Ordinal),
            verifier.Verify(unchanged, fixture.LicenseState, deadline: null).Issues.Select(static issue => issue.Location).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// A verification issue locates a field, attachment or property under the address its
    /// target gives it. A custom key that spells a standard entry, which only a batch that skipped
    /// validation could hold, is located under the key itself.
    /// </summary>
    [Fact]
    public void Verify_LocatesAMissingEffectUnderTheEscapedName()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("unchanged.pdf", pages: 1);
        string source = fixture.File("source.txt");
        File.WriteAllText(source, "east,1");
        using var document = new Document(input);
        var verifier = new PdfEditVerifier(document);

        PdfOp[] ops =
        [
            new SetFormFieldOp { Id = "op-0001", Name = Name, Value = "Contoso" },
            new AddAttachmentOp { Id = "op-0002", Path = source, Name = Name },
            new SetPropertiesOp { Id = "op-0003", Custom = new Dictionary<string, string> { [Name] = "v", ["title"] = "w" } },
        ];
        foreach (PdfOp op in ops)
        {
            verifier.Record(op, AppliedOperations.Changed(PdfOp.Catalog, op, op is SetPropertiesOp ? 2 : 1));
        }

        VerificationFacts verification = verifier.Verify(document, fixture.LicenseState, deadline: null);

        Assert.Equal(
            [$"attachment/{Escaped}", $"field/{Escaped}", $"property/{Escaped}", "property/title"],
            verification.Issues.Select(static issue => issue.Location).Order(StringComparer.Ordinal));
    }
}
