using System.CommandLine;
using Aspose.Cli.Host.App;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility.Commanding;
using Aspose.Cli.Sdk.Rendering;

namespace Aspose.Cli.Host.Commands;

/// <summary>The local browser application command and lifecycle controls.</summary>
internal static class AppCommand
{
    public static Command Create(
        CommandExecutor executor,
        ProductCatalog catalog,
        CliEditionInfo edition,
        Func<CapabilitiesResult> capabilities,
        GlobalOptions globals)
    {
        var fileArgument = new Argument<string?>("file")
        {
            Description = "File to open directly in the local App.",
            Arity = ArgumentArity.ZeroOrOne,
        }.WithInput(InputKind.File);
        var welcomeOption = new Option<bool>("--welcome")
        {
            Description = "Open the first-run workspace guide.",
        };
        var foregroundOption = new Option<bool>("--foreground")
        {
            Description = "Keep the App in this process for debugging, containers and tests.",
        };
        var portOption = new Option<int>("--port")
        {
            Description = "Loopback port in foreground mode; 0 chooses a free port.",
            DefaultValueFactory = _ => 0,
        };
        var noOpenOption = new Option<bool>("--no-open")
        {
            Description = "Start or activate the App without opening a browser.",
        };
        var serveOption = new Option<bool>("--serve") { Hidden = true };
        var routeOption = new Option<string?>("--route") { Hidden = true }.WithInput(InputKind.None);
        var fonts = new FontDirectoryOptions();

        var app = new Command("app", "Open the local file workspace in a browser.");
        app.Arguments.Add(fileArgument);
        app.Options.Add(welcomeOption);
        app.Options.Add(foregroundOption);
        app.Options.Add(portOption);
        app.Options.Add(noOpenOption);
        app.Options.Add(serveOption);
        app.Options.Add(routeOption);
        fonts.AddTo(app);

        app.Subcommands.Add(CreateStatus(
            executor,
            catalog,
            edition,
            capabilities,
            globals));
        app.Subcommands.Add(CreateStop(
            executor,
            catalog,
            edition,
            capabilities,
            globals));

        app.SetAction(parseResult =>
        {
            var coordinator = new AppServiceController(
                catalog,
                edition,
                capabilities);
            int port = parseResult.GetValue(portOption);
            OptionGuards.EnsureInRange("--port", port, 0, 65535,
                "Pass --port 0 for a free port, or a port between 1 and 65535.");

            string? file = ResolveFile(parseResult.GetValue(fileArgument), globals.Resolve(parseResult));
            string route = parseResult.GetValue(routeOption)
                ?? ResolveRoute(parseResult.GetValue(welcomeOption));
            bool openBrowser = !parseResult.GetValue(noOpenOption);
            FontSearchProfile fontProfile = fonts.Read(parseResult);

            if (parseResult.GetValue(serveOption))
            {
                ServiceStartSecrets? secrets =
                    ServiceStartSecretChannel.TryReceive();
                if (secrets is null)
                {
                    throw CliErrors.OptionInvalid(
                        "app service",
                        "the authenticated service-start channel is missing",
                        "Start the background App with 'aspose-cli app'.");
                }

                using IDisposable scope =
                    ServiceStartSecretChannel.Push(secrets);
                return coordinator.RunService(
                    globals.Resolve(parseResult),
                    port,
                    route,
                    file);
            }

            return parseResult.GetValue(foregroundOption)
                ? executor.RunHosted(parseResult, globals,
                    values => coordinator.StartForeground(
                        values, port, route, file, openBrowser, fontProfile))
                : executor.RunLightweight(parseResult, globals,
                    (values, _) => coordinator.StartOrActivate(
                        values,
                        route,
                        file,
                        openBrowser,
                        fontProfile));
        });

        return app;
    }

    private static Command CreateStatus(
        CommandExecutor executor,
        ProductCatalog catalog,
        CliEditionInfo edition,
        Func<CapabilitiesResult> capabilities,
        GlobalOptions globals)
    {
        var status = new Command("status", "Show whether the local App is running.");
        status.SetAction(parseResult => executor.RunLightweight(
            parseResult,
            globals,
            (_, _) => new AppServiceController(
                catalog,
                edition,
                capabilities).Status()));
        return status;
    }

    private static Command CreateStop(
        CommandExecutor executor,
        ProductCatalog catalog,
        CliEditionInfo edition,
        Func<CapabilitiesResult> capabilities,
        GlobalOptions globals)
    {
        var stop = new Command("stop", "Stop the local App and its preview sessions.");
        stop.SetAction(parseResult => executor.RunLightweight(
            parseResult,
            globals,
            (_, _) => new AppServiceController(
                catalog,
                edition,
                capabilities).Stop()));
        return stop;
    }

    private static string ResolveRoute(bool welcome) =>
        welcome ? AppRoutes.Welcome : AppRoutes.Home;

    private static string? ResolveFile(string? value, GlobalValues globals)
    {
        if (value is null)
        {
            return null;
        }

        string baseDirectory = globals.WorkDir is null
            ? Directory.GetCurrentDirectory()
            : Path.GetFullPath(globals.WorkDir);
        return Path.GetFullPath(value, baseDirectory);
    }
}
