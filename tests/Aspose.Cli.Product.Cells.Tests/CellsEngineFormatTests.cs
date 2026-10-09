using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>
/// Every format the product declares is one the engine can serve: an input format is one the
/// engine detects, a convert format has a save format and a render format an image type.
/// </summary>
public sealed class CellsEngineFormatTests
{
    [Fact]
    public void EveryDeclaredFormat_HasAnEngineMapping()
    {
        var missing = new List<string>();
        foreach (FormatDescriptor format in CellsFormats.Definitions)
        {
            CellsEngineFormat? engine = CellsEngineFormats.All.SingleOrDefault(entry => entry.Id == format.Id);
            if (format.Uses.HasFlag(FormatUse.Input) && engine?.Detected is null)
            {
                missing.Add($"{format.Id}: no detected engine format");
            }

            if (format.Uses.HasFlag(FormatUse.Convert) && engine?.Save is null)
            {
                missing.Add($"{format.Id}: no engine save format");
            }

            if (format.Uses.HasFlag(FormatUse.Render) && engine?.Image is null)
            {
                missing.Add($"{format.Id}: no engine image type");
            }
        }

        Assert.True(missing.Count == 0, string.Join(Environment.NewLine, missing));
    }

    [Fact]
    public void EveryEngineMapping_IsADeclaredFormat()
    {
        HashSet<string> declared = CellsFormats.Definitions.Select(static format => format.Id).ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain(CellsEngineFormats.All, entry => !declared.Contains(entry.Id));
        Assert.Equal(
            CellsEngineFormats.All.Count,
            CellsEngineFormats.All.Select(static entry => entry.Id).Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [InlineData(Aspose.Cells.FileFormatType.Excel97To2003, "xls")]
    [InlineData(Aspose.Cells.FileFormatType.TabDelimited, "tsv")]
    [InlineData(Aspose.Cells.FileFormatType.Excel2, "excel2")]
    public void DetectedFormats_MapToTheirPublicIdOrTheEngineName(Aspose.Cells.FileFormatType detected, string id) =>
        Assert.Equal(id, CellsEngineFormats.IdOf(detected));
}
