using System.CommandLine;
using System.Runtime.CompilerServices;

namespace Aspose.Cli.Sdk.Extensibility;

/// <summary>A command and description shown in a help page's learning links.</summary>
/// <param name="Command">A complete command line that can be copied.</param>
/// <param name="Description">A short description of the referenced material.</param>
public sealed record CommandHelpLink(
    string Command,
    string Description);

/// <summary>Product-owned additions to the standard command help output.</summary>
/// <param name="Examples">Complete command lines shown as examples.</param>
/// <param name="LearnMore">Related documentation or schema commands.</param>
public sealed record CommandHelpMetadata(
    IReadOnlyList<string> Examples,
    IReadOnlyList<CommandHelpLink> LearnMore);

/// <summary>Associates immutable help metadata with a command definition.</summary>
public static class CommandHelpExtensions
{
    private static readonly ConditionalWeakTable<Command, CommandHelpMetadata>
        Metadata = new();

    /// <summary>
    /// Attaches examples and optional learning links to a command. Metadata is
    /// owned by the assembly that defines the command and can be read by any
    /// host without knowing the product.
    /// </summary>
    /// <param name="command">The command receiving the metadata.</param>
    /// <param name="examples">Complete, copyable command lines.</param>
    /// <param name="learnMore">Optional documentation and schema links.</param>
    /// <returns>The same command instance.</returns>
    public static Command WithExamples(
        this Command command,
        IEnumerable<string> examples,
        IEnumerable<CommandHelpLink>? learnMore = null)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(examples);

        var value = new CommandHelpMetadata(
            examples.ToArray(),
            learnMore?.ToArray() ?? []);
        Metadata.Remove(command);
        Metadata.Add(command, value);
        return command;
    }

    /// <summary>Returns the metadata attached by <see cref="WithExamples"/>.</summary>
    /// <param name="command">The command to inspect.</param>
    /// <param name="metadata">The attached metadata, when present.</param>
    /// <returns><see langword="true"/> when metadata is attached.</returns>
    public static bool TryGetHelpMetadata(
        this Command command,
        out CommandHelpMetadata? metadata)
    {
        ArgumentNullException.ThrowIfNull(command);
        return Metadata.TryGetValue(command, out metadata);
    }
}
