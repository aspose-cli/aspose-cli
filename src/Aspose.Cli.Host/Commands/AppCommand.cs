using System.CommandLine;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Host.ViewerService;
using Aspose.Cli.Sdk;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;

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
        var welcomeOption = new Option<bool>("--welcome")
        {
            Description = "Open the first-run workspace guide.",
        };
        var routeOption = new Option<string?>("--route") { Hidden = true }.WithInput(InputKind.None);
        var app = new Command("app", "Open the local file workspace in a browser.");
        StartOptions home = StartOptions.AddTo(app);
        app.Options.Add(welcomeOption);
        app.Options.Add(routeOption);
        app.SetAction(parseResult => Start(executor, catalog, capabilities, globals, parseResult, home, _ => new ViewerAppRequest
        {
            Route = parseResult.GetValue(routeOption)
                ?? (parseResult.GetValue(welcomeOption) ? AppRoutes.Welcome : AppRoutes.Home),
        }));

        app.Subcommands.Add(CreateOpen(executor, catalog, capabilities, globals));
        app.Subcommands.Add(CreateStatus(executor, globals));
        app.Subcommands.Add(CreateStop(executor, globals));

        // The App is one shared workspace that follows the configured license.
        return app.WithInvocationPolicy(new CommandInvocationPolicy(
            Execution: CommandExecutionOwnership.Service, RefusesEvaluationRequest: true));
    }

    private static Command CreateOpen(
        CommandExecutor executor,
        ProductCatalog catalog,
        Func<CapabilitiesResult> capabilities,
        GlobalOptions globals)
    {
        var fileArgument = new Argument<string?>("file")
        {
            Description = "File to open in the local App.",
            Arity = ArgumentArity.ZeroOrOne,
        }.WithInput(InputKind.File);
        var open = new Command("open", "Open a file in the local App.");
        open.Arguments.Add(fileArgument);
        StartOptions options = StartOptions.AddTo(open);
        open.SetAction(parseResult => Start(executor, catalog, capabilities, globals, parseResult, options, values => new ViewerAppRequest
        {
            Route = AppRoutes.Preview,
            File = ResolveFile(parseResult.GetValue(fileArgument), values),
        }));
        return open;
    }

    /// <summary>Starts or activates the App and lands on the page <paramref name="page"/> chooses.</summary>
    private static int Start(
        CommandExecutor executor,
        ProductCatalog catalog,
        Func<CapabilitiesResult> capabilities,
        GlobalOptions globals,
        ParseResult parseResult,
        StartOptions options,
        Func<GlobalValues, ViewerAppRequest> page)
    {
        int port = parseResult.GetValue(options.Port);
        OptionGuards.EnsureInRange("--port", port, 0, 65535,
            "Pass --port 0 for a free port, or a port between 1 and 65535.");
        bool openBrowser = !parseResult.GetValue(options.NoOpen);
        if (parseResult.GetValue(options.Foreground))
        {
            // Debugging, containers and browser tests: the service, the
            // App and this command are one process that does not detach.
            return executor.RunHosted(parseResult, globals, values =>
                ViewerServiceHosting.Start(
                    values,
                    port,
                    catalog,
                    capabilities,
                    page(values),
                    openBrowser));
        }
        return executor.RunLightweight(parseResult, globals, (values, budgets) =>
        {
            ViewerAppRequest request = page(values);
            var client = new ViewerServiceClient();
            bool reused = client.Status(budgets.Deadline) is not null;
            ViewerAppResponse opened = client.App(values, request, port, budgets.Deadline);
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
    }

    private static Command CreateStatus(CommandExecutor executor, GlobalOptions globals)
    {
        var status = new Command("status", "Show whether the local App is running.");
        status.SetAction(parseResult => executor.RunLightweight(
            parseResult,
            globals,
            (_, _) => Describe(new ViewerServiceClient().Status())));
        return status.WithInvocationPolicy(new CommandInvocationPolicy(McpAllowed: true));
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

    private static string ResolveFile(string? value, GlobalValues globals)
    {
        if (value is not { Length: > 0 })
        {
            throw CliErrors.OptionInvalid(
                "file",
                "no file was given",
                $"Run '{DistributionInfo.CommandName} app open <file>'.");
        }

        string baseDirectory = globals.WorkDir is null
            ? Directory.GetCurrentDirectory()
            : Path.GetFullPath(globals.WorkDir);
        return Path.GetFullPath(value, baseDirectory);
    }

    /// <summary>The options that start or activate the App, one set for each command that does.</summary>
    private sealed record StartOptions(Option<bool> Foreground, Option<int> Port, Option<bool> NoOpen)
    {
        public static StartOptions AddTo(Command command)
        {
            var options = new StartOptions(
                new Option<bool>("--foreground")
                {
                    Description = "Keep the App in this process for debugging, containers and tests.",
                },
                new Option<int>("--port")
                {
                    Description = "Loopback port; 0 chooses a free port.",
                    DefaultValueFactory = _ => 0,
                },
                new Option<bool>("--no-open")
                {
                    Description = "Start or activate the App without opening a browser.",
                });
            command.Options.Add(options.Foreground);
            command.Options.Add(options.Port);
            command.Options.Add(options.NoOpen);
            return options;
        }
    }
}
