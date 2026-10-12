using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Pdf.Contracts;

public sealed record PdfEditRequest
{
    /// <summary>The PDF to edit.</summary>
    public required string Input { get; init; }

    /// <summary>The validated operation batch.</summary>
    public required PdfOpsBatch Batch { get; init; }

    /// <summary>The resolved output: its path, overwrite permission and in-place backup.</summary>
    public required ResolvedOutput Output { get; init; }
    public EditCommandOptions Options { get; init; } = new();
    public Secret? Password { get; init; }

    /// <summary>Whether to read the staged output back against the effect of each operation.</summary>
    public bool Verify { get; init; }

    /// <summary>The operations' secrets by the environment variable their <c>*Env</c> fields name.</summary>
    public IReadOnlyDictionary<string, Secret>? OpSecrets { get; init; }

    /// <summary>
    /// The password the output opens with (<c>--encrypt</c>), applied after the operations and
    /// replacing any encryption they leave; null keeps what they leave.
    /// </summary>
    public Secret? EncryptPassword { get; init; }
}
