namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>
/// The names of the Cells options that the engine names in its errors, declared once for the
/// commands that define the options and the engine that reports on their values.
/// </summary>
public static class CellsOptionNames
{
    /// <summary>The sheet selection, which the <c>sheet</c> range option is named after.</summary>
    public const string Sheets = "--sheets";

    /// <summary>The text encoding of a CSV or TSV input.</summary>
    public const string Encoding = "--encoding";

    /// <summary>The culture of a CSV or TSV input's numbers and dates.</summary>
    public const string Culture = "--culture";
}
