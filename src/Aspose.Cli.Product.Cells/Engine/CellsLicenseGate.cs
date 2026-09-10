using Aspose.Cli.Sdk.Licensing;

namespace Aspose.Cli.Product.Cells.Engine;

/// <summary>Applies the resolved Aspose.Cells license lazily and once.</summary>
internal sealed class CellsLicenseGate(
    LicenseResolution resolution,
    Func<string, string?> environmentVariable)
    : LicenseGate(resolution, environmentVariable)
{
    protected override void ApplyLicense(string path) =>
        new Aspose.Cells.License().SetLicense(path);

    protected override void ApplyLicense(Stream stream) =>
        new Aspose.Cells.License().SetLicense(stream);
}
