using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Licensing;

namespace Aspose.Cli.Sdk.Extensibility;

/// <summary>Centralizes product license resolution and deferred invalid-source reporting.</summary>
internal static class ProductLicenseGateFactory
{
    public static ILicenseGate Create(
        ProductActivationContext context,
        string productId,
        Func<LicenseResolution, ILicenseGate> factory)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(productId);
        ArgumentNullException.ThrowIfNull(factory);
        if (context.EvaluationRequested)
        {
            // An explicit request, not a fall back: no source is read, so none can fail.
            return factory(LicenseResolution.EvaluationRequested)
                ?? throw new InvalidOperationException($"Product '{productId}' returned no license gate.");
        }
        try
        {
            LicenseResolution resolution = LicenseResolver.Resolve(
                context.LicensePath, productId, context.EnvironmentVariable,
                context.WorkDirectory, context.ConfigDirectory, context.UserLicenseChanges);
            return factory(resolution)
                ?? throw new InvalidOperationException($"Product '{productId}' returned no license gate.");
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
