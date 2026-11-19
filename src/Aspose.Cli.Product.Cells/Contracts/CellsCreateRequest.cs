using Aspose.Cli.Sdk.IO.Output;

namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>Options of <c>cells create</c>.</summary>
public sealed record CellsCreateRequest
{
    /// <summary>The resolved output: its format, path and overwrite permission.</summary>
    public required ResolvedOutput Output { get; init; }

    /// <summary>The workbook saved as the new one (<c>--template</c>), or null for the engine's new workbook.</summary>
    public SourceFile? Template { get; init; }

    /// <summary>The password the output opens with, or null.</summary>
    public Secret? EncryptPassword { get; init; }
}
