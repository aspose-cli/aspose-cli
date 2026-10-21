using Aspose.Cli.Sdk;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Integration;

/// <summary>
/// The CLI keeps its per-user state in the local application data folder, never in the
/// temporary directory, where another account sharing it could create the directory first.
/// </summary>
public sealed class LocalStateTests
{
    [Fact]
    public void AWorkerCommand_KeepsItsStateInTheLocalApplicationDataFolder()
    {
        Requires.Windows();
        using var workspace = new TempWorkspace();
        string temporary = Directory.CreateDirectory(workspace.File("temp")).FullName;
        string local = Directory.CreateDirectory(workspace.File("local")).FullName;

        // --timeout runs the command in a supervised worker, whose output root is per-user state.
        CliResult result = workspace.RunWithEnv(
            new Dictionary<string, string?> { ["TEMP"] = temporary, ["TMP"] = temporary, ["LOCALAPPDATA"] = local },
            "cells", "create", "book.xlsx", "--timeout", TestDeadline.CliTimeout, "--output-format", "json");

        Assert.True(result.ExitCode == 0, result.StdErr);
        Assert.True(File.Exists(workspace.File("book.xlsx")));
        Assert.Empty(Directory.EnumerateFileSystemEntries(temporary, DistributionInfo.Id + "*"));
        Assert.True(Directory.Exists(Path.Combine(local, DistributionInfo.Id, "worker")),
            $"No worker state in '{local}'; it holds: {string.Join(", ", Directory.EnumerateFileSystemEntries(local))}");
    }
}
