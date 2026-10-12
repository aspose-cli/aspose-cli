namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>Options for a budgeted page-text read.</summary>
public sealed record PdfReadRequest
{
    /// <summary>The PDF to open.</summary>
    public required string Input { get; init; }
    public PageRange? Pages { get; init; }
    public string Mode { get; init; } = PdfReadModes.Plain;
    public int MaxCharacters { get; init; } = 20_000;
    public Secret? Password { get; init; }
}

/// <summary>Stable PDF read projection modes.</summary>
public static class PdfReadModes
{
    public const string Plain = "plain";
    public const string Layout = "layout";
    public static IReadOnlyList<string> All { get; } = [Plain, Layout];
}
