using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

public sealed class ConfigurationOwnershipTests
{
    [Theory]
    [InlineData("{\"schemaVersion\":1,\"productId\":\"another-cli\"}")]
    [InlineData("{\"schemaVersion\":\"1\",\"productId\":\"another-cli\"}")]
    [InlineData("{unfinished")]
    [InlineData("[]")]
    public void DocumentCommand_RejectsForeignOrMalformedConfigurationWithoutWriting(string marker)
    {
        using var workspace = new TempWorkspace();
        string config = workspace.ConfigDirectory;
        Directory.CreateDirectory(config);
        string ownership = Path.Combine(config, ".aspose-cli-config.json");
        File.WriteAllText(ownership, marker);
        string output = workspace.File("rejected.xlsx");

        CliResult result = workspace.Run(
            "cells", "create", output, "--output", "json");

        Assert.True(result.ExitCode == 2, result.StdErr);
        using JsonDocument error = JsonDocument.Parse(result.StdErr);
        Assert.Equal("OPTION_INVALID", error.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.False(File.Exists(output));
        Assert.Equal(marker, File.ReadAllText(ownership));
        Assert.Equal(new[] { ownership }, Directory.GetFiles(config, "*", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [SupportedOSPlatform("windows")]
    public void DocumentCommand_AcceptsConfigurationWhoseAclGrantsAnotherPrincipal(bool marked)
    {
        Requires.Windows();
        using var workspace = new TempWorkspace();
        DirectoryInfo config = Directory.CreateDirectory(workspace.ConfigDirectory);
        DirectorySecurity acl = config.GetAccessControl();
        acl.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
            FileSystemRights.ReadAndExecute,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None, AccessControlType.Allow));
        config.SetAccessControl(acl);
        string ownership = Path.Combine(config.FullName, ".aspose-cli-config.json");
        if (marked)
        {
            File.WriteAllText(ownership,
                "{\"schemaVersion\":1,\"productId\":\"" + Aspose.Cli.Sdk.DistributionInfo.Id + "\"}");
        }
        string output = workspace.File("accepted.xlsx");

        CliResult result = workspace.Run("cells", "create", output, "--output", "json");

        Assert.True(result.ExitCode == 0, result.StdErr);
        Assert.True(File.Exists(output));
    }
}
