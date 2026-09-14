using Aspose.Cli.Sdk.Licensing;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>Applies the resolved Aspose.PDF license lazily and once.</summary>
internal sealed class PdfLicenseGate(
    LicenseResolution resolution,
    Func<string, string?> environmentVariable)
    : LicenseGate(resolution, environmentVariable)
{
    protected override void ApplyLicense(Stream stream) =>
        new Aspose.Pdf.License().SetLicense(stream);
}
