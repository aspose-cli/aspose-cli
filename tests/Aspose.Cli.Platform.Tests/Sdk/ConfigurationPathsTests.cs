using Aspose.Cli.Sdk.Configuration;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Sdk;

/// <summary>
/// The CLI writes into a configuration directory only when it is its own. One chosen through the
/// environment that already holds someone else's files and no marker is refused: the CLI writes
/// no marker into it and leaves its files as they were.
/// </summary>
[Collection(ProcessLicenseEnvironmentCollection.Name)]
public sealed class ConfigurationPathsTests
{
    [Fact]
    public void SelectedConfigurationDirectoryWithForeignFiles_IsRefusedAndLeftUntouched()
    {
        using var root = new TempDirectory();
        string selected = Directory.CreateDirectory(root.File("config")).FullName;
        string foreign = Path.Combine(selected, "notes.txt");
        File.WriteAllText(foreign, "user data");
        string? saved = Environment.GetEnvironmentVariable(ConfigurationPaths.EnvironmentVariableName);
        CliException refused;
        try
        {
            Environment.SetEnvironmentVariable(ConfigurationPaths.EnvironmentVariableName, selected);
            refused = Assert.Throws<CliException>(ConfigurationPaths.EnsureUserDirectory);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ConfigurationPaths.EnvironmentVariableName, saved);
        }

        Assert.Equal(ErrorCodes.OptionInvalid, refused.Code);
        Assert.Equal(2, (int)refused.ExitCode);
        Assert.Equal("user data", File.ReadAllText(foreign));
        Assert.False(File.Exists(Path.Combine(selected, ".aspose-cli-config.json")));
        Assert.Equal([foreign], Directory.GetFileSystemEntries(selected));
    }
}
