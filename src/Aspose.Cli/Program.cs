using Aspose.Cli.Host;

namespace Aspose.Cli;

/// <summary>Process entry point for the commercial distribution.</summary>
public static class Program
{
    public static int Main(string[] args) =>
        CliHost.Run(
            args,
            static () => CompiledProductCatalog.Instance,
            new CliEditionInfo(Aspose.Cli.Sdk.DistributionInfo.Edition, Aspose.Cli.Sdk.DistributionInfo.DisplayName));
}
