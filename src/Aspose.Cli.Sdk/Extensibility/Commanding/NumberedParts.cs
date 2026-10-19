using System.CommandLine;
using System.Runtime.CompilerServices;

namespace Aspose.Cli.Sdk.Extensibility.Commanding;

/// <summary>
/// The commands that write their output as numbered parts (<see cref="IO.Output.ResolvedOutput.Parts"/>,
/// <see cref="IO.Output.ResolvedDirectory.Parts"/>), known by the option that selects the parts, so a
/// dry run can name the files the run writes without reading the document.
/// </summary>
internal static class NumberedParts
{
    private static readonly ConditionalWeakTable<Option, Func<ParseResult, IReadOnlyList<int>?>> Readers = [];

    /// <summary>Declares that the command with <paramref name="option"/> numbers the parts it writes.</summary>
    /// <param name="option">The option that selects or divides the parts.</param>
    /// <param name="numbers">
    /// The part numbers the command line names, or null when they depend on the document,
    /// such as every page of it.
    /// </param>
    internal static void Declare(Option option, Func<ParseResult, IReadOnlyList<int>?> numbers) =>
        Readers.AddOrUpdate(option, numbers);

    /// <summary>
    /// The part numbers of the invoked command: null when it does not number its parts;
    /// otherwise the numbers its command line names, or an empty list when they depend on the document.
    /// </summary>
    internal static IReadOnlyList<int>? Of(ParseResult parse)
    {
        ArgumentNullException.ThrowIfNull(parse);
        foreach (Option option in parse.CommandResult.Command.Options)
        {
            if (Readers.TryGetValue(option, out Func<ParseResult, IReadOnlyList<int>?>? read))
            {
                return read(parse) ?? [];
            }
        }

        return null;
    }
}
