using System.CommandLine;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Host.Viewer;
using Aspose.Cli.Host.ViewerService;

namespace Aspose.Cli.Host.Commands;

/// <summary>
/// Hidden warm renderer behind the local viewer service. It speaks the frame
/// protocol on standard input and output, so it bypasses the result envelope
/// every other command writes.
/// </summary>
internal static class RenderWorkerCommand
{
    public static Command Create(ProductCatalog catalog, GlobalOptions globals)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(globals);
        var command = new Command(
            RenderWorkerProtocol.CommandName,
            "Serve view render requests for the local viewer service.")
        {
            Hidden = true,
        };
        command.SetAction(parse => ViewRenderWorker.Run(catalog, globals.Resolve(parse)));
        return command.WithInvocationPolicy(new CommandInvocationPolicy(Execution: CommandExecutionOwnership.Service));
    }
}
