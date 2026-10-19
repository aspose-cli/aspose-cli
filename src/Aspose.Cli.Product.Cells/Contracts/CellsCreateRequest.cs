using Aspose.Cli.Sdk.IO.Output;

namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>Options of <c>cells create</c>.</summary>
public sealed record CellsCreateRequest
{
    /// <summary>The resolved output: its format, path and overwrite permission.</summary>
    public required ResolvedOutput Output { get; init; }

    /// <summary>Sheet names, in order; already validated non-empty and unique.</summary>
    public required IReadOnlyList<string> SheetNames { get; init; }

    /// <summary>Resolved password to protect the output file; null leaves it unencrypted.</summary>
    public Secret? EncryptPassword { get; init; }
}
