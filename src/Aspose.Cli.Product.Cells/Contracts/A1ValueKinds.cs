using Aspose.Cli.Sdk.Operations;

namespace Aspose.Cli.Product.Cells.Contracts;

// The A1 value kinds of Cells operations: the schema states each kind's pattern, and the A1
// parser the engine reads the value with checks the rest and gives the reason for a rejection.

/// <summary>A cell or range on the operation's sheet.</summary>
[AttributeUsage(AttributeTargets.Property)]
internal sealed class A1RangeAttribute() : ValueKindAttribute(
    "must be an A1 cell or range on the operation's sheet, such as B2 or B2:D10",
    "^[^!]+$",
    "range",
    "An A1 cell or range on the operation's sheet, such as B2 or B2:D10; name another sheet with the operation's sheet field. Whole rows and columns are not ranges.")
{
    protected override void Parse(string value) => A1.ParseRange(value);
}

/// <summary>One cell on the operation's sheet.</summary>
[AttributeUsage(AttributeTargets.Property)]
internal sealed class A1CellAttribute() : ValueKindAttribute(
    "must be one A1 cell on the operation's sheet, such as B2",
    "^[^!:]+$",
    "cell",
    "One A1 cell on the operation's sheet, such as B2; name another sheet with the operation's sheet field.")
{
    protected override void Parse(string value) => A1.ParseCell(value);
}

/// <summary>A cell or range that may name another sheet.</summary>
[AttributeUsage(AttributeTargets.Property)]
internal sealed class A1ReferenceAttribute() : ValueKindAttribute(
    "must be an A1 cell or range, optionally sheet-qualified, such as A1:C10 or Data!A1:C10",
    @"\S",
    "reference",
    "An A1 cell or range on the operation's sheet, such as A1:C10, or on another sheet, such as Data!A1:C10 or 'My Sheet'!A1:C10.")
{
    protected override void Parse(string value) => A1.ParseRange(value);
}

/// <summary>Column letters.</summary>
[AttributeUsage(AttributeTargets.Property)]
internal sealed class A1ColumnAttribute() : ValueKindAttribute(
    "must be column letters from A to XFD, such as B or AA",
    "^[A-Za-z]{1,3}$",
    "column",
    "Column letters from A to XFD, such as B or AA.")
{
    protected override void Parse(string value) => A1.ParseColumn(value);
}

/// <summary>A band of whole rows, the rows a printout repeats.</summary>
[AttributeUsage(AttributeTargets.Property)]
internal sealed class A1RowBandAttribute() : ValueKindAttribute(
    "must be a row or rows such as 1 or 1:2",
    @"^\$?[0-9]+(:\$?[0-9]+)?$")
{
    protected override void Parse(string value) => A1.ParseRowBand(value);
}

/// <summary>A band of whole columns, the columns a printout repeats.</summary>
[AttributeUsage(AttributeTargets.Property)]
internal sealed class A1ColumnBandAttribute() : ValueKindAttribute(
    "must be a column or columns such as A or A:B",
    @"^\$?[A-Za-z]{1,3}(:\$?[A-Za-z]{1,3})?$")
{
    protected override void Parse(string value) => A1.ParseColumnBand(value);
}
