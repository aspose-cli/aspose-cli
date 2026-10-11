using System.Text.Json.Nodes;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.Invariants.Tests;

/// <summary>What a same-name option must share with every other option of its name.</summary>
[Flags]
internal enum OptionAspect
{
    None = 0,

    /// <summary>The value type and the minimum and maximum arity.</summary>
    Arity = 1,
    AllowedValues = 2,
    Default = 4,
    Required = 8,
    Secret = 16,

    /// <summary>Where the value comes from: the command line, an environment variable name or stdin.</summary>
    ValueSource = 32,
}

/// <summary>
/// An option name whose commands may differ in some aspects, as C-R07 allows, with the reason.
/// With <see cref="Command"/>, only that command's option is left out of the comparison.
/// </summary>
internal sealed record SameNameExemption(string Option, OptionAspect Aspects, string Reason, string? Command = null);

/// <summary>
/// The invariant <see cref="InvariantCases.SameNameOption"/>, the check of contract rule C-R07:
/// across every command of the tree, products and host commands alike, hidden ones included,
/// options that share a name share their value type and arity, allowed values, default, whether
/// they are required, whether they are secret, and where their value comes from, so an agent that
/// learned an option once can rely on it everywhere. Descriptions are never compared: wording is
/// not behavior. A shared option reads one definition (the SDK option groups); any other repeated
/// name is kept consistent by hand. The only differences allowed are the <see cref="Exemptions"/>,
/// each one C-R07 permits (<see cref="RulePermits"/>) and each with its reason; an exemption that
/// no longer matches a difference fails, so the list only shrinks.
/// </summary>
internal static class SameNameOptions
{
    /// <summary>The names whose allowed values, and so defaults, C-R07 lets differ from product to product.</summary>
    public static IReadOnlySet<string> ProductValues { get; } =
        new HashSet<string>(["--to", "--detail", "--what", "--content", "--scope", "--by"], StringComparer.Ordinal);

    /// <summary>
    /// The differences C-R07 permits: the values of <see cref="ProductValues"/> and their defaults
    /// (a default is one of the values), and a required
    /// <c>--out</c> on a merge, which has no single input to name a default output after.
    /// </summary>
    public static IReadOnlyList<SameNameExemption> Exemptions { get; } =
    [
        new("--detail", OptionAspect.AllowedValues,
            "C-R07: each product's inspect lists its own sections. Kept on purpose: slides media covers audio and video, "
            + "so it stays apart from the images of words."),
        new("--scope", OptionAspect.AllowedValues | OptionAspect.Default,
            "C-R07: the part of the document a command reads or searches is the product's own, and so is its default."),
        new("--to", OptionAspect.AllowedValues,
            "C-R07: each convert, extract and render lists its own product's formats (C-R36 checks the lists)."),
        new("--what", OptionAspect.AllowedValues,
            "C-R07: each product extracts its own part kinds. Kept on purpose: slides media covers audio and video, "
            + "so it stays wider than the images of pdf and words."),
        new("--out", OptionAspect.Required,
            "C-R07: merge has no single input to name a default output after, so its --out is required.",
            "pdf merge"),
    ];

    /// <summary>Whether C-R07 permits the difference an exemption allows.</summary>
    public static bool RulePermits(SameNameExemption exemption) => exemption.Aspects switch
    {
        OptionAspect.AllowedValues or OptionAspect.Default or (OptionAspect.AllowedValues | OptionAspect.Default) =>
            exemption.Command is null && ProductValues.Contains(exemption.Option),
        OptionAspect.Required => exemption.Option == "--out" && exemption.Command?.Split(' ')[^1] == "merge",
        _ => false,
    };

    /// <summary>Every aspect compared; descriptions are not one of them.</summary>
    public static IReadOnlyList<OptionAspect> Compared { get; } =
    [
        OptionAspect.Arity, OptionAspect.AllowedValues, OptionAspect.Default, OptionAspect.Required, OptionAspect.Secret,
        OptionAspect.ValueSource,
    ];

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
            yield return new InvariantCase(id, InvariantCases.SameNameOption, InvariantCases.Contract, Slow: false,
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
        OptionAspect.Required => "required flags",
        OptionAspect.Secret => "secret flags",
        OptionAspect.ValueSource => "value sources",
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
        OptionAspect.Required => Option["required"]!.GetValue<bool>() ? "required" : "optional",
        OptionAspect.Secret => Option["secret"]!.GetValue<bool>() ? "secret" : "not secret",
        OptionAspect.ValueSource => Option["valueSource"]!.GetValue<string>(),
        _ => throw new ArgumentOutOfRangeException(nameof(aspect)),
    };
}

/// <summary>
/// The exemption list itself stays honest: each entry names a repeated option, a difference C-R07
/// permits and its reason; and the comparison never reads a description.
/// </summary>
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
            Assert.True(SameNameOptions.RulePermits(exemption),
                $"C-R07 does not let {exemption.Option} differ in {exemption.Aspects}{(exemption.Command is null ? string.Empty : " on " + exemption.Command)}; "
                + "the difference is a violation, not an exemption.");
        }
    }

    [Fact]
    public void DescriptionsAreNotCompared()
    {
        foreach (DeclaredOption option in SameNameOptions.Declared(CliCatalog.Current).Values.SelectMany(static options => options))
        {
            JsonNode reworded = option.Option.DeepClone();
            reworded["description"] = "Reworded " + (option.Option["description"]?.GetValue<string>() ?? string.Empty);
            var other = new DeclaredOption(option.Command, reworded);
            Assert.All(SameNameOptions.Compared, aspect => Assert.Equal(option.Read(aspect), other.Read(aspect)));
        }
    }
}
