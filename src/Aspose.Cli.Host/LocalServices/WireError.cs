using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Serialization;

namespace Aspose.Cli.Host.LocalServices;

/// <summary>
/// An error on its way between the Host's own processes, whole: the fields of the error payload
/// and the exit code the failure would produce on the command line. Every Host protocol carries
/// a failure as this record, so an error field added once crosses every process boundary.
/// </summary>
internal sealed record WireError(
    string Code,
    int Exit,
    string Message,
    string? Hint = null,
    JsonObject? Details = null,
    string? Docs = null)
{
    public static WireError From(CliException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return new(
            exception.Code.Name,
            (int)exception.ExitCode,
            exception.Message,
            exception.Hint,
            exception.Details,
            exception.Docs);
    }

    /// <summary>
    /// Reads the error envelope a failed child wrote to its standard error, given the exit code
    /// it ended with; null when the text is not a well-formed envelope of this contract.
    /// </summary>
    public static WireError? FromEnvelope(string text, int exitCode)
    {
        try
        {
            return FromEnvelope(JsonNode.Parse(text), exitCode);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <inheritdoc cref="FromEnvelope(string, int)"/>
    public static WireError? FromEnvelope(ReadOnlySpan<byte> utf8, int exitCode)
    {
        try
        {
            return FromEnvelope(JsonNode.Parse(utf8), exitCode);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public CliException ToException() =>
        CliErrors.FromRemote(Code, (ExitCode)Exit, Message, Hint, Details, Docs);

    private static WireError? FromEnvelope(JsonNode? node, int exitCode)
    {
        try
        {
            if (node is not JsonObject root
                || root["schema"]?.GetValue<string>() != ErrorEnvelope.SchemaUri
                || root["schemaVersion"]?.GetValue<int>() != 2)
            {
                return null;
            }

            ErrorPayload? error = root.Deserialize(SdkJsonContext.Default.ErrorEnvelope)?.Error;
            return error is null
                || string.IsNullOrWhiteSpace(error.Code)
                || string.IsNullOrWhiteSpace(error.Message)
                ? null
                : new(error.Code, exitCode, error.Message, error.Hint, error.Details, error.Docs);
        }
        catch (Exception exception) when (
            exception is JsonException
                or InvalidOperationException
                or FormatException)
        {
            return null;
        }
    }
}
