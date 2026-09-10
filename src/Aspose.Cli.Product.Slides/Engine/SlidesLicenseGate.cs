using Aspose.Cli.Sdk.Licensing;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>Applies the resolved Aspose.Slides license lazily and once.</summary>
internal sealed class SlidesLicenseGate(
    LicenseResolution resolution,
    Func<string, string?> environmentVariable)
    : LicenseGate(resolution, environmentVariable)
{
    protected override void ApplyLicense(string path) =>
        new Aspose.Slides.License().SetLicense(path);

    protected override void ApplyLicense(Stream stream) =>
        new Aspose.Slides.License().SetLicense(stream);
}
