using Aspose.Cli.Sdk.Operations;

namespace Aspose.Cli.Product.Cells.Contracts;

// Ops that bring cells or sheets in from another workbook file.

/// <summary>
/// Copies a range from another workbook file into this one at an anchor cell. The source is any
/// file the Cells loader opens, including CSV and TSV, and is only read; it may be the edited file
/// itself, read as it is on disk before the edit. A reference in an imported formula to another
/// sheet of the source points at the sheet of that name in this workbook, or becomes #REF! when
/// this workbook has none.
/// </summary>
[Operation("import_range")]
public sealed record ImportRangeOp : CellsOp
{
    /// <summary>The source workbook file, relative to the working directory.</summary>
    [InputPath, Pattern(@"\S")] public required string Path { get; init; }

    /// <summary>The source range; it may name a source sheet, such as Sheet1!A1:D20, and otherwise lies on the source's first sheet.</summary>
    [A1Reference] public required string From { get; init; }

    /// <summary>The destination anchor cell; it may name another sheet, such as Summary!A1.</summary>
    [A1Reference] public required string To { get; init; }

    /// <summary>
    /// What is imported: values writes the cell values, with formulas replaced by their results,
    /// and the number formats; all also keeps the formulas as written and the formatting, as
    /// copy_range does.
    /// </summary>
    [AllowedValues(typeof(ImportContents))] public string Content { get; init; } = ImportContents.Values;

    /// <summary>The environment variable that holds the source's open password, for an encrypted source.</summary>
    [SecretEnv] public string? PasswordEnv { get; init; }

    /// <inheritdoc />
    protected override BoundedOperation Validated()
    {
        OperationInvalidException.Require(A1.ParseRange(To).Range.CellCount == 1, "to must be a single anchor cell, e.g. \"Summary!A1\"");
        return this;
    }
}

/// <summary>Accepted values of <see cref="ImportRangeOp.Content"/>.</summary>
public static class ImportContents
{
    public const string Values = "values";
    public const string Everything = "all";
}

/// <summary>
/// Copies a whole sheet from another workbook file into this one as a new sheet, with its values,
/// formulas, formatting, merged cells, column widths, tables, charts, pivot tables, comments,
/// hyperlinks, validation and conditional formatting. The source is only read and may be the
/// edited file itself, read as it is on disk before the edit. A reference to another sheet of the
/// source points at the sheet of that name in this workbook, or becomes #REF! when this workbook
/// has none, and the defined names the sheet refers to come along. A source that defines a
/// workbook-level name this workbook also defines differently is refused unless the name refers
/// only to the imported sheet.
/// </summary>
[Operation("import_sheet")]
public sealed record ImportSheetOp : CellsOp
{
    /// <summary>The source sheet to copy, in the source workbook; the source's first sheet when omitted.</summary>
    public override string? Sheet { get; init; }

    /// <summary>The source workbook file, relative to the working directory.</summary>
    [InputPath, Pattern(@"\S")] public required string Path { get; init; }

    /// <summary>The name of the new sheet; the source sheet's name when omitted. A name this workbook already uses is refused.</summary>
    [Pattern(@"\S")] public string? Name { get; init; }

    /// <summary>The zero-based position of the new sheet; appended when omitted.</summary>
    [Minimum(0)] public int? Position { get; init; }

    /// <summary>The environment variable that holds the source's open password, for an encrypted source.</summary>
    [SecretEnv] public string? PasswordEnv { get; init; }
}
