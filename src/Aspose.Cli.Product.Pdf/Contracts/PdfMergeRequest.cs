using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>Options for merging PDF inputs.</summary>
public sealed record PdfMergeRequest
{
    public required IReadOnlyList<string> InputPaths { get; init; }
    /// <summary>The resolved output: its format, path and overwrite permission.</summary>
    public required ResolvedOutput Output { get; init; }
    public bool PreserveBookmarks { get; init; } = true;
    public Secret? Password { get; init; }

    /// <summary>The password the output opens with (<c>--encrypt</c>), or null to write it without one.</summary>
    public Secret? EncryptPassword { get; init; }
}
