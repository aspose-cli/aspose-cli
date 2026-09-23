using System.CommandLine;
using Aspose.Cli.Host.App;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Host.ViewerService;
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
            Description = "Loopback port; 0 chooses a free port.",
            DefaultValueFactory = _ => 0,
        };
        var noOpenOption = new Option<bool>("--no-open")
        {
            Description = "Start or activate the App without opening a browser.",
        };
        var routeOption = new Option<string?>("--route") { Hidden = true }.WithInput(InputKind.None);

        var app = new Command("app", "Open the local file workspace in a browser.");
        app.Arguments.Add(fileArgument);
        app.Options.Add(welcomeOption);
        app.Options.Add(foregroundOption);
        app.Options.Add(portOption);
        app.Options.Add(noOpenOption);
        app.Options.Add(routeOption);

        app.Subcommands.Add(CreateStatus(executor, globals));
        app.Subcommands.Add(CreateStop(executor, globals));

        app.SetAction(parseResult =>
        {
            int port = parseResult.GetValue(portOption);
            OptionGuards.EnsureInRange("--port", port, 0, 65535,
                "Pass --port 0 for a free port, or a port between 1 and 65535.");
            bool openBrowser = !parseResult.GetValue(noOpenOption);
            if (parseResult.GetValue(foregroundOption))
            {
                // Debugging, containers and browser tests: the service, the
                // App and this command are one process that does not detach.
                return executor.RunHosted(parseResult, globals, values =>
                    ViewerServiceHosting.Start(
                        values,
                        port,
                        catalog,
                        capabilities,
                        Page(parseResult, values, routeOption, welcomeOption, fileArgument),
                        openBrowser));
            }
            return executor.RunLightweight(parseResult, globals, (values, budgets) =>
            {
                ViewerAppRequest page = Page(parseResult, values, routeOption, welcomeOption, fileArgument);
                var client = new ViewerServiceClient();
                bool reused = client.Status(budgets.Deadline) is not null;
                ViewerAppResponse opened = client.App(values, page, port, budgets.Deadline);
                if (openBrowser)
                {
                    BrowserLauncher.Open(opened.Url);
                }
                return new AppResult
                {
                    Running = true,
                    Url = opened.Url,
                    Port = opened.Port,
                    Pid = opened.Pid,
                    Reused = reused,
                    Route = opened.Route,
                    File = opened.File,
                };
            });
        });

        return app.WithInvocationPolicy(new CommandInvocationPolicy(Execution: CommandExecutionOwnership.Service));
    }

    private static Command CreateStatus(CommandExecutor executor, GlobalOptions globals)
    {
        var status = new Command("status", "Show whether the local App is running.");
        status.SetAction(parseResult => executor.RunLightweight(
            parseResult,
            globals,
            (_, _) => Describe(new ViewerServiceClient().Status())));
        return status.WithInvocationPolicy(new CommandInvocationPolicy(McpReadOnly: true));
    }

    private static Command CreateStop(CommandExecutor executor, GlobalOptions globals)
    {
        var stop = new Command("stop", "Stop the local App, its documents and the viewer service.");
        stop.SetAction(parseResult => executor.RunLightweight(
            parseResult,
            globals,
            (_, _) =>
            {
                _ = new ViewerServiceClient().Stop(id: null, all: true);
                return new AppResult
                {
                    Running = false,
                    Reused = false,
                    Route = AppRoutes.Home,
                };
            }));
        return stop;
    }

    /// <summary>
    /// The App lives in the viewer service, so its state is the service's:
    /// running when the service runs, showing whatever it has open.
    /// </summary>
    private static AppResult Describe(ViewerStatusResponse? service) => new()
    {
        Running = service is not null,
        Url = service is null
            ? null
            : service.Url + ((service.AppRoute ?? AppRoutes.Home) == AppRoutes.Welcome
                ? string.Empty
                : service.AppRoute ?? AppRoutes.Home),
        Port = service is null ? null : PortOf(service.Url),
        Pid = service?.Pid,
        Reused = false,
        Route = service?.AppRoute ?? AppRoutes.Home,
        File = service?.AppFile,
    };

    private static int? PortOf(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out Uri? parsed) ? parsed.Port : null;

    /// <summary>The page the browser lands on, and the file it shows there.</summary>
    private static ViewerAppRequest Page(
        ParseResult parseResult,
        GlobalValues values,
        Option<string?> routeOption,
        Option<bool> welcomeOption,
        Argument<string?> fileArgument)
    {
        string? file = ResolveFile(parseResult.GetValue(fileArgument), values);
        return new ViewerAppRequest
        {
            Route = parseResult.GetValue(routeOption)
                ?? (file is not null
                    ? AppRoutes.Preview
                    : parseResult.GetValue(welcomeOption) ? AppRoutes.Welcome : AppRoutes.Home),
            File = file,
        };
    }

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
