using System.Text;
using Aspose.Cli.Host.Licensing;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Host.Tests;

/// <summary>
/// A fingerprint tells a resident process whether the license it applied is still the one a
/// request would apply. License content carried in an environment variable can differ from one
/// requester to the next, so the fingerprint follows the content, without ever holding it.
/// </summary>
[Collection(ProcessLicenseEnvironmentCollection.Name)]
public sealed class LicenseFingerprintTests
{
    [Fact]
    public void ProcessBase64License_ChangedValue_ChangesTheFingerprint()
    {
        using var workspace = new TempWorkspace();
        string? saved = Environment.GetEnvironmentVariable(LicenseResolver.EnvBase64Name);
        try
        {
            Environment.SetEnvironmentVariable(LicenseResolver.EnvBase64Name, Encode("<License>first</License>"));
            string first = Fingerprint(workspace);
            Environment.SetEnvironmentVariable(LicenseResolver.EnvBase64Name, Encode("<License>second</License>"));
            string second = Fingerprint(workspace);
            Environment.SetEnvironmentVariable(LicenseResolver.EnvBase64Name, Encode("<License>first</License>"));
            string again = Fingerprint(workspace);

            Assert.NotEqual(first, second);
            Assert.Equal(first, again);
        }
        finally
        {
            Environment.SetEnvironmentVariable(LicenseResolver.EnvBase64Name, saved);
        }
    }

    private static string Fingerprint(TempWorkspace workspace) =>
        LicenseFingerprint.Of(ActualCommandTree.Host.Catalog, null, workspace.Path, workspace.ConfigDirectory);

    private static string Encode(string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
}
