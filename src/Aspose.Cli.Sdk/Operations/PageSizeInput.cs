using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Sdk.Operations;

/// <summary>
/// The size of a page in points (72 per inch) as an operation sets it: a preset, in portrait, or
/// both the width and the height. A read reports it as a page size under the same names.
/// </summary>
[OperationInput]
[ExactlyOneOf("preset", "width")]
[DependentRequired("width", "height")]
[DependentRequired("height", "width")]
public sealed partial record PageSizeInput
{
    /// <summary>The preset, in portrait; omitted when the width and height are given.</summary>
    [AllowedValues(typeof(PageSizePresets))]
    public string? Preset { get; init; }

    /// <summary>The page width in points, given with the height instead of a preset.</summary>
    [ExclusiveMinimum(0)]
    public double? Width { get; init; }

    /// <summary>The page height in points, given with the width instead of a preset.</summary>
    [ExclusiveMinimum(0)]
    public double? Height { get; init; }

    /// <summary>The width and height in points: the preset's portrait dimensions, or the ones given.</summary>
    public (double Width, double Height) Dimensions() =>
        Preset is { } preset ? PageSizePresets.Dimensions(preset) : (Width!.Value, Height!.Value);
}
