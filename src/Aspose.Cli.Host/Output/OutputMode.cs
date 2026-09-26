namespace Aspose.Cli.Host.Output;

/// <summary>How command results are rendered.</summary>
internal enum OutputMode
{
    /// <summary>Structured JSON envelopes (the contract agents parse).</summary>
    Json,

    /// <summary>The same JSON envelopes, each written on one line.</summary>
    Compact,

    /// <summary>Compact human-readable text.</summary>
    Table,

    /// <summary>Like table, but grids render as Markdown pipe tables.</summary>
    Markdown,
}

/// <summary>Creates the writer for a resolved output mode.</summary>
internal static class OutputWriterFactory
{
    public static IOutputWriter Create(
        OutputMode mode,
        bool quiet,
        Aspose.Cli.Sdk.Extensibility.ProductCatalog catalog,
        Aspose.Cli.Sdk.Serialization.ContractJsonSerializer serializer,
        TextWriter? output = null,
        TextWriter? error = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(serializer);
        return mode switch
        {
            OutputMode.Json => new JsonOutputWriter(serializer, compact: false, output, error),
            OutputMode.Compact => new JsonOutputWriter(serializer, compact: true, output, error),
            OutputMode.Markdown => new TableOutputWriter(
                catalog, serializer, quiet, markdown: true, output, error),
            _ => new TableOutputWriter(
                catalog, serializer, quiet, markdown: false, output, error),
        };
    }
}
