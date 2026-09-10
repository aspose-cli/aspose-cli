namespace Aspose.Cli.Product.Cells.Addressing;

/// <summary>
/// A parsed range specification as written by the user, e.g.
/// <c>Sales!A1:C10</c>. The sheet part is optional; commands combine it with
/// the <c>--sheet</c> option and reject contradictions.
/// </summary>
/// <param name="SheetName">Sheet name when the spec was sheet-qualified, otherwise null.</param>
/// <param name="Range">The rectangular range.</param>
internal sealed record RangeSpec(string? SheetName, RangeRef Range);
