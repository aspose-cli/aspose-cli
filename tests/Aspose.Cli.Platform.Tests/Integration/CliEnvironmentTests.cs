using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>Guards deterministic child-process environment isolation.</summary>
public sealed class CliEnvironmentTests
{
    [Fact]
    public void Evaluation_RemovesInheritedAndCallerProvidedLicenseSources()
    {
        var variables = new Dictionary<string, string?>
        {
            ["ASPOSE_CELLS_LICENSE_B64"] = "caller-secret",
            ["ASPOSE_CLI_CONFIG_DIR"] = "caller-config",
            ["TEST_MARKER"] = "kept",
        };
        var environment = new Dictionary<string, string?>
        {
            ["ASPOSE_PDF_LICENSE_PATH"] = "inherited.lic",
        };

        CliEnvironment.Evaluation("test-config", variables).Apply(environment);

        Assert.DoesNotContain("ASPOSE_PDF_LICENSE_PATH", environment.Keys);
        Assert.DoesNotContain("ASPOSE_CELLS_LICENSE_B64", environment.Keys);
        Assert.Equal(
            Path.Combine("test-config", "aspose-cli"),
            environment["ASPOSE_CLI_CONFIG_DIR"]);
        Assert.Equal("kept", environment["TEST_MARKER"]);
    }
}
