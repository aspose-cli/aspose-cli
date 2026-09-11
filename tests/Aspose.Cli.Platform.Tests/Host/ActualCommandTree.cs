using System.Reflection;
using Aspose.Cli.Host;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Sdk;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.TestKit;

namespace Aspose.Cli.Host.Tests;

/// <summary>Loads this build's generated catalog; parsing tests never activate its engines.</summary>
internal static class ActualCommandTree
{
    private static readonly Lazy<HostContext> Context = new(() =>
    {
        Assembly launcher = Assembly.LoadFrom(Path.ChangeExtension(CliRunner.ExecutablePath, ".dll"));
        Type type = launcher.GetType("Aspose.Cli.Generated.CompiledProductCatalog", throwOnError: true)!;
        var catalog = (ProductCatalog)type.GetProperty("Instance",
            BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        return new HostContext(catalog, new CliEditionInfo(DistributionInfo.Edition, DistributionInfo.DisplayName));
    });

    internal static HostContext Host => Context.Value;
    internal static InvocationParser Parser => Host.Parser;
}
