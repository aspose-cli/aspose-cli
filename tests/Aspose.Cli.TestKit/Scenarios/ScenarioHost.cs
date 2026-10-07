using System.Text.Json.Nodes;

namespace Aspose.Cli.TestKit.Scenarios;

/// <summary>
/// The process-wide workspace that reads the CLI's own contract (capabilities and schemas) for
/// the scenario runner. It runs in evaluation mode and is removed when the test process exits.
/// Test discovery reads the capabilities, so a run without a CLI fails here with one message.
/// </summary>
internal static class ScenarioHost
{
    private static readonly Lazy<TempWorkspace> Workspace = new(static () =>
    {
        RequireExecutable();
        var workspace = new TempWorkspace();
        AppDomain.CurrentDomain.ProcessExit += (_, _) => workspace.Dispose();
        return workspace;
    });

    public static JsonNode Capabilities()
    {
        CliResult result = Workspace.Value.Run("capabilities", "--output", "json");
        return result.ExitCode == 0
            ? JsonNode.Parse(result.StdOut)!
            : throw new InvalidOperationException($"The CLI capabilities could not be read: {result.StdErr}");
    }

    private static void RequireExecutable()
    {
        try
        {
            _ = CliRunner.ExecutablePath;
        }
        catch (Exception exception) when (exception is InvalidOperationException or FileNotFoundException)
        {
            string reason = exception is FileNotFoundException { FileName: { } file } ? $"{exception.Message} {file}" : exception.Message;
            throw new InvalidOperationException(
                $"The scenario and invariant tests generate their cases from the built CLI, and there is none to run ({reason}). "
                + $"Build src/Aspose.Cli in this configuration, or set {CliRunner.ExecutableEnvironmentVariable} to a built aspose-cli.exe.",
                exception);
        }
    }

    public static string Schema(string id)
    {
        CliResult result = Workspace.Value.Run("schema", id);
        return result.ExitCode == 0
            ? result.StdOut
            : throw new InvalidOperationException($"The CLI schema '{id}' could not be read: {result.StdErr}");
    }
}
