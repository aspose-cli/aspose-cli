using System.Runtime.CompilerServices;
using Xunit;

namespace Aspose.Cli.Platform.Tests;

/// <summary>Commercial-license scenarios run when an explicit test license is supplied.</summary>
public sealed class LicensedFactAttribute : FactAttribute
{
    public LicensedFactAttribute(
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPOSE_CLI_TEST_LICENSE_PATH")))
        {
            Skip = "Set ASPOSE_CLI_TEST_LICENSE_PATH to exercise a real commercial license.";
        }
    }
}
