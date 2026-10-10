using System.Text.Json.Nodes;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Invariants;

/// <summary>What a same-name option must share with every other option of its name.</summary>
[Flags]
internal enum OptionAspect
{
    None = 0,

    /// <summary>The value type and the minimum and maximum arity.</summary>
    Arity = 1,
    AllowedValues = 2,
    Default = 4,
}

/// <summary>
/// An option name whose commands may differ in some aspects, with the reason read from today's
/// help. With <see cref="Command"/>, only that command's option is left out of the comparison.
/// </summary>
internal sealed record SameNameExemption(string Option, OptionAspect Aspects, string Reason, string? Command = null);

/// <summary>
/// The invariant <see cref="InvariantCases.SameNameOption"/>: across every command of the tree,
/// products and host commands alike, hidden ones included, options that share a name share their
/// value type and arity, allowed values and default, so an agent that learned an option once can
/// rely on it everywhere. A shared option reads one definition (the SDK option groups); any other
/// repeated name is kept consistent by hand. The only differences allowed are the
/// <see cref="Exemptions"/>, each with its reason; an exemption that no longer matches a
/// difference fails, so the list only shrinks.
/// </summary>
internal static class SameNameOptions
{
    /// <summary>
    /// The names that mean different things on different commands, or whose values the command
    /// owns: the smallest list that lets every other repeated name be one definition.
    /// </summary>
    public static IReadOnlyList<SameNameExemption> Exemptions { get; } =
    [
        new("--scope", OptionAspect.AllowedValues | OptionAspect.Default,
            "Three meanings: the projection a query returns (cells query range, slides query slides, words query blocks), "
            + "where a search looks (cells, slides and words query search) and where skill install writes (project or user). "
            + "A rename is left to S3."),
        new("--what", OptionAspect.AllowedValues,
            "The part kinds each product extracts differ (pdf: attachments, forms; slides: media, notes; words: comments). "
            + "A rename is left to S3."),
        new("--to", OptionAspect.AllowedValues,
            "Each convert, extract and render writes its own product's formats; S1 keeps each list."),
        new("--detail", OptionAspect.AllowedValues,
            "Each product's inspect has its own detail sections; S1 keeps them."),
        new("--view", OptionAspect.AllowedValues,
            "preview open also offers the live workbook view, which review does not render; S1 keeps both lists."),
        new("--product", OptionAspect.Default,
            "Three roles: the font engine fonts list inspects (default cells), the product a license command acts on, and "
            + "the product that reads the file in preview open and review."),
        new("--pages", OptionAspect.Arity,
            "pdf split --pages takes one or more page groups (repeatable list), not one page range.",
            "pdf split"),
    ];

    private static readonly OptionAspect[] Compared = [OptionAspect.Arity, OptionAspect.AllowedValues, OptionAspect.Default];

    /// <summary>One case per option name that two or more commands declare.</summary>
    public static IEnumerable<InvariantCase> Cases(CliCatalog catalog)
    {
        Dictionary<string, List<DeclaredOption>> byName = Declared(catalog);
        foreach ((string name, List<DeclaredOption> declared) in byName.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            if (declared.Count < 2)
            {
                continue;
            }
            string id = $"{InvariantCases.SameNameOption} | {name}";
            yield return new InvariantCase(id, InvariantCases.SameNameOption, InvariantCases.Platform, Slow: false,
                Scenario: null, Check: () => Problems(name, declared));
        }
    }

    /// <summary>Every option of every command, by name, with the command path after the executable name.</summary>
    public static Dictionary<string, List<DeclaredOption>> Declared(CliCatalog catalog)
    {
        var byName = new Dictionary<string, List<DeclaredOption>>(StringComparer.Ordinal);
        foreach (JsonNode? command in catalog.Document["commands"]!.AsArray())
        {
            string path = command!["path"]!.GetValue<string>();
            string words = path.Contains(' ', StringComparison.Ordinal) ? path[(path.IndexOf(' ', StringComparison.Ordinal) + 1)..] : string.Empty;
            foreach (JsonNode? option in command["options"]!.AsArray())
            {
                string name = option!["name"]!.GetValue<string>();
                if (!byName.TryGetValue(name, out List<DeclaredOption>? list))
                {
                    byName[name] = list = [];
                }
                list.Add(new DeclaredOption(words.Length == 0 ? "aspose-cli" : words, option));
            }
        }
        return byName;
    }

    private static IReadOnlyList<ScenarioProblem> Problems(string name, IReadOnlyList<DeclaredOption> declared)
    {
        SameNameExemption[] exemptions = [.. Exemptions.Where(exemption => exemption.Option == name)];
        var problems = new List<ScenarioProblem>();
        foreach (OptionAspect aspect in Compared)
        {
            bool exempt = exemptions.Any(exemption => exemption.Command is null && exemption.Aspects.HasFlag(aspect));
            DeclaredOption[] compared =
            [
                .. declared.Where(option => !exemptions.Any(exemption =>
                    exemption.Command == option.Command && exemption.Aspects.HasFlag(aspect))),
            ];
            IGrouping<string, DeclaredOption>[] variants = [.. compared.GroupBy(option => option.Read(aspect), StringComparer.Ordinal)];
            if (!exempt && variants.Length > 1)
            {
                problems.Add(Problem(
                    $"{name}: {variants.Length} different {Describe(aspect)}: "
                    + string.Join(" | ", variants.Select(static variant =>
                        $"{variant.Key} ({string.Join(", ", variant.Select(static option => option.Command))})"))));
            }
        }
        foreach (SameNameExemption exemption in exemptions)
        {
            foreach (OptionAspect aspect in Compared.Where(candidate => exemption.Aspects.HasFlag(candidate)))
            {
                // A command-wide exemption is needed while the commands differ; one command's while
                // its option differs from every other command's.
                bool differs = exemption.Command is null
                    ? declared.Select(option => option.Read(aspect)).Distinct(StringComparer.Ordinal).Count() > 1
                    : declared.Where(option => option.Command == exemption.Command).Select(option => option.Read(aspect))
                        .Except(declared.Where(option => option.Command != exemption.Command).Select(option => option.Read(aspect)), StringComparer.Ordinal)
                        .Any();
                if (!differs)
                {
                    problems.Add(Problem(
                        $"{name}{(exemption.Command is null ? string.Empty : " on " + exemption.Command)}: the exemption for "
                        + $"{Describe(aspect)} no longer matches a difference; delete it from {nameof(SameNameOptions)}.{nameof(Exemptions)}."));
                }
            }
        }
        return problems;
    }

    private static ScenarioProblem Problem(string message) => new(0, InvariantCases.SameNameOption, message);

    private static string Describe(OptionAspect aspect) => aspect switch
    {
        OptionAspect.Arity => "types or arities",
        OptionAspect.AllowedValues => "allowed-value sets",
        OptionAspect.Default => "defaults",
        _ => aspect.ToString(),
    };
}

/// <summary>One option as capabilities declares it on one command.</summary>
internal sealed record DeclaredOption(string Command, JsonNode Option)
{
    public string Read(OptionAspect aspect) => aspect switch
    {
        OptionAspect.Arity =>
            $"{Option["type"]!.GetValue<string>()} {Option["minimumArity"]!.GetValue<int>()}..{Option["maximumArity"]!.GetValue<int>()}",
        OptionAspect.AllowedValues =>
            "[" + string.Join(", ", Option["allowedValues"]!.AsArray().Select(static value => value!.GetValue<string>()).Order(StringComparer.Ordinal)) + "]",
        OptionAspect.Default => Option["hasDefault"]?.GetValue<bool>() == true
            ? "\"" + (Option["default"]?.GetValue<string>() ?? string.Empty) + "\""
            : "none",
        _ => throw new ArgumentOutOfRangeException(nameof(aspect)),
    };
}

/// <summary>The exemption list itself stays honest: each entry names a repeated option with its reason.</summary>
public sealed class SameNameExemptionTests
{
    [Fact]
    public void EveryExemptionNamesARepeatedOptionWithAReason()
    {
        Dictionary<string, List<DeclaredOption>> declared = SameNameOptions.Declared(CliCatalog.Current);
        foreach (SameNameExemption exemption in SameNameOptions.Exemptions)
        {
            Assert.True(declared.TryGetValue(exemption.Option, out List<DeclaredOption>? options) && options.Count > 1,
                $"{exemption.Option} is no longer declared by two or more commands; delete its exemption.");
            Assert.True(exemption.Command is null || options!.Any(option => option.Command == exemption.Command),
                $"{exemption.Command} no longer declares {exemption.Option}; delete its exemption.");
            Assert.True(exemption.Aspects != OptionAspect.None && exemption.Reason.Length > 20,
                $"The exemption of {exemption.Option} names what may differ and why.");
        }
    }
}
