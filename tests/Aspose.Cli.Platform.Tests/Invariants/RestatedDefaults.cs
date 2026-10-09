using Aspose.Cli.TestKit.Scenarios;

namespace Aspose.Cli.Platform.Tests.Invariants;

/// <summary>
/// The invariant <see cref="InvariantCases.DefaultNotRestated"/>: help prints <c>[default: X]</c>
/// after the description of every option with a parser default, so such a description must not
/// state the default again (no word "default", in any case). An option without one, such as a
/// boolean flag that defaults to false, keeps stating its default in words. Covers every command
/// of the tree, hidden ones included.
/// </summary>
internal static class RestatedDefaults
{
    /// <summary>One case per option name that some command declares with a parser default.</summary>
    public static IEnumerable<InvariantCase> Cases(CliCatalog catalog)
    {
        foreach ((string name, List<DeclaredOption> declared) in SameNameOptions.Declared(catalog)
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            DeclaredOption[] defaulted = [.. declared.Where(ShowsDefault)];
            if (defaulted.Length == 0)
            {
                continue;
            }
            yield return new InvariantCase($"{InvariantCases.DefaultNotRestated} | {name}", InvariantCases.DefaultNotRestated,
                InvariantCases.Platform, Slow: false, Scenario: null, Check: () => Problems(name, defaulted));
        }
    }

    /// <summary>
    /// Whether help prints <c>[default: X]</c> for the option: it has a parser default, unless it
    /// is a boolean flag that defaults to false, which help shows without one.
    /// </summary>
    private static bool ShowsDefault(DeclaredOption declared) =>
        declared.Option["hasDefault"]?.GetValue<bool>() == true
        && !(declared.Option["type"]?.GetValue<string>() == "boolean"
            && string.Equals(declared.Option["default"]?.GetValue<string>(), "false", StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<ScenarioProblem> Problems(string name, IReadOnlyList<DeclaredOption> defaulted)
    {
        string[] restated =
        [
            .. defaulted.Where(static option => (option.Option["description"]?.GetValue<string>() ?? string.Empty)
                    .Contains("default", StringComparison.OrdinalIgnoreCase))
                .Select(static option => $"{option.Read(OptionAspect.Description)} ({option.Command}, parser default {option.Read(OptionAspect.Default)})"),
        ];
        return restated.Length == 0
            ? []
            : [new ScenarioProblem(0, InvariantCases.DefaultNotRestated,
                $"{name}: {restated.Length} descriptions restate the default that help already shows as [default: ...]: "
                + string.Join(" | ", restated))];
    }
}
