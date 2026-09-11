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
    /// edition catalog or host construction.
    /// </summary>
    public static int Run(
        string[] args,
        Func<ProductCatalog> catalogFactory,
        CliEditionInfo edition)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(catalogFactory);
        ArgumentNullException.ThrowIfNull(edition);
        Invocation.WindowsProcessErrorMode.SuppressNativeErrorUi();
        try
        {
            return Run(args, catalogFactory(), edition);
        }
        catch (Exception exception)
        {
            return ProcessFailureBoundary.RenderBootstrapFailure(
                exception,
                Console.Error);
        }
    }

    /// <summary>Runs one CLI invocation.</summary>
    public static int Run(
        string[] args,
        ProductCatalog catalog,
        CliEditionInfo edition)
    {
        ArgumentNullException.ThrowIfNull(args);
        Invocation.WindowsProcessErrorMode.SuppressNativeErrorUi();
        var host = new HostContext(catalog, edition);
        ConfigureConsole();
        ConfigureUiCulture();
        args = NormalizeInteractiveArguments(args, IsInteractiveDesktop());
        return ProcessFailureBoundary.Run(host, args, arguments =>
        {
            ParsedInvocation invocation = host.Parser.Parse(arguments);
            invocation.EnsureValid();
            return TimeoutWorkerSupervisor.Run(host, arguments, invocation,
                () => RunInProcess(invocation));
        });
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

    internal static string[] NormalizeInteractiveArguments(
        string[] args,
        bool interactiveDesktop)
    {
        if (!interactiveDesktop)
        {
            return args;
        }

        if (args.Length == 0)
        {
            return ["app"];
        }

        return args.Length == 1 && File.Exists(args[0])
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
