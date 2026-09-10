using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Rendering;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Sdk;

public sealed class RenderPixelGuardTests
{
    [Theory]
    [InlineData(36)]
    [InlineData(600)]
    public void EnsureDpi_AcceptsThePublicBounds(int dpi) =>
        RenderPixelGuard.EnsureDpi(dpi, 36, 600);

    [Theory]
    [InlineData(35)]
    [InlineData(601)]
    public void EnsureDpi_RejectsValuesOutsideThePublicBounds(int dpi)
    {
        CliException error = Assert.Throws<CliException>(
            () => RenderPixelGuard.EnsureDpi(dpi, 36, 600));

        Assert.Equal(ErrorCodes.OptionInvalid, error.Code);
    }

    [Fact]
    public void EnsureDpi_AcceptsProductSpecificBounds() =>
        RenderPixelGuard.EnsureDpi(1_200, 36, 1_200);

    [Fact]
    public void EnsureFits_RejectsOversizedImagesWithoutIntegerOverflow()
    {
        CliException error = Assert.Throws<CliException>(() =>
            RenderPixelGuard.EnsureFits(
                long.MaxValue,
                long.MaxValue,
                144));

        Assert.Equal(ErrorCodes.RenderTooLarge, error.Code);
        Assert.Equal(long.MaxValue, error.Details?["width"]?.GetValue<long>());
        Assert.Contains("smaller render region", error.Hint, StringComparison.Ordinal);
    }
}
