using System.CommandLine;

namespace Aspose.Cli.Sdk.Extensibility.Commanding;

/// <summary>
/// The one <c>--detail</c> option of an inspect command: extra sections the result includes,
/// repeatable, among the product's section names, in the standard wording with a note for the
/// sections whose names do not say what they hold.
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

        // Help prints the allowed values after the description, so it names only those with a note.
        string[] noted =
        [
            .. values.Where(value => notes?.ContainsKey(value) == true).Select(value => $"{value}: {notes![value]}"),
        ];
        _detail = new Option<string[]>("--detail")
        {
            Description = "Extra sections to include; repeat for more."
                + (noted.Length == 0 ? string.Empty : $" {string.Join("; ", noted)}."),
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
