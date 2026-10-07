using System.CommandLine;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Host.ViewerService;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Rendering;

namespace Aspose.Cli.Host.Commands;

/// <summary>
/// Opens a file in the local viewer and keeps it live. The command itself
/// renders nothing: it hands the file to the per-user viewer service, which
/// watches it and re-renders it through its warm worker whenever it changes.
/// </summary>
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
            Description = "File to preview; content detection selects the product unless --product is supplied.",
            Arity = ArgumentArity.ZeroOrOne,
        }.WithInput(InputKind.File);
        StartSymbols symbols = StartSymbols.Create(catalog);
        Command preview = symbols.Standard.CreateCommand(
            "preview",
            "Watch a file in the local viewer and follow every change.",
            [file, symbols.Port, symbols.Product, symbols.View, symbols.Open, symbols.Effect]);
        preview.Subcommands.Add(CreateStatus(executor, globals));
        preview.Subcommands.Add(CreateStop(executor, globals));
        preview.SetAction(parse => executor.Run(parse, globals, context =>
            Start(parse, context, file, symbols)));
        return preview.WithInvocationPolicy(new CommandInvocationPolicy(Execution: CommandExecutionOwnership.Service));
    }

    private static ProductPreviewStartResult Start(
        ParseResult parse,
        CommandContext context,
        Argument<string?> file,
        StartSymbols symbols)
    {
        if (parse.GetValue(file) is not { Length: > 0 } requested)
        {
            throw CliErrors.OptionInvalid(
                "file",
                "no file was given",
                "Run 'aspose-cli preview <file>'.");
        }
        int port = parse.GetValue(symbols.Port);
        OptionGuards.EnsureInRange(
            "--port", port, 0, MaxPort,
            "Pass --port 0 for a system-assigned port, or a port from 1 to 65535.");
        string input = context.Paths.ResolveInput(requested);
        string view = parse.GetValue(symbols.View) ?? AutoView;
        StandardInvocation standard = symbols.Standard.Bind(
            parse, context.Paths, context.ResourceBudgets.Inputs, context.ReadEnvironment);
        FontSearchProfile fonts = standard.FontDirectories;
        ViewerOpenResponse opened = new ViewerServiceClient().Open(
            context.Globals,
            new ViewerOpenRequest
            {
                File = input,
                MaxInputBytes = context.Globals.MaxInputBytes,
                Product = parse.GetValue(symbols.Product),
                View = view == AutoView ? null : view,
                Effect = parse.GetValue(symbols.Effect),
                Password = standard.InputPassword,
                License = context.Globals.LicensePath is { } license
                    ? Path.GetFullPath(license, context.Paths.BaseDirectory)
                    : null,
                EvaluationRequested = context.Globals.EvaluationRequested,
                FontDirectories = fonts.IsAmbient ? null : fonts.Directories,
            },
            port,
            context.Deadline);
        if (parse.GetValue(symbols.Open))
        {
            BrowserLauncher.Open(opened.Document.Url);
        }
        return new ProductPreviewStartResult
        {
            Id = opened.Document.Id,
            Product = opened.Document.Product,
            Url = opened.Document.Url,
            Pid = opened.Pid,
            File = opened.Document.File,
            View = opened.Document.View,
            Reused = opened.Reused,
            License = opened.Document.License is { Length: > 0 } mode
                ? new LicenseInfo { Mode = mode }
                : null,
        };
    }

    private static Command CreateStatus(CommandExecutor executor, GlobalOptions globals)
    {
        var id = new Argument<string?>("id")
        {
            Description = "Optional document id.",
            Arity = ArgumentArity.ZeroOrOne,
        }.WithInput(InputKind.None);
        var status = new Command("status", "List the documents the local viewer has open.");
        status.Arguments.Add(id);
        status.SetAction(parse => executor.RunLightweight(parse, globals, (_, _) =>
        {
            ViewerStatusResponse? state = new ViewerServiceClient().Status();
            string? selected = parse.GetValue(id);
            return new ProductPreviewStatusResult
            {
                Sessions = (state?.Documents ?? [])
                    .Where(document => selected is null || document.Id == selected)
                    .Select(document => ToInfo(document, state!.Pid))
                    .ToArray(),
            };
        }));
        return status.WithInvocationPolicy(new CommandInvocationPolicy(McpAllowed: true));
    }

    private static Command CreateStop(CommandExecutor executor, GlobalOptions globals)
    {
        var id = new Argument<string?>("id")
        {
            Description = "Document id to close.",
            Arity = ArgumentArity.ZeroOrOne,
        }.WithInput(InputKind.None);
        var all = new Option<bool>("--all")
        {
            Description = "Close every document and stop the viewer service.",
        };
        var stop = new Command("stop", "Close one document, or stop the local viewer service.");
        stop.Arguments.Add(id);
        stop.Options.Add(all);
        stop.SetAction(parse => executor.RunLightweight(parse, globals, (_, _) =>
        {
            var client = new ViewerServiceClient();
            ViewerStopResponse? state = client.Stop(parse.GetValue(id), parse.GetValue(all));
            int pid = state?.Pid ?? 0;
            return new ProductPreviewStopResult
            {
                Stopped = state?.Stopped ?? [],
                Sessions = (state?.Documents ?? []).Select(document => ToInfo(document, pid)).ToArray(),
            };
        }));
        return stop;
    }

    private static ProductPreviewSessionInfo ToInfo(ViewerDocumentState document, int pid) => new()
    {
        Id = document.Id,
        Product = document.Product,
        Url = document.Url,
        Pid = pid,
        File = document.File,
        View = document.View,
        Revision = document.Revision,
    };

    private sealed record StartSymbols(
        Option<int> Port,
        Option<string?> Product,
        Option<string> View,
        Option<bool> Open,
        Option<string?> Effect,
        StandardOptions Standard)
    {
        public static StartSymbols Create(ProductCatalog catalog)
        {
            var port = new Option<int>("--port")
            {
                Description = "Loopback port of the viewer service when it starts; 0 chooses a free port.",
                DefaultValueFactory = _ => 0,
            };
            var product = new Option<string?>("--product")
            {
                Description = "Product that reads the file; by default, the one whose format the file's content matches.",
            }.WithInput(InputKind.None);
            product.AcceptOnlyFromAmong(
                catalog.Products.Select(static item => item.Manifest.Id).ToArray());
            var view = new Option<string>("--view")
            {
                Description = "Product view; auto uses the product's live view.",
                DefaultValueFactory = _ => AutoView,
            }.WithInput(InputKind.None);
            view.AcceptOnlyFromAmong(
                catalog.Products
                    .SelectMany(static item => item.View.Views)
                    .Select(static item => item.Id)
                    .Append(AutoView)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray());
            var open = new Option<bool>("--open") { Description = "Open the viewer in the default browser." };
            var effect = new Option<string?>("--fx")
            {
                Description = "Presentation effect for live demonstrations (for example 'demo').",
            }.WithInput(InputKind.None);
            return new StartSymbols(
                port,
                product,
                view,
                open,
                effect,
                new StandardOptions(new CommandTraits { PasswordSubject = "the file", UsesFonts = true }));
        }
    }
}
