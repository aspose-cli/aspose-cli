using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Serialization;

namespace Aspose.Cli.Host.LocalServices;

/// <summary>Restores an exact structured error emitted by a supervised child process.</summary>
internal static class LocalServiceChildError
{
    internal const int MaximumEnvelopeCharacters = 64 * 1024;

    public static CliException? TryRead(string standardError, int exitCode)
    {
        if (exitCode is <= 0 or > (int)ExitCode.OperationTimeout
            || string.IsNullOrWhiteSpace(standardError)
            || standardError.Length > MaximumEnvelopeCharacters)
        {
            return null;
        }

        try
        {
            JsonObject? root = JsonNode.Parse(standardError) as JsonObject;
            if (root?["schema"]?.GetValue<string>() != CommonSchemaIds.Error
                || root["schemaVersion"]?.GetValue<int>() != 2)
            {
                return null;
            }

            ErrorEnvelope? envelope = root.Deserialize(
                SdkJsonContext.Default.ErrorEnvelope);
            ErrorPayload? error = envelope?.Error;
            if (error is null
                || string.IsNullOrWhiteSpace(error.Code)
                || string.IsNullOrWhiteSpace(error.Message))
            {
                return null;
            }

            return CliErrors.FromRemote(
                error.Code,
                (ExitCode)exitCode,
                error.Message,
                error.Hint,
                error.Details,
                error.Docs);
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
