using Aspose.Cli.Sdk.IO.Output;

namespace Aspose.Cli.Product.Pdf.Commands;

/// <summary>The input documents PDF commands declare.</summary>
internal static class PdfInputs
{
    /// <summary>The PDF every reading command opens.</summary>
    public static readonly InputDocument Document = new("PDF document to open.", "the PDF");

    /// <summary>The password a writing command can put on the PDF it writes.</summary>
    public static readonly EncryptedOutput EncryptedPdf = new("the output PDF");

    /// <summary>
    /// The command line, quoted for a hint, that reads the form fields of the invocation's input
    /// with their values: <c>pdf inspect &lt;input&gt; --detail forms</c>, with the input
    /// password's environment variable when one gave it.
    /// </summary>
    public static string FormsInspection(StandardInvocation standard) =>
        standard.ContinuationAt("pdf", "inspect").Option("--detail", PdfInspectDetails.Forms).Quoted();

    /// <summary>
    /// The command line, quoted for a hint, that reads the bookmarks of the edited document as
    /// they were before the edit: <c>pdf inspect &lt;file&gt; --detail outline</c> on its input, or
    /// on the backup of an in-place edit, which replaces the input; null for an in-place edit
    /// without a backup, which leaves no file to read them from.
    /// </summary>
    public static string? OutlineInspection(StandardInvocation standard, ResolvedOutput output) =>
        (output.InPlace ? output.BackupPath : standard.Input) is { } document
            ? standard.ContinuationOf(document).ContinuationAt("pdf", "inspect").Option("--detail", PdfInspectDetails.Outline).Quoted()
            : null;
}
