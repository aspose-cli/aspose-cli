namespace Aspose.Cli.Host.Output;

/// <summary>How command results are rendered.</summary>
internal enum OutputMode
{
    /// <summary>Structured JSON envelopes (the contract agents parse).</summary>
    Json,

    /// <summary>The same JSON envelopes, each written on one line.</summary>
    Compact,
}

/// <summary>Creates the writer for a resolved output mode.</summary>
internal static class OutputWriterFactory
{
    public static IOutputWriter Create(
        OutputMode mode,
        Aspose.Cli.Sdk.Serialization.ContractJsonSerializer serializer,
        TextWriter? output = null,
        TextWriter? error = null)
    {
        ArgumentNullException.ThrowIfNull(serializer);
        return new JsonOutputWriter(serializer, compact: mode == OutputMode.Compact, output, error);
    }
}