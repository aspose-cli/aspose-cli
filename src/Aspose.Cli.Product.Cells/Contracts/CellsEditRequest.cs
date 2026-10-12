using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>Options of <c>cells edit</c>.</summary>
public sealed record CellsEditRequest
{
    /// <summary>The workbook to edit.</summary>
    public required string Input { get; init; }

    /// <summary>The validated operation batch to apply atomically.</summary>
    public required CellsOpsBatch Batch { get; init; }

    /// <summary>Transient environment secret values; never part of ops JSON or result envelopes.</summary>
    public IReadOnlyDictionary<string, Secret>? OpSecrets { get; init; }

    /// <summary>The resolved output: its format, path, overwrite permission and in-place backup.</summary>
    public required ResolvedOutput Output { get; init; }

    /// <summary>Shared stale-input, dry-run, and best-effort semantics.</summary>
    public EditCommandOptions Options { get; init; } = new();

    /// <summary>
    /// Verify the staged workbook and publish its render evidence with the edit. The edit
    /// command admits it only for a published, recalculated edit.
    /// </summary>
    public bool Verify { get; init; }

    /// <summary>Recalculate formulas after applying the ops.</summary>
    public bool Recalculate { get; init; } = true;

    /// <summary>Password for opening an encrypted input file.</summary>
    public Secret? Password { get; init; }

    /// <summary>New output password; null preserves source encryption where the target format supports it.</summary>
    public Secret? EncryptPassword { get; init; }
}
