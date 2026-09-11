using System.CommandLine;
using Aspose.Cli.Host.Catalog;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Host.Preview;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;
using Aspose.Cli.Sdk.Rendering;
using Aspose.Cli.Sdk.Results;

namespace Aspose.Cli.Host.Commands;

/// <summary>Product-neutral entry point for background file previews.</summary>
internal static class PreviewCommand
{
    private const string AutoView = "auto";
    private const int MaxPort = 65535;

    public static Command Create(
        CommandExecutor executor,
        ProductCatalog catalog,
        GlobalOptions globals)
    {
        var file = new Argument<string?>("file")
        {
            Description = "File to preview; its extension selects the product unless --product is supplied.",
            Arity = ArgumentArity.ZeroOrOne,
        }.WithInput(InputKind.File);
        StartSymbols shortcutSymbols = StartSymbols.Create(catalog, "the file");
        var preview = new Command("preview", "Start, inspect, and stop background previews for any file product.");
        preview.Arguments.Add(file);
        shortcutSymbols.AddTo(preview);
        preview.Subcommands.Add(CreateStatus(executor, globals));
        preview.Subcommands.Add(CreateStop(executor, globals));
        preview.Subcommands.Add(CreateHost(executor, catalog, globals));
        preview.SetAction(parse => executor.Run(parse, globals, context =>
            Start(catalog, parse, context, file, shortcutSymbols)));
        return preview.WithInvocationPolicy(new CommandInvocationPolicy(ServiceLifetime: true));
    }

    private static Command CreateHost(
        CommandExecutor executor,
        ProductCatalog catalog,
        GlobalOptions globals)
    {
        var file = new Argument<string>("file") { Description = "Preview source file." }.WithInput(InputKind.File);
        var product = new Option<string>("--product") { Description = "Owning product id." }.WithInput(InputKind.None);
        var port = new Option<int>("--port") { Description = "Loopback preview port." };
        var view = new Option<string>("--view") { Description = "Product preview view." }.WithInput(InputKind.None);
        var effect = new Option<string?>("--presentation-effect")
        {
            Description = "Optional product presentation effect.",
        }.WithInput(InputKind.None);
        var serviceId = new Option<string?>("--preview-service-id") { Hidden = true }.WithInput(InputKind.None);
        var password = new PasswordOptions("--password", "the preview source", allowStdin: false);
        var host = new Command("__host", "Internal product-neutral preview host.")
        {
            Hidden = true,
        };
        host.Arguments.Add(file);
        host.Options.Add(product);
        host.Options.Add(port);
        host.Options.Add(view);
        host.Options.Add(effect);
        host.Options.Add(serviceId);
        password.AddTo(host);
        host.SetAction(parse =>
        {
            ServiceStartSecrets? secrets =
                ServiceStartSecretChannel.TryReceive();
            using IDisposable? scope = secrets is null
                ? null
                : ServiceStartSecretChannel.Push(secrets);
            return executor.RunServer(
                parse,
                globals,
                context => StartHost(
                    catalog,
                    parse,
                    context,
                    file,
                    product,
                    port,
                    view,
                    effect,
                    serviceId,
                    password,
                    secrets));
        });
        return host;
    }

    private static HostedCommandLifecycle StartHost(
        ProductCatalog catalog,
        ParseResult parse,
        CommandContext context,
        Argument<string> file,
        Option<string> product,
        Option<int> port,
        Option<string> view,
        Option<string?> effect,
        Option<string?> serviceId,
        PasswordOptions password,
        ServiceStartSecrets? secrets)
    {
        int requestedPort = parse.GetValue(port);
        OptionGuards.EnsureInRange(
            "--port", requestedPort, 0, MaxPort,
            "Pass --port 0 for a system-assigned port, or a port from 1 to 65535.");
        string input = context.Paths.ResolveInput(
            parse.GetRequiredValue(file));
        ProductDefinition definition = catalog.ResolveById(
            parse.GetRequiredValue(product));
        string selectedView = parse.GetRequiredValue(view);
        Aspose.Cli.Sdk.Preview.ProductPreviewPayload? selectedSelector =
            secrets?.PreviewSelector;
        string? id = parse.GetValue(serviceId);
        string? token = secrets?.ServiceToken;
        if (id is not null && token is null)
        {
            throw CliErrors.OptionInvalid(
                "preview service",
                "the authenticated service-start channel is missing",
                "Start background previews with 'aspose-cli preview <file>'.");
        }

        using RunningPreview runtime = PreviewRuntime.Start(
            new PreviewStartOptions(
                context.Activate(definition),
                definition,
                context.ResourceBudgets,
                input,
                requestedPort,
                new ProductPreviewRequest(
                    selectedView,
                    Password: secrets?.Password
                        ?? password.Resolve(
                            parse,
                            context.ResourceBudgets.Inputs),
                    Selector: selectedSelector,
                    FontProfile: secrets?.FontProfile),
                parse.GetValue(effect)));
        var result = new ProductPreviewStartResult
        {
            Id = id ?? string.Empty,
            Product = definition.Manifest.Id,
            Url = runtime.Url,
            Pid = Environment.ProcessId,
            File = input,
            View = runtime.View,
            Selector = selectedSelector,
            Reused = false,
            License = EnvelopeParts.License(runtime.License),
            Warnings = EnvelopeParts.CombineWarnings(
                EnvelopeParts.OutputWarnings(runtime.License),
                runtime.Outcome.Warnings),
        };
        PreviewServiceLifetime? service = PreviewBackgroundService.Create(
            runtime,
            definition,
            input,
            selectedSelector,
            id,
            token,
            ResultEnvelopeMetadata.From(result));
        TimeSpan idle = service is null
            ? PreviewRuntime.ResolveIdleWindow()
            : Timeout.InfiniteTimeSpan;
        return runtime.Supervise(result, once: false, idle, service);
    }

    private static ProductPreviewStartResult Start(
        ProductCatalog catalog,
        ParseResult parse,
        CommandContext context,
        Argument<string?> file,
        StartSymbols symbols)
    {
        string? fileValue = parse.GetValue(file);
        if (string.IsNullOrWhiteSpace(fileValue))
        {
            throw CliErrors.OptionInvalid(
                "preview",
                "a file path is required",
                "Run 'aspose-cli preview <file>'.");
        }

        int port = parse.GetValue(symbols.Port);
        OptionGuards.EnsureInRange(
            "--port", port, 0, MaxPort,
            "Pass --port 0 for a system-assigned port, or a port from 1 to 65535.");
        string input = context.Paths.ResolveInput(fileValue);
        ProductDefinition product = catalog.ResolveExistingFile(
            input,
            parse.GetValue(symbols.Product),
            operation: "preview",
            cancellationToken: context.Deadline.Token);
        ProductPreviewDefinition adapter = product.Preview;
        string requestedView = parse.GetValue(symbols.View) ?? AutoView;
        string view = requestedView == AutoView ? adapter.DefaultView : requestedView;
        FontSearchProfile fontProfile = symbols.Fonts.Read(parse);
        if (!fontProfile.IsAmbient
            && !product.Manifest.Engine.SupportsExplicitFontProfiles)
        {
            throw CliErrors.OptionInvalid(
                "--font-dir",
                $"explicit font profiles are not supported by {product.Manifest.Id}",
                "Omit --font-dir or use a product that advertises supportsExplicitFontProfiles.");
        }
        PreviewErrors.EnsureViewSupported(
            product.Manifest.Id,
            view,
            adapter.Views);
        PreviewStartState state = new PreviewServiceController().Start(
            product,
            context.Paths.BaseDirectory,
            context.Globals.LicensePath is null
                ? null
                : Path.GetFullPath(
                    context.Globals.LicensePath,
                    context.Paths.BaseDirectory),
            input,
            port,
            new ProductPreviewRequest(
                view,
                Password: symbols.Password.Resolve(
                    parse, context.ResourceBudgets.Inputs),
                FontProfile: fontProfile.IsAmbient ? null : fontProfile),
            presentationEffect: null,
            openBrowser: parse.GetValue(symbols.Open));
        return ToResult(state);
    }

    private static Command CreateStatus(
        CommandExecutor executor,
        GlobalOptions globals)
    {
        var id = new Argument<string?>("id")
        {
            Description = "Optional session id.",
            Arity = ArgumentArity.ZeroOrOne,
        }.WithInput(InputKind.None);
        var status = new Command("status", "List one or all current-user preview sessions.");
        status.Arguments.Add(id);
        status.SetAction(parse => executor.RunLightweight(parse, globals, (_, _) =>
        {
            PreviewStatusState state = new PreviewServiceController().Status(parse.GetValue(id));
            return new ProductPreviewStatusResult
            {
                Sessions = state.Sessions.Select(ToInfo).ToArray(),
                Warnings = state.Warnings.Count == 0 ? null : state.Warnings,
            };
        }));
        return status.WithInvocationPolicy(new CommandInvocationPolicy(McpReadOnly: true));
    }

    private static Command CreateStop(
        CommandExecutor executor,
        GlobalOptions globals)
    {
        var id = new Argument<string?>("id")
        {
            Description = "Session id to stop.",
            Arity = ArgumentArity.ZeroOrOne,
        }.WithInput(InputKind.None);
        var all = new Option<bool>("--all") { Description = "Stop every current-user preview session." };
        var stop = new Command("stop", "Gracefully stop one or all background previews.");
        stop.Arguments.Add(id);
        stop.Options.Add(all);
        stop.SetAction(parse => executor.RunLightweight(parse, globals, (_, _) =>
        {
            PreviewStopState state = new PreviewServiceController().Stop(
                parse.GetValue(id), parse.GetValue(all));
            return new ProductPreviewStopResult
            {
                Stopped = state.Stopped,
                Sessions = state.Sessions.Select(ToInfo).ToArray(),
            };
        }));
        return stop;
    }

    private static ProductPreviewStartResult ToResult(PreviewStartState state) => new()
    {
        Id = state.Session.Id,
        Product = state.Session.Product,
        Url = state.Session.Url,
        Pid = state.Session.Pid,
        File = state.Session.File,
        View = state.Session.View,
        Selector = state.Session.Selector,
        Reused = state.Reused,
        License = state.Metadata.License,
        Warnings = state.Metadata.Warnings,
    };

    private static ProductPreviewSessionInfo ToInfo(PreviewSessionState state) => new()
    {
        Id = state.Id,
        Product = state.Product,
        Url = state.Url,
        Pid = state.Pid,
        File = state.File,
        View = state.View,
        Selector = state.Selector,
        Revision = state.Revision,
        State = state.State,
    };

    private sealed record StartSymbols(
        Option<int> Port,
        Option<string?> Product,
        Option<string> View,
        Option<bool> Open,
        PasswordOptions Password,
        FontDirectoryOptions Fonts)
    {
        public static StartSymbols Create(
            ProductCatalog catalog,
            string passwordTarget)
        {
            var port = new Option<int>("--port")
            {
                Description = "Loopback port; 0 chooses a free port.",
                DefaultValueFactory = _ => 0,
            };
            var product = new Option<string?>("--product")
            {
                Description = "Explicit product override; normally inferred from the file extension.",
            }.WithInput(InputKind.None);
            product.AcceptOnlyFromAmong(
                catalog.Products
                    .Select(static item => item.Manifest.Id)
                    .ToArray());
            var view = new Option<string>("--view")
            {
                Description = "Preview view; auto uses the product default.",
                DefaultValueFactory = _ => AutoView,
            }.WithInput(InputKind.None);
            view.AcceptOnlyFromAmong(
                catalog.Products
                    .SelectMany(static item => item.Preview.Views)
                    .Append(AutoView)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray());
            var open = new Option<bool>("--open") { Description = "Open the preview URL in the default browser." };
            return new StartSymbols(
                port,
                product,
                view,
                open,
                new PasswordOptions("--password", passwordTarget),
                new FontDirectoryOptions());
        }

        public void AddTo(Command command)
        {
            command.Options.Add(Port);
            command.Options.Add(Product);
            command.Options.Add(View);
            command.Options.Add(Open);
            Password.AddTo(command);
            Fonts.AddTo(command);
        }
    }
}
