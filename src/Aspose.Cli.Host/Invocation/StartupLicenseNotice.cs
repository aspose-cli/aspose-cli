using Aspose.Cli.Host.Licensing;
using Aspose.Cli.Host.Output;
using Aspose.Cli.Sdk;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Licensing;

namespace Aspose.Cli.Host.Invocation;

/// <summary>Reports native-validated licenses once at the human CLI boundary.</summary>
internal static class StartupLicenseNotice
{
    internal static void Write(
        HostContext host,
        ParsedInvocation invocation,
        TextWriter error)
    {
        GlobalValues? globals = invocation.GlobalValues;
        if (globals is null
            || globals.Output is OutputMode.Json or OutputMode.Compact
            || globals.Quiet
            || globals.Verbose
            || InvocationInputs.Current is not null
            || invocation.CommandPath.FirstOrDefault()?.Name == "mcp"
            || invocation.CommandPath.Any(static command => command.Hidden))
        {
            return;
        }

        LicenseStatusResult status = LicenseManager.Inspect(
            CompositionRoot.Create(host.Catalog, globals),
            invocation.ProductId);
        if (!status.Applicable)
        {
            return;
        }

        IEnumerable<ProductLicenseStatus> products = status.Products.Where(
            static product => product.Applicable);
        string summary = string.Join("; ", products.Select(
            static product => product.Source == LicenseResolution.RequestedSourceLabel
                ? $"{product.Product}={product.Mode} (requested)"
                : $"{product.Product}={product.Mode}"));
        string detail = status.Products.Any(static product => product.Problem is not null)
            ? $" (run '{DistributionInfo.CommandName} license status' for details)"
            : string.Empty;
        error.WriteLine($"license: {summary}{detail}");
    }

}
