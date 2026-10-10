using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Licensing;

namespace Aspose.Cli.Sdk.Extensibility;

/// <summary>Centralizes product license resolution and deferred invalid-source reporting.</summary>
internal static class ProductLicenseGateFactory
{
    public static ILicenseGate Create(
        ProductActivationContext context,
        string productId,
        Action<Stream> applyLicense)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(productId);
        ArgumentNullException.ThrowIfNull(applyLicense);
        if (context.EvaluationRequested)
        {
            // An explicit request, not a fall back: no source is read, so none can fail.
            return new LicenseGate(LicenseResolution.EvaluationRequested, context.EnvironmentVariable, applyLicense);
        }
        try
        {
            LicenseResolution resolution = LicenseResolver.Resolve(
                context.LicensePath, productId, context.EnvironmentVariable,
                context.WorkDirectory, context.ConfigDirectory, context.UserLicenseChanges);
            return new LicenseGate(resolution, context.EnvironmentVariable, applyLicense);
        }
        catch (CliException exception) { return new UnavailableLicenseGate(exception); }
    }

    private sealed class UnavailableLicenseGate(CliException error) : ILicenseGate
    {
        public bool IsApplicable => true;
        public LicenseResolution Resolution => LicenseResolution.None;
        public string Identity => throw error;
        public LicenseState EnsureApplied() => throw error;
    }
}
