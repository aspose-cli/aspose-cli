using Aspose.Cli.Sdk.Licensing;

namespace Aspose.Cli.Product.Words.Engine;

/// <summary>Applies the resolved Aspose.Words license lazily and once.</summary>
internal sealed class WordsLicenseGate(
    LicenseResolution resolution,
    Func<string, string?> environmentVariable)
    : LicenseGate(resolution, environmentVariable)
{
    protected override void ApplyLicense(Stream stream) =>
        new Aspose.Words.License().SetLicense(stream);
}
