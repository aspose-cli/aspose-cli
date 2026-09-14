using System.CommandLine;
using Aspose.Cli.Host.Invocation;

namespace Aspose.Cli.Host.Commands;

/// <summary>Provides explicit, user-invoked release update operations.</summary>
internal static class UpdateCommand
{
    public static Command Create(
        CommandExecutor executor,
        GlobalOptions globals)
    {
        var update = new Command("update", "Check or install a verified CLI release; no background checks are performed.");
        update.Subcommands.Add(CreateCheck(executor, globals));
        update.Subcommands.Add(CreateInstall(executor, globals));
        return update;
    }

    private static Command CreateCheck(CommandExecutor executor, GlobalOptions globals)
    {
        var command = new Command("check", "Check one signed local or HTTPS release feed.");
        var feed = new Argument<string>("feed") { Description = "Path to RELEASE-MANIFEST.json or an HTTPS manifest URL." }.WithInput(InputKind.None);
        command.Arguments.Add(feed);
        command.SetAction(parse => executor.Run(parse, globals, context =>
            UpdateClient.Check(context, parse.GetRequiredValue(feed))));
        return command;
    }

    private static Command CreateInstall(CommandExecutor executor, GlobalOptions globals)
    {
        var command = new Command("install", "Install a verified release from one signed local or HTTPS feed.");
        var feed = new Argument<string>("feed") { Description = "Path to RELEASE-MANIFEST.json or an HTTPS manifest URL." }.WithInput(InputKind.None);
        command.Arguments.Add(feed);
        command.SetAction(parse => executor.Run(parse, globals, context =>
            UpdateClient.Install(context, parse.GetRequiredValue(feed))));
        return command;
    }
}
