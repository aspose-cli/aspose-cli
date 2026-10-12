using System.Globalization;
using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Sdk.Operations;

/// <summary>
/// The size of a page in points (72 per inch) as an operation sets it: a preset, in portrait, or
/// both the width and the height. A read reports it as a page size under the same names, with the
/// preset the dimensions match, and writes back as it reads: given with the width and height, the
/// preset must be the paper they measure in either orientation, and the width and height apply.
/// </summary>
[OperationInput]
[AtLeastOneOf("preset", "width")]
[DependentRequired("width", "height")]
[DependentRequired("height", "width")]
public sealed partial record PageSizeInput
{
    /// <summary>The preset, in portrait; with the width and height, the paper they measure.</summary>
    [AllowedValues(typeof(PageSizePresets))]
    public string? Preset { get; init; }

    /// <summary>The page width in points, given with the height.</summary>
    [ExclusiveMinimum(0)]
    public double? Width { get; init; }

    /// <summary>The page height in points, given with the width.</summary>
    [ExclusiveMinimum(0)]
    public double? Height { get; init; }

    /// <summary>
    /// The width and height in points: the ones given, or the preset's portrait dimensions.
    /// </summary>
    /// <exception cref="OperationInvalidException">The preset is not the paper the width and height measure.</exception>
    public (double Width, double Height) Dimensions()
    {
        if (Width is not { } width)
        {
            return PageSizePresets.Dimensions(Preset!);
        }

        double height = Height!.Value;
        OperationInvalidException.Require(
            Preset is null || PageSizePresets.Matching(width, height) == Preset,
            string.Create(CultureInfo.InvariantCulture, $"pageSize names {Preset} but measures {width} by {height} points, which is not {Preset} in either orientation"),
            "Give the preset alone, the width and height alone, or the three as a read reports them.");
        return (width, height);
    }
}
