using System.CommandLine;
using System.Globalization;
using System.Text;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Host.Output;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;

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
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(catalogFactory);
        Invocation.WindowsProcessErrorMode.SuppressNativeErrorUi();
        try
        {
            ProductCatalog catalog = catalogFactory();
            using InvocationInputs? inputs = InvocationInputs.Receive();
            var host = new HostContext(catalog, TimeoutWorkerSupervisor.ReceiveOutputSession());
            ConfigureConsole();
            ConfigureUiCulture();
            args = NormalizeInteractiveArguments(args, IsInteractiveDesktop(), host.Parser);
            return ProcessFailureBoundary.Run(host, args, arguments =>
            {
                ParsedInvocation invocation = host.Parser.Parse(arguments);
                invocation.EnsureValid();
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

    private static bool IsInteractiveDesktop() =>
        !Console.IsInputRedirected
        && !Console.IsOutputRedirected
        && !Console.IsErrorRedirected
        && Environment.UserInteractive
        && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CI"))
        && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"));

    /// <summary>
    /// A double-click, or dropping a file on the executable, opens the App. A
    /// lone argument names a file to open only when it is not already a
    /// command line, so <c>aspose-cli doctor</c> stays the doctor even in a
    /// folder that holds a file named <c>doctor</c>.
    /// </summary>
    internal static string[] NormalizeInteractiveArguments(
        string[] args,
        bool interactiveDesktop,
        InvocationParser parser)
    {
        if (!interactiveDesktop)
        {
            return args;
        }

        if (args.Length == 0)
        {
            return ["app"];
        }

        return args.Length == 1
            && File.Exists(args[0])
            && parser.Parse(args).ParseResult.Errors.Count > 0
            ? ["app", args[0]]
            : args;
    }

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

    private static void ConfigureUiCulture()
    {
        CultureInfo english = CultureInfo.GetCultureInfo("en");
        CultureInfo.DefaultThreadCurrentUICulture = english;
        Thread.CurrentThread.CurrentUICulture = english;
    }
}
