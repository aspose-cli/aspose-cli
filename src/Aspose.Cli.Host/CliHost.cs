using System.CommandLine;
using System.Globalization;
using System.Text;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Host.Licensing;
using Aspose.Cli.Host.Output;
using Aspose.Cli.Sdk.IO.Input;

namespace Aspose.Cli.Host;

/// <summary>Runs the shared CLI host against one explicit compiled product catalog.</summary>
public static class CliHost
{
    /// <summary>
    /// Runs one CLI invocation while also containing failures raised during
    /// product catalog or host construction.
    /// </summary>
    public static int Run(string[] args, Func<ProductCatalog> catalogFactory)
    {
        Invocation.WindowsDllSearch.ExcludeCurrentDirectory();
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(catalogFactory);
        Invocation.WindowsProcessErrorMode.SuppressNativeErrorUi();
        ConfigureCulture();
        try
        {
            ProductCatalog catalog = catalogFactory();
            using InvocationInputs? inputs = InvocationInputs.Receive();
            var host = new HostContext(catalog, TimeoutWorkerSupervisor.ReceiveOutputSession());
            ConfigureConsole();
            return ProcessFailureBoundary.Run(host, args, arguments =>
            {
                ParsedInvocation invocation = host.Parser.Parse(arguments);
                if (invocation.LacksCommand
                    && host.Parser.ResolveErrorOutput(arguments).OutputFormat is OutputMode.Table or OutputMode.Markdown)
                {
                    // A group run without a command answers with its help, before the error.
                    host.Parser.WriteHelp(arguments, Console.Error);
                }

                invocation.EnsureValid();
                // Every process that runs a command, a worker included, gets here first.
                NetworkGate.Admit(invocation.NetworkAccess);
                StartupLicenseNotice.Write(host, invocation, Console.Error);
                return TimeoutWorkerSupervisor.Run(host, arguments, invocation,
                    () => RunInProcess(invocation));
            });
        }
        catch (Exception exception)
        {
            return ProcessFailureBoundary.RenderBootstrapFailure(
                exception,
                Console.Error);
        }
    }

    private static int RunInProcess(ParsedInvocation invocation) =>
        invocation.ParseResult.Invoke(new InvocationConfiguration
        {
            EnableDefaultExceptionHandler = false,
        });

    private static void ConfigureConsole()
    {
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
        }
        catch (Exception exception) when (
            exception is IOException or PlatformNotSupportedException)
        {
            // A detached process may not have a writable console. In that
            // case the platform default encoding remains the only option.
        }
    }

    /// <summary>
    /// Pins the process's cultures, so what a command computes or shows does not change with the
    /// machine's regional settings; a command that takes another culture does so by an option.
    /// </summary>
    private static void ConfigureCulture()
    {
        CultureInfo english = CultureInfo.GetCultureInfo("en");
        CultureInfo.DefaultThreadCurrentUICulture = english;
        Thread.CurrentThread.CurrentUICulture = english;
        CultureInfo unitedStates = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.DefaultThreadCurrentCulture = unitedStates;
        Thread.CurrentThread.CurrentCulture = unitedStates;
    }
}
