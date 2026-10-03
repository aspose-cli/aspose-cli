namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>Post-edit evidence produced by <c>cells edit --verify</c>.</summary>
public sealed record EditVerification
{
    /// <summary><c>true</c> when diffing and formula checks completed without a verification issue.</summary>
    public required bool Ok { get; init; }

    /// <summary>Targets requested by the operations in this invocation.</summary>
    public required IReadOnlyList<VerificationTarget> RequestedTargets { get; init; }

    /// <summary>Changes inside a requested operation target.</summary>
    public required IReadOnlyList<VerifiedCellChange> DirectChanges { get; init; }

    /// <summary>Cells whose formula text stayed the same while the calculated result changed.</summary>
    public required IReadOnlyList<VerifiedCellChange> FormulaResultChanges { get; init; }

    /// <summary>Changes that could not be reliably attributed to a requested target or formula recalculation.</summary>
    public required IReadOnlyList<VerificationOtherChange> OtherChanges { get; init; }

    /// <summary>Formula errors found anywhere in the edited workbook.</summary>
    public required IReadOnlyList<CellError> FormulaErrors { get; init; }

    /// <summary>
    /// <c>true</c> when the edit changed more than 1000 cells, so the change lists hold only the
    /// first 1000; the checks behind <see cref="Ok"/> still cover every cell.
    /// </summary>
    public required bool Truncated { get; init; }

    /// <summary>Verification problems, empty when <see cref="Ok"/>; an edit artifact is preserved when this list is non-empty.</summary>
    public required IReadOnlyList<VerificationIssue> Issues { get; init; }
}

/// <summary>An operation target included in post-edit verification.</summary>
public sealed record VerificationTarget
{
    /// <summary>Worksheet name; omitted when the operation addressed the active sheet implicitly.</summary>
    public string? Sheet { get; init; }

    /// <summary>A1 range; omitted for a whole-sheet target.</summary>
    public string? Range { get; init; }
}

/// <summary>Before-and-after values for one verified cell.</summary>
public sealed record VerifiedCellChange
{
    /// <summary>Worksheet name.</summary>
    public required string Sheet { get; init; }

    /// <summary>A1 cell address.</summary>
    public required string Cell { get; init; }

    /// <summary>Cell before the edit; null when it was empty.</summary>
    public CellSide? Left { get; init; }

    /// <summary>Cell after the edit; null when it is empty.</summary>
    public CellSide? Right { get; init; }
}

/// <summary>A changed cell or worksheet that could not be classified as a direct or formula-result change.</summary>
public sealed record VerificationOtherChange
{
    /// <summary>Worksheet name.</summary>
    public required string Sheet { get; init; }

    /// <summary>Sheet status: <c>added</c>, <c>removed</c>, or <c>modified</c>.</summary>
    public required string Status { get; init; }

    /// <summary>A1 cell address for a cell-level change.</summary>
    public string? Cell { get; init; }

    /// <summary>Cell before the edit; null when absent or sheet-level.</summary>
    public CellSide? Left { get; init; }

    /// <summary>Cell after the edit; null when absent or sheet-level.</summary>
    public CellSide? Right { get; init; }
}
