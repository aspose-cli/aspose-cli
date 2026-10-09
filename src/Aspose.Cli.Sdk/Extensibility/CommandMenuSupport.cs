using System.CommandLine;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Sdk.Extensibility;

/// <summary>The examples and links a product or group command shows below its description.</summary>
/// <param name="Examples">Examples, written after the executable name.</param>
/// <param name="Links">Links to further documentation.</param>
public sealed record CommandHelp(IReadOnlyList<string> Examples, IReadOnlyList<CommandHelpLink> Links);

/// <summary>
/// Runs a product's own cross-cutting check around every engine call of its commands and views,
/// such as translating an evaluation-mode limit, and returns what <paramref name="run"/> returns.
/// A lambda cannot be generic, so the value passes as an object; the SDK casts it back.
/// </summary>
/// <param name="session">The product's engine session.</param>
/// <param name="run">The guarded engine call.</param>
public delegate object? ProductGuard<in TSession>(TSession session, Func<object?> run);

/// <summary>What the host gives one invocation of a menu command.</summary>
internal sealed record CommandScope<TSession>(
    ProductBinding<TSession> Binding,
    PathResolver Paths,
    InputSource Inputs,
    Func<string, string?> ReadEnvironment)
    where TSession : class;

/// <summary>Runs one menu command through the host's envelope, output and exit-code pipeline.</summary>
internal delegate int CommandRunner<TSession>(ParseResult parse, Func<CommandScope<TSession>, ResultEnvelope> run)
    where TSession : class;

/// <summary>What every command of one product menu shares when its command tree is built.</summary>
/// <param name="Run">The host pipeline.</param>
/// <param name="Guard">The product guard, or null.</param>
/// <param name="DetectFormat">The product's content format detector, or null.</param>
internal sealed record MenuContext<TSession>(
    CommandRunner<TSession> Run,
    ProductGuard<TSession>? Guard,
    Func<string, string?>? DetectFormat)
    where TSession : class
{
    /// <summary>Calls <paramref name="handler"/> inside the product guard.</summary>
    internal TResult Guarded<TRequest, TResult>(
        Func<TSession, TRequest, TResult> handler,
        TSession session,
        TRequest request) =>
        Guard is null
            ? handler(session, request)
            : (TResult)Guard(session, () => handler(session, request))!;
}

/// <summary>One line of a product menu: a command, or a group of further lines.</summary>
internal interface IMenuEntry<TSession>
    where TSession : class
{
    Command Create(MenuContext<TSession> context);

    /// <summary>The table renderers the entry's commands declare.</summary>
    IEnumerable<ProductOutputDefinition> Outputs();
}

/// <summary>A named group of menu lines, such as <c>query</c>.</summary>
internal sealed class MenuGroup<TSession>(string name, string description, IReadOnlyList<IMenuEntry<TSession>> entries)
    : IMenuEntry<TSession>
    where TSession : class
{
    public Command Create(MenuContext<TSession> context)
    {
        var group = new Command(name, description);
        foreach (IMenuEntry<TSession> entry in entries)
        {
            group.Subcommands.Add(entry.Create(context));
        }

        return group;
    }

    public IEnumerable<ProductOutputDefinition> Outputs() => entries.SelectMany(static entry => entry.Outputs());
}
