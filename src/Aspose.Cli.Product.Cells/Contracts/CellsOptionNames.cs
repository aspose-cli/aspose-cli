namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>The names of the Cells options that the engine side names in its errors.</summary>
public static class CellsOptionNames
{
    /// <summary>The sheet a convert or render writes, by its name.</summary>
    public const string Sheet = "--sheet";

    /// <summary>The area of one sheet a render draws.</summary>
    public const string Area = "--area";

    /// <summary>The separator of a CSV input or of a CSV or TSV output.</summary>
    public const string Separator = "--separator";

    /// <summary>The text encoding of a CSV or TSV input or output.</summary>
    public const string Encoding = "--encoding";

    /// <summary>Whether a CSV or TSV input's numbers become numbers.</summary>
    public const string ConvertNumbers = "--convert-numbers";

    /// <summary>Whether a CSV or TSV input's dates become dates.</summary>
    public const string ConvertDates = "--convert-dates";

    /// <summary>The culture a CSV or TSV input's numbers and dates are read in.</summary>
    public const string Culture = "--culture";

    /// <summary>How a CSV, TSV or Markdown output writes cell values.</summary>
    public const string FormatStrategy = "--format-strategy";
}
