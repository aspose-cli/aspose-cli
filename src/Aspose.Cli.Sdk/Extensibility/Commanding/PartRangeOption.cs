using System.CommandLine;
using System.Runtime.CompilerServices;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Sdk.Extensibility.Commanding;

/// <summary>
/// The one <c>--{part}s</c> range option, such as <c>--pages</c>: the parts a
/// command processes, in the shared range syntax and the standard wording, with the command's
/// own default and precondition.
/// </summary>
public sealed class PartRangeOption
{
    // Each option, with the outputs for which its selection keeps the engine from reading the parts it leaves out.
    private static readonly ConditionalWeakTable<Option, Narrowing> Narrowings = [];

    private readonly Option<string?> _range;

    /// <summary>Creates the option.</summary>
    /// <param name="part">The part noun, such as <c>page</c>; the option is named after its plural.</param>
    /// <param name="defaultText">What the command processes when the option is omitted, such as <c>every page</c>.</param>
    /// <param name="onlyWith">The condition the option needs, such as <c>--by pages</c>, or null.</param>
    /// <param name="narrowsReads">
    /// Whether the engine reads only the selected parts when the command writes an output in the
    /// given format, so a selection within an evaluation limit keeps the command within it and the
    /// SDK may offer one; false for an output made by removing the other parts from the document,
    /// which reads them. Null when the engine reads only the selected parts whatever the command writes.
    /// </param>
    public PartRangeOption(string part, string defaultText, string? onlyWith = null, Func<FormatDescriptor, bool>? narrowsReads = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(part);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultText);
        string parts = part + "s";
        _range = new Option<string?>("--" + parts)
        {
            Description = $"{char.ToUpperInvariant(parts[0])}{parts[1..]} to process: 1-based numbers and ranges such as 1-3,7,9-, or all. "
                + $"Default: {defaultText}."
                + (onlyWith is null ? string.Empty : $" Only with {onlyWith}."),
        }.WithInput(InputKind.None);
        Options = [_range];
        Narrowings.Add(_range, new Narrowing(narrowsReads));
    }

    /// <summary>
    /// The selection option of <paramref name="command"/> over <paramref name="unit"/>, such as
    /// <c>--pages</c> for <c>pages</c>, when a selection keeps the engine from reading the parts it
    /// leaves out of the output in <paramref name="output"/>'s format; null otherwise.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="unit">The unit of a limit, such as <c>pages</c>.</param>
    /// <param name="output">The format of the command's output, asked only of an option that narrows the reads of some outputs.</param>
    internal static string? NarrowingOption(Command command, string unit, Func<FormatDescriptor> output) =>
        command.Options.FirstOrDefault(option => option.Name == "--" + unit) is { } option
            && Narrowings.TryGetValue(option, out Narrowing? narrowing)
            && (narrowing.ForOutput is null || narrowing.ForOutput(output()))
            ? option.Name
            : null;

    /// <summary>The option name, such as <c>--pages</c>.</summary>
    public string Name => _range.Name;

    /// <summary>
    /// What to do instead of a selection that is not one, such as naming the part through another
    /// option; it leads the hint of the error. Null for the selection syntax alone.
    /// </summary>
    public string? InvalidHint { get; init; }

    /// <summary>The option, for a product command's option list.</summary>
    public IReadOnlyList<Option> Options { get; }

    /// <summary>The range text as given, or null when the option is omitted.</summary>
    public string? Read(ParseResult parse)
    {
        ArgumentNullException.ThrowIfNull(parse);
        return parse.GetValue(_range);
    }

    /// <summary>The parsed range, or null when the option is omitted.</summary>
    /// <exception cref="Errors.CliException"><c>OPTION_INVALID</c> when the value is not a selection.</exception>
    public PageRange? ReadRange(ParseResult parse) =>
        Read(parse) is { } text ? PageRange.ParseOption(Name, text, InvalidHint) : null;

    /// <summary>The outputs for which an option's selection narrows the engine's reads; null for every output.</summary>
    private sealed record Narrowing(Func<FormatDescriptor, bool>? ForOutput);
}
