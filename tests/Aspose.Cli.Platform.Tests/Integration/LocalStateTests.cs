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

    /// <summary>
    /// The per-user state is used by the path the file system reaches it by: a local application
    /// data folder whose path holds a junction, as one an MSIX-packaged host redirects resolves
    /// elsewhere, still runs the commands whose worker reads back what it wrote there.
    /// </summary>
    [Fact]
    public void WorkerCommands_RunWhenTheLocalApplicationDataFolderResolvesElsewhere()
    {
        Requires.Windows();
        using var workspace = new TempWorkspace();
        string target = Directory.CreateDirectory(workspace.File("target")).FullName;
        string linked = workspace.File("linked");
        FileSystemLinks.CreateDirectoryLink(linked, target);
        string local = Directory.CreateDirectory(Path.Combine(linked, "local")).FullName;
        var environment = new Dictionary<string, string?> { ["LOCALAPPDATA"] = local };
        Assert.Equal(0, workspace.Run("cells", "create", "book.xlsx", "--output-format", "json").ExitCode);

        CliResult review = workspace.RunWithEnv(environment,
            "review", "book.xlsx", "--out", "evidence", "--timeout", TestDeadline.CliTimeout, "--output-format", "json");
        CliResult convert = workspace.RunWithEnv(environment,
            "cells", "convert", "book.xlsx", "--to", "csv", "--out", "book.csv", "--timeout", TestDeadline.CliTimeout, "--output-format", "json");

        Assert.True(review.ExitCode == 0, review.StdErr);
        Assert.True(File.Exists(workspace.File(Path.Combine("evidence", "review.json"))));
        Assert.True(convert.ExitCode == 0, convert.StdErr);
        Assert.True(File.Exists(workspace.File("book.csv")));
        Assert.True(Directory.Exists(Path.Combine(target, "local", DistributionInfo.Id, "worker")));
    }
}
