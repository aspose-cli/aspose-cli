using System.Runtime.CompilerServices;
using Aspose.Cli.Sdk.Licensing;
using Xunit;

namespace Aspose.Cli.TestKit;

/// <summary>
/// The one license source of a test run. A run is licensed only when
/// <c>ASPOSE_CLI_TEST_LICENSE_PATH</c> names a license file; otherwise it runs in
/// evaluation mode, whatever licenses or configuration the developer's machine has.
/// </summary>
public static class TestLicense
{
    public const string PathVariable = "ASPOSE_CLI_TEST_LICENSE_PATH";

    /// <summary>The explicit test license, or null for an evaluation run.</summary>
    /// <exception cref="FileNotFoundException">The variable names a missing file.</exception>
    public static string? Path
    {
        get
        {
            string? configured = Environment.GetEnvironmentVariable(PathVariable);
            if (string.IsNullOrWhiteSpace(configured))
            {
                return null;
            }
            string full = System.IO.Path.GetFullPath(configured);
            return File.Exists(full)
                ? full
                : throw new FileNotFoundException($"{PathVariable} names a missing license file.", full);
        }
    }

    /// <summary>
    /// Skips a test that evaluation mode cannot run, naming <see cref="PathVariable"/> so the run
    /// reports it as a licensed case (the Full scope of scripts/test.ps1 fails when one is skipped).
    /// </summary>
    public static void Require(string evaluationLimit) =>
        Assert.SkipWhen(Path is null, $"{evaluationLimit} Set {PathVariable} to run it.");

    /// <summary>
    /// Creates a product license gate over the test license and applies it at once, so every
    /// document a test authors through the SDK is written in the state the engine later reads.
    /// </summary>
    public static TGate Apply<TGate>(Func<LicenseResolution, Func<string, string?>, TGate> create)
        where TGate : ILicenseGate
    {
        ArgumentNullException.ThrowIfNull(create);
        LicenseResolution resolution = Path is { } path
            ? new LicenseResolution(LicenseSourceKind.Flag, path)
            : LicenseResolution.None;
        TGate gate = create(resolution, static _ => null);
        gate.EnsureApplied();
        return gate;
    }
}

/// <summary>A commercial-license scenario; it runs only when an explicit test license is supplied.</summary>
public sealed class LicensedFactAttribute : FactAttribute
{
    public LicensedFactAttribute(
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(TestLicense.PathVariable)))
        {
            Skip = $"Set {TestLicense.PathVariable} to exercise a real commercial license.";
        }
    }
}
