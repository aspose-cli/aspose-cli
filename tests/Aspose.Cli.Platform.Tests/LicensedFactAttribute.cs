using Xunit;

namespace Aspose.Cli.Platform.Tests;

/// <summary>Commercial-license scenarios run when an explicit test license is supplied.</summary>
public sealed class LicensedFactAttribute : FactAttribute
{
    public LicensedFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPOSE_CLI_TEST_LICENSE_PATH")))
        {
            Skip = "Set ASPOSE_CLI_TEST_LICENSE_PATH to exercise a real commercial license.";
        }
    }
}
