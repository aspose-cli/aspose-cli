namespace Aspose.Cli.Host.Output;

/// <summary>How command results are rendered.</summary>
internal enum OutputMode
{
    /// <summary>Structured JSON envelopes (the contract agents parse).</summary>
    Json,

    /// <summary>The same JSON envelopes, each written on one line.</summary>
    Compact,
}
