namespace Aspose.Cli.Sdk.Extensibility.Output;

/// <summary>
/// Where a table renderer writes and how its grids are formatted. Bundling the
/// two keeps every renderer's signature small and gives future rendering options
/// a single place to live.
/// </summary>
public readonly struct TableSurface(TextWriter output, TableFormat format)
{
    /// <summary>The destination writer (stdout in normal use).</summary>
    public TextWriter Out { get; } = output;

    /// <summary>Plain or Markdown grids.</summary>
    public TableFormat Format { get; } = format;
}
