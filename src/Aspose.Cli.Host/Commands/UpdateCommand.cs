using System.CommandLine;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Host.Updating;

namespace Aspose.Cli.Host.Commands;

/// <summary>Provides explicit, user-invoked release update operations.</summary>
internal static class UpdateCommand
{
    public static Command Create(
        HostContext host, CommandExecutor executor,
        GlobalOptions globals)
    {
        var update = new Command("update", "Check or install a CLI release; no background checks are performed.");
        update.Subcommands.Add(CreateCheck(executor, globals));
        update.Subcommands.Add(CreateInstall(host, executor, globals));
        update.Subcommands.Add(CreatePreparation(host, executor, globals));
        return update;
    }

    private static Command CreateCheck(CommandExecutor executor, GlobalOptions globals)
    {
        var command = new Command("check", "Check whether a newer release is available.");
        Argument<string> feed = Feed();
        command.Arguments.Add(feed);
        command.SetAction(parse => executor.Run(parse, globals, context =>
            UpdateClient.Check(context, parse.GetValue(feed)!)));
        return command;
    }

    private static Command CreateInstall(HostContext host, CommandExecutor executor, GlobalOptions globals)
    {
        var command = new Command("install", "Install a newer release after checking its archive against the release manifest.");
        Argument<string> feed = Feed();
        command.Arguments.Add(feed);
        command.SetAction(parse => executor.RunHandoff(parse, globals, context =>
            UpdateInstaller.Install(host, context, parse.GetValue(feed)!)));
        return command.WithInvocationPolicy(new CommandInvocationPolicy(Execution: CommandExecutionOwnership.ParentHandoff));
    }

    private static Command CreatePreparation(HostContext host, CommandExecutor executor, GlobalOptions globals)
    {
        var command = new Command(UpdateInstaller.PreparationCommand, "Prepare a verified update for its owning parent.") { Hidden = true };
        var feed = new Argument<string>("feed").WithInput(InputKind.None);
        var target = new Argument<string>("target").WithInput(InputKind.None);
        command.Arguments.Add(feed);
        command.Arguments.Add(target);
        command.SetAction(parse => executor.Run(parse, globals, context =>
        {
            if (host.WorkerOutputs is null || InvocationInputs.Current is null)
            {
                throw Aspose.Cli.Sdk.Errors.CliErrors.Usage(["Update preparation requires an owned worker invocation."]);
            }
            return UpdateClient.Prepare(context, parse.GetRequiredValue(feed), parse.GetRequiredValue(target));
        }));
        return command.WithInvocationPolicy(new CommandInvocationPolicy(
            OutputBytesLimit: UpdateClient.MaximumArchiveBytes));
    }

    private static Argument<string> Feed() => new Argument<string>("feed")
    {
        Description = "RELEASE-MANIFEST.json, as a local path or an HTTPS URL.",
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = static _ => UpdateClient.DefaultFeed,
    }.WithInput(InputKind.None);
}
