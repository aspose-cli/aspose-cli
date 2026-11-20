namespace Aspose.Cli.TestKit.Scenarios;

/// <summary>
/// The process-wide workspace that reads the CLI's own contract (the schema catalog and its
/// documents) for the scenario runner. It runs in evaluation mode and is removed when the test
/// process exits. Test discovery reads the catalog, so a run without a CLI fails here with one message.
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

    /// <summary>What <c>aspose-cli schema &lt;id&gt;</c> prints; the catalog for an empty id.</summary>
    public static string Schema(string id)
    {
        CliResult result = id.Length == 0 ? Workspace.Value.Run("schema") : Workspace.Value.Run("schema", id);
        return result.ExitCode == 0
            ? result.StdOut
            : throw new InvalidOperationException($"The CLI schema '{id}' could not be read: {result.StdErr}");
    }
}
