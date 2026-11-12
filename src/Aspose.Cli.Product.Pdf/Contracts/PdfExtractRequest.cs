using Aspose.Cli.Sdk.Formats;
using Aspose.Cli.Sdk.IO.Output;

namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>Options for a bounded PDF extraction into a directory.</summary>
public sealed record PdfExtractRequest
{
    /// <summary>The PDF to open.</summary>
    public required string Input { get; init; }

    /// <summary>What to extract, one of <see cref="PdfExtractKinds"/>.</summary>
    public required string What { get; init; }

    /// <summary>The resolved directory that receives the files.</summary>
    public required ResolvedDirectory Output { get; init; }
    public PageRange? Pages { get; init; }
    public Secret? Password { get; init; }

    /// <summary>Whether each table's UTF-8 CSV starts with a byte order mark.</summary>
    public bool ByteOrderMark { get; init; }

    /// <summary>The format the form data is written in (<c>--form-format</c>), with <c>--what forms</c> only.</summary>
    public FormatDescriptor? FormFormat { get; init; }

    /// <summary>
    /// The quoted command line that extracts the text of the same pages into the same directory,
    /// with the input password's environment variable when one gave it, which the warning that no
    /// table was found names; null for an extraction made without a command line.
    /// </summary>
    public string? TextExtraction { get; init; }

    /// <summary>The quoted command line that reviews the input, as <see cref="TextExtraction"/> names one; null likewise.</summary>
    public string? InputReview { get; init; }

    /// <summary>
    /// The quoted command line that reads the input's form fields with their values, which the
    /// warning that the extracted text leaves out field values names; null likewise.
    /// </summary>
    public string? FormsInspection { get; init; }
}

/// <summary>What <c>pdf extract --what</c> takes out of a PDF.</summary>
public static class PdfExtractKinds
{
    /// <summary>The images placed on the selected pages.</summary>
    public const string Images = "images";

    /// <summary>The embedded files.</summary>
    public const string Attachments = "attachments";

    /// <summary>The text of the selected pages, as one file.</summary>
    public const string Text = "text";

    /// <summary>The tables found on the selected pages, one CSV file each.</summary>
    public const string Tables = "tables";

    /// <summary>The form data, as one JSON, FDF or XFDF file.</summary>
    public const string Forms = "forms";

    /// <summary>Every value, in documentation order.</summary>
    public static IReadOnlyList<string> All { get; } = [Images, Attachments, Text, Tables, Forms];
}
