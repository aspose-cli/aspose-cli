using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Serialization;

namespace Aspose.Cli.Host.Output;

/// <summary>
/// Writes results and errors as JSON contract envelopes, indented or on one line. A result goes
/// to stdout and an error to stderr, never the other way around, so pipelines stay parseable.
/// </summary>
internal sealed class JsonOutputWriter
{
    private readonly TextWriter? _error;
    private readonly ContractJsonSerializer _serializer;
    private readonly bool _compact;

    /// <param name="serializer">The serializer of the contract envelopes.</param>
    /// <param name="mode">How the envelopes print.</param>
    /// <param name="error">Where an error is written instead of stderr.</param>
    public JsonOutputWriter(
        ContractJsonSerializer serializer,
        OutputMode mode,
        TextWriter? error = null)
    {
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _compact = mode == OutputMode.Compact;
        _error = error;
    }

    /// <summary>Writes a successful result to stdout.</summary>
    public void WriteResult(ResultEnvelope result) =>
        Console.Out.WriteLine(Serialize(result));

    /// <summary>Writes an error to stderr.</summary>
    public void WriteError(ErrorEnvelope error) =>
        (_error ?? Console.Error).WriteLine(Serialize(error));

    private string Serialize(object value) =>
        _compact ? _serializer.SerializeCompact(value) : _serializer.Serialize(value);
}
