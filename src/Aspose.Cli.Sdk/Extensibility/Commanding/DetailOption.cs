using System.CommandLine;

namespace Aspose.Cli.Sdk.Extensibility.Commanding;

/// <summary>
/// The one <c>--detail</c> option of an inspect command: extra sections the result includes,
/// repeatable, among the product's section names, in the standard wording generated from them.
/// </summary>
public sealed class DetailOption
{
    private readonly Option<string[]> _detail;

    /// <summary>Creates the option.</summary>
    /// <param name="values">The section names, in the order help lists them.</param>
    /// <param name="notes">What a section holds when its name does not say, by section name.</param>
    public DetailOption(IReadOnlyList<string> values, IReadOnlyDictionary<string, string>? notes = null)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
        {
            throw new ArgumentException("A detail option offers at least one section.", nameof(values));
        }
        if (notes?.Keys.FirstOrDefault(name => !values.Contains(name, StringComparer.Ordinal)) is { } unknown)
        {
            throw new ArgumentException($"The note for '{unknown}' names no section.", nameof(notes));
        }

        IEnumerable<string> listed = values.Select(value =>
            notes is not null && notes.TryGetValue(value, out string? note) ? $"{value} ({note})" : value);
        _detail = new Option<string[]>("--detail")
        {
            Description = $"Extra sections to include; repeat for more: {string.Join(", ", listed)}.",
            AllowMultipleArgumentsPerToken = true,
        }.WithInput(InputKind.None);
        _detail.AcceptOnlyFromAmong([.. values]);
        Options = [_detail];
    }

    /// <summary>The option, for a product command's option list.</summary>
    public IReadOnlyList<Option> Options { get; }

    /// <summary>The requested sections, empty when none is.</summary>
    public string[] Read(ParseResult parse)
    {
        ArgumentNullException.ThrowIfNull(parse);
        return parse.GetValue(_detail) ?? [];
    }
}
