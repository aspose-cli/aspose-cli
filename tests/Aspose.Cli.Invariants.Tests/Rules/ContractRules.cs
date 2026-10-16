using Aspose.Cli.TestKit.Scenarios;

namespace Aspose.Cli.Invariants.Tests;

/// <summary>
/// One numbered rule of the command-line contract, checked by the invariant cases its parts
/// generate from the live catalog, help and schemas. Each case names the rule as its invariant and
/// reads <c>C-R&lt;nn&gt; | &lt;subject&gt;</c>; today's violations are listed in
/// <c>known-violations.json</c> like any other invariant's.
/// </summary>
internal sealed record ContractRule(string Id, params Func<CliCatalog, IEnumerable<InvariantCase>>[] Parts)
{
    public IEnumerable<InvariantCase> Cases(CliCatalog catalog) => Parts.SelectMany(part => part(catalog));
}

/// <summary>The contract rules with mechanical checks, each part in the file of the area it checks.</summary>
internal static partial class ContractRules
{
    public static IReadOnlyList<ContractRule> All { get; } =
    [
        new("C-R01", R01),
        new("C-R02", R02),
        new("C-R03", R03Declared, R03Run),
        new("C-R04", R04),
        new("C-R05", R05),
        new("C-R06", R06),
        new("C-R08", R08),
        new("C-R09", R09),
        new("C-R10", R10Declared, R10Run),
        new("C-R11", R11Declared, R11Run),
        new("C-R12", R12Declared, R12Run),
        new("C-R13", R13Declared, R13Run),
        new("C-R14", R14),
        new("C-R15", R15Declared, R15Run),
        new("C-R16", R16),
        new("C-R17", R17Options, R17Schemas),
        new("C-R18", R18),
        new("C-R19", R19Names, R19Run),
        new("C-R20", R20),
        new("C-R21", R21Declared, R21Run),
        new("C-R22", R22Run),
        new("C-R23", R23),
        new("C-R24", R24Declared, R24Run),
        new("C-R25", R25),
        new("C-R26", R26),
        new("C-R27", R27),
        new("C-R28", R28),
        new("C-R29", R29),
        new("C-R30", R30Schema, R30Run),
        new("C-R31", R31),
        new("C-R32", R32),
        new("C-R33", R33),
        new("C-R34", R34),
        new("C-R35", R35),
        new("C-R36", R36),
        new("C-R37", R37),
        new("C-R38", R38),
        new("C-R39", R39Run),
        new("C-R40", R40),
    ];

    /// <summary>
    /// A case that the catalog, help or schemas answer without running a command: each message is
    /// one violation of <paramref name="rule"/> by <paramref name="subject"/>, in a stable order.
    /// </summary>
    internal static InvariantCase Check(
        string rule, string subject, Func<IEnumerable<string>> violations, string shard = InvariantCases.Contract, bool slow = false) =>
        new($"{rule} | {subject}", rule, shard, slow, Scenario: null,
            Check: () => [.. violations().Select(message => new ScenarioProblem(0, rule, message))]);
}
