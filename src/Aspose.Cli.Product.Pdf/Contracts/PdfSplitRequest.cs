using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>Options for transactional PDF splitting.</summary>
public sealed record PdfSplitRequest
{
    /// <summary>The PDF to open.</summary>
    public required string Input { get; init; }
    /// <summary>How the PDF is divided: by pages, in runs or explicit groups, or at bookmarks.</summary>
    public required SplitPlan Plan { get; init; }
    /// <summary>The resolved directory that receives the files.</summary>
    public required ResolvedDirectory Output { get; init; }
    public Secret? Password { get; init; }

    /// <summary>The password every part opens with (<c>--encrypt</c>), or null to write them without one.</summary>
    public Secret? EncryptPassword { get; init; }
}
