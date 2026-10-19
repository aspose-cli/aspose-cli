using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Host.LocalServices;

/// <summary>Restores an exact structured error emitted by a supervised child process.</summary>
internal static class LocalServiceChildError
{
    internal const int MaximumEnvelopeCharacters = 64 * 1024;

    public static CliException? TryRead(string standardError, int exitCode) =>
        exitCode is <= 0 or > (int)ExitCode.OperationTimeout
            || string.IsNullOrWhiteSpace(standardError)
            || standardError.Length > MaximumEnvelopeCharacters
            ? null
            : WireError.FromEnvelope(standardError, exitCode)?.ToException();
}
