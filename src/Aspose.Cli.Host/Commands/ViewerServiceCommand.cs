using System.CommandLine;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Host.ViewerService;

namespace Aspose.Cli.Host.Commands;

/// <summary>
/// Hidden host of the viewer service. Commands start it through
/// <see cref="ViewerServiceClient"/>; it is never run by hand.
/// </summary>
internal static class ViewerServiceCommand
{
    public static Command Create(CommandExecutor executor, GlobalOptions globals)
    {
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(globals);
        var port = new Option<int>("--port")
        {
            Description = "Loopback port; 0 chooses a free port.",
            DefaultValueFactory = _ => 0,
        };
        var command = new Command(
            ViewerServiceCommands.CommandName,
            "Host the local viewer service.")
        {
            Hidden = true,
        };
        command.Options.Add(port);
        command.SetAction(parse =>
        {
            ServiceStartSecrets? secrets = ServiceStartSecretChannel.TryReceive();
            using IDisposable? scope = secrets is null ? null : ServiceStartSecretChannel.Push(secrets);
            return executor.RunHosted(
                parse,
                globals,
                values => ViewerServiceHost.Start(values, secrets, parse.GetValue(port)));
        });
        return command.WithInvocationPolicy(new CommandInvocationPolicy(ServiceLifetime: true));
    }
}
