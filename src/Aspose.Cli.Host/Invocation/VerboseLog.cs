using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Host.Invocation;

/// <summary>
/// Structured diagnostics for <c>--verbose</c>: one JSON object per line on
/// stderr, leaving stdout (the result contract) untouched. Opt-in and
/// machine-readable, so an agent can time and triage a run without parsing prose.
/// </summary>
internal static class VerboseLog
{
    /// <summary>Emits a <c>completed</c> event with the elapsed wall-clock time.</summary>
    public static void Completed(GlobalValues globals, long elapsedMs) =>
        Write(globals, new JsonObject { ["event"] = "completed", ["ms"] = elapsedMs });

    /// <summary>
    /// Emits a <c>failed</c> event with the error code and exit code. Internal
    /// failures expose only their stable diagnostic id; exception messages and
    /// stack traces belong in the bounded private process log.
    /// </summary>
    public static void Failed(
        GlobalValues globals,
        long elapsedMs,
        CliException error)
    {
        var fields = new JsonObject
        {
            ["event"] = "failed",
            ["ms"] = elapsedMs,
            ["code"] = error.Code.Name,
            ["exit"] = (int)error.ExitCode,
        };
        if (error.Details?["diagnosticId"] is JsonValue diagnosticId)
        {
            fields["diagnosticId"] = diagnosticId.GetValue<string>();
        }

        Write(globals, fields);
    }

    private static void Write(GlobalValues globals, JsonObject fields)
    {
        if (globals.Verbose)
        {
            Console.Error.WriteLine(fields.ToJsonString());
        }
    }
}
