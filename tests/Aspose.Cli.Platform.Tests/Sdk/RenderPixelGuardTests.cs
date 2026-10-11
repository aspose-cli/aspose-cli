using System.CommandLine;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility.Commanding;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Rendering;
using Aspose.Cli.Sdk.Tests;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Sdk;

public sealed class RenderPixelGuardTests
{
    [Theory]
    [InlineData("36", 36)]
    [InlineData("1200", 1_200)]
    public void DpiOption_AcceptsTheSharedBounds(string value, int expected)
    {
        (DpiOption dpi, ParseResult parse) = ParseDpi("--dpi", value);

        Assert.Equal(expected, dpi.Read(parse));
        Assert.True(dpi.IsExplicit(parse));
    }

    [Theory]
    [InlineData("24")]
    [InlineData("35")]
    [InlineData("1201")]
    public void DpiOption_RejectsValuesOutsideTheSharedBounds(string value)
    {
        (DpiOption dpi, ParseResult parse) = ParseDpi("--dpi", value);

        CliException error = Assert.Throws<CliException>(() => dpi.Read(parse));

        Assert.Equal(ErrorCodes.OptionInvalid, error.Code);
    }

    [Fact]
    public void DpiOption_DefaultsWithoutCountingAsExplicit()
    {
        (DpiOption dpi, ParseResult parse) = ParseDpi();

        Assert.Equal(RenderPixelGuard.DefaultDpi, dpi.Read(parse));
        Assert.False(dpi.IsExplicit(parse));
    }

    [Fact]
    public void EnsureFits_UsesTheLedgerBudgetForOneImage()
    {
        ResourceBudgetLedger budgets = TestBudgets.Create();
        long limit = budgets.Limit(ResourceBudgetKinds.RasterPixels);

        RenderPixelGuard.EnsureFits(budgets, limit, 1, dpi: 72);
        CliException error = Assert.Throws<CliException>(() =>
            RenderPixelGuard.EnsureFits(budgets, limit, 2, dpi: null, hint: "Shrink it."));

        Assert.Equal(ResourceBudgetDefaults.RasterPixels, limit);
        Assert.Equal(ErrorCodes.RenderTooLarge, error.Code);
        Assert.Equal("Shrink it.", error.Hint);
        Assert.Equal(limit, error.Details?["limit"]?.GetValue<long>());
        Assert.Equal(limit * 2, error.Details?["actual"]?.GetValue<long>());
        Assert.Equal("pixels", error.Details?["unit"]?.GetValue<string>());
        Assert.False(error.Details!.ContainsKey("dpi"), "A render without a resolution leaves dpi out instead of stating null.");
        Assert.False(error.Details.ContainsKey("maxPixels"));
    }

    [Fact]
    public void EnsureFits_RejectsOversizedImagesWithoutIntegerOverflow()
    {
        CliException error = Assert.Throws<CliException>(() =>
            RenderPixelGuard.EnsureFits(TestBudgets.Create(), long.MaxValue, long.MaxValue, 144));

        Assert.Equal(ErrorCodes.RenderTooLarge, error.Code);
        Assert.Equal(long.MaxValue, error.Details?["width"]?.GetValue<long>());
        Assert.Contains("smaller render region", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void PartSelection_ReadsARangeOrAllAndRejectsAMalformedOneAsAnOptionMistake()
    {
        var selection = new PartSelectionOptions("page");
        var command = new Command("render");
        foreach (Option option in selection.Options)
        {
            command.Options.Add(option);
        }

        PageRange? range = selection.Read(command.Parse(["--pages", "2-3"]));
        PageRange? all = selection.Read(command.Parse(["--pages", "all"]));
        PageRange? omitted = selection.Read(command.Parse([]));
        CliException error = Assert.Throws<CliException>(
            () => selection.Read(command.Parse(["--pages", "3-x"])));

        Assert.Equal("2-3", range!.Text);
        Assert.Equal([1, 2, 3, 4], all!.Resolve(4));
        Assert.Null(omitted);
        Assert.Equal(ErrorCodes.OptionInvalid, error.Code);
        Assert.Equal("--pages", error.Details!["option"]!.GetValue<string>());
        Assert.DoesNotContain(selection.Options, static option => option.Name.StartsWith("--all-", StringComparison.Ordinal));
    }

    private static (DpiOption Dpi, ParseResult Parse) ParseDpi(params string[] arguments)
    {
        var dpi = new DpiOption();
        var command = new Command("render");
        foreach (Option option in dpi.Options)
        {
            command.Options.Add(option);
        }
        return (dpi, command.Parse(arguments));
    }
}
