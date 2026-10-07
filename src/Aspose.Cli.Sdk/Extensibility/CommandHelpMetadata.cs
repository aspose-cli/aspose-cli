using System.CommandLine;
using System.Runtime.CompilerServices;

namespace Aspose.Cli.Sdk.Extensibility;

/// <summary>A command and description shown in a help page's learning links.</summary>
/// <param name="Command">A complete command line that can be copied.</param>
/// <param name="Description">A short description of the referenced material.</param>
public sealed record CommandHelpLink(
    string Command,
    string Description)
{
    /// <summary>Links one of a product's documentation topics, such as its <c>editing</c> topic.</summary>
    /// <param name="product">The product whose topic is linked.</param>
    /// <param name="topic">The topic name within the product.</param>
    /// <param name="description">A short description of the topic.</param>
    public static CommandHelpLink Docs(ProductManifest product, string topic, string description)
    {
        ArgumentNullException.ThrowIfNull(product);
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        return new($"{DistributionInfo.CommandName} docs {product.Id}/{topic}", description);
    }

    /// <summary>Links the JSON schema of a product's operation vocabulary.</summary>
    /// <param name="product">The product, which declares exactly one operation vocabulary.</param>
    /// <param name="description">A short description of the schema.</param>
    public static CommandHelpLink Schema(ProductManifest product, string description)
    {
        ArgumentNullException.ThrowIfNull(product);
        return new($"{DistributionInfo.CommandName} schema {product.Operations.Single().Descriptor.InputSchema}", description);
    }
}

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
    /// host without knowing the product. An example is written after the executable
    /// name, which is prepended here.
    /// </summary>
    /// <param name="command">The command receiving the metadata.</param>
    /// <param name="examples">Copyable command lines after the executable name.</param>
    /// <param name="learnMore">Optional documentation and schema links.</param>
    /// <returns>The same command instance.</returns>
    public static Command WithExamples(
        this Command command,
        IEnumerable<string> examples,
        IEnumerable<CommandHelpLink>? learnMore = null)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(examples);

        string executable = DistributionInfo.CommandName + " ";
        var value = new CommandHelpMetadata(
            examples.Select(example => executable + example).ToArray(),
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
