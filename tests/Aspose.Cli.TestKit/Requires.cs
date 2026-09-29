using Xunit;

namespace Aspose.Cli.TestKit;

/// <summary>
/// Preconditions a machine may not meet. An unmet one skips the test with its reason,
/// which scripts/test.ps1 reports, instead of letting the test pass without asserting.
/// </summary>
public static class Requires
{
    public static void Windows() =>
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Requires Windows.");

    public static void ElevatedWindows()
    {
        Windows();
        Assert.SkipUnless(
            OperatingSystem.IsWindows()
                && new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent())
                    .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator),
            "Requires an elevated Windows process.");
    }

    public static void Unix() =>
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Requires a Unix platform.");
}
