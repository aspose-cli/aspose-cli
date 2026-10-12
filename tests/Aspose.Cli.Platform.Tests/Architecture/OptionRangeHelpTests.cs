using System.CommandLine;
using System.Text.RegularExpressions;
using Aspose.Cli.Host.Tests;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility.Commanding;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Architecture;

/// <summary>
/// C-B20: a bounded integer option states its range in its description in one wording,
/// <c>Range: MIN-MAX.</c>, which <c>OptionGuards.InRange</c> writes from the bounds it enforces.
/// </summary>
public sealed partial class OptionRangeHelpTests
{
    /// <summary>The integer options with an upper bound, in every command that has them.</summary>
    private static readonly string[] Bounded =
    [
        "--dpi", "--grid", "--width", "--max-blocks", "--max-chars", "--max-hits", "--max-cells", "--max-diffs",
        "--sample-rows", "--max-items", "--port", "--max-input-bytes",
    ];

    [Fact]
    public void EveryBoundedIntegerOption_StatesItsRangeInTheOneWording()
    {
        Command root = ActualCommandTree.Parser.Parse([]).ParseResult.RootCommandResult.Command;
        Option[] options = [.. Descendants(root).SelectMany(static command => command.Options).Distinct()];

        Assert.All(Bounded, name => Assert.Contains(options, option => option.Name == name));
        Assert.All(
            options.Where(static option => Bounded.Contains(option.Name)),
            static option => Assert.Matches(StatedRange(), option.Description ?? string.Empty));
        Assert.All(
            options.Where(static option => option.ValueType == typeof(int) || option.ValueType == typeof(int?) || option.ValueType == typeof(long?)),
            static option => Assert.DoesNotMatch(OtherRangeWording(), option.Description ?? string.Empty));
    }

    [Fact]
    public void InRange_StatesTheBoundsAndGetInRangeEnforcesThem()
    {
        Option<int> count = new Option<int>("--count") { Description = "Items to list." }.InRange(1, 50);
        var command = new RootCommand { count };

        Assert.Equal("Items to list. Range: 1-50.", count.Description);
        Assert.Equal(50, command.Parse(["--count", "50"]).GetInRange(count));
        CliException error = Assert.Throws<CliException>(() => command.Parse(["--count", "51"]).GetInRange(count));
        Assert.Equal(ErrorCodes.OptionInvalid, error.Code);
        Assert.Contains("between 1 and 50", error.Message, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => command.Parse([]).GetInRange(new Option<int>("--other")));
    }

    private static IEnumerable<Command> Descendants(Command command) =>
        command.Subcommands.SelectMany(Descendants).Prepend(command);

    [GeneratedRegex(@"(^| )Range: \d+-\d+\.$")]
    private static partial Regex StatedRange();

    /// <summary>The wordings the one range wording replaced: a parenthesized range, a bare <c>Range N-M</c> or a hard maximum.</summary>
    [GeneratedRegex(@"\(\d+-\d+\)|Range \d|hard maximum|between \d+ and \d+")]
    private static partial Regex OtherRangeWording();
}
