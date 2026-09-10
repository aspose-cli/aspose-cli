using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Serialization;

namespace Aspose.Cli.Host.Output;

/// <summary>Writes results and errors as JSON contract envelopes.</summary>
internal sealed class JsonOutputWriter : IOutputWriter
{
    private readonly TextWriter? _output;
    private readonly TextWriter? _error;
    private readonly ContractJsonSerializer _serializer;

    public JsonOutputWriter(
        ContractJsonSerializer serializer,
        TextWriter? output = null,
        TextWriter? error = null)
    {
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _output = output;
        _error = error;
    }

    public void WriteResult(ResultEnvelope result) =>
        (_output ?? Console.Out).WriteLine(
            _serializer.Serialize(result));

    public void WriteError(ErrorEnvelope error) =>
        (_error ?? Console.Error).WriteLine(
            _serializer.Serialize(error));
}
