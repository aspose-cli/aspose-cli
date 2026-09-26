using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Serialization;

namespace Aspose.Cli.Host.Output;

/// <summary>Writes results and errors as JSON contract envelopes, indented or on one line.</summary>
internal sealed class JsonOutputWriter : IOutputWriter
{
    private readonly TextWriter? _output;
    private readonly TextWriter? _error;
    private readonly ContractJsonSerializer _serializer;
    private readonly bool _compact;

    public JsonOutputWriter(
        ContractJsonSerializer serializer,
        bool compact = false,
        TextWriter? output = null,
        TextWriter? error = null)
    {
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _compact = compact;
        _output = output;
        _error = error;
    }

    public void WriteResult(ResultEnvelope result) =>
        (_output ?? Console.Out).WriteLine(Serialize(result));

    public void WriteError(ErrorEnvelope error) =>
        (_error ?? Console.Error).WriteLine(Serialize(error));

    private string Serialize(object value) =>
        _compact ? _serializer.SerializeCompact(value) : _serializer.Serialize(value);
}
