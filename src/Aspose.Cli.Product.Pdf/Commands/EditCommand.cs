using Aspose.Cli.Sdk.Extensibility.Output;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class EditCommand
{
    public static CommandDefinition<PdfEditRequest, PdfEditResult> Create() =>
        EditDefinition.Create<PdfOp, PdfOpsBatch, PdfEditRequest, PdfEditResult>(
            new BoundedEditCommand<PdfOp, PdfOpsBatch>(new BoundedEditDefinition<PdfOp, PdfOpsBatch>
            {
                Contracts = ProductJsonContext.Definition,
                VerifyDescription = "Read the staged output back and check the effect of each form, redaction, bookmark, metadata, attachment and page operation.",
                Writes = PdfFormats.Document,
            }),
            "Apply one validated, atomic PDF operation batch.",
            new CommandTraits
            {
                Input = PdfInputs.Document,
                UsesFonts = true,
            },
            [],
            static (_, edit, standard) => new PdfEditRequest
            {
                Input = standard.Input,
                Batch = edit.Batch,
                Output = standard.Output,
                Options = edit.Options,
                Password = standard.InputPassword,
                Verify = edit.Verify,
                OpSecrets = edit.Secrets,
            },
            Table);

    internal static void Table(PdfEditResult result, TableSurface surface)
    {
        ResultText.Edit(surface, result.DryRun, result.Output, result.Applied, result.Backup);
        if (result.PagesTouched is { Count: > 0 } pages)
        {
            surface.Out.WriteLine($"pages touched: {string.Join(", ", pages)}");
        }

        if (result.Verification is { } verification)
        {
            ResultText.Verification(
                surface,
                verification.Ok,
                verification.Issues,
                $" ({verification.CheckedOps.Count} operation(s) read back: "
                + $"{(verification.CheckedOps.Count == 0 ? "none" : string.Join(", ", verification.CheckedOps))})",
                hints: true);
        }
    }
}
