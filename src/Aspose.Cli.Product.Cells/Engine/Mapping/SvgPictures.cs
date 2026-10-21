using Aspose.Cells;
using Aspose.Cells.Drawing;
using Aspose.Cli.Sdk.IO.Input;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// Keeps an SVG picture to what the picture itself holds. Aspose.Cells rasterizes an SVG picture
/// that has no raster image yet, when the picture is added or when a workbook that stores only
/// the SVG is loaded, and reads every image the SVG names: a bare path from the file system and a
/// URL through its own request, with no resource hook (known issue CELLS-SVG-EGRESS,
/// KNOWN-ISSUES.md). So every SVG the engine gets is first kept to its own references by
/// <see cref="SvgReferences"/>, and an SVG that does not parse as XML is not used at all.
/// </summary>
internal static class SvgPictures
{
    /// <summary>Whether the bytes are an SVG document, as the image check recognizes one.</summary>
    internal static bool IsSvg(byte[]? content) =>
        content is { Length: > 0 } && ImageInput.EngineFormatOf(content) == EngineImageFormat.Svg;

    /// <summary>
    /// The <c>REMOTE_RESOURCES_BLOCKED</c> warning for <paramref name="omitted"/> resources left out,
    /// <paramref name="fromSvg"/> of them references removed from SVG images; null for none.
    /// </summary>
    internal static Warning? OmissionWarning(int omitted, int fromSvg) => omitted <= 0 ? null
        : new Warning(WarningCodes.RemoteResourcesBlocked,
            $"{omitted} external resource(s) were omitted, {fromSvg} of them referenced by SVG images, whose references were removed.")
        {
            Hint = "Embed an SVG image's images as base64 data: URIs (PNG, JPEG or GIF), place other resources beside the input and reference them by relative path, then review the incomplete output.",
            AffectsCompleteness = true,
        };

    /// <summary>
    /// Replaces each SVG picture of a loaded workbook that names references outside itself with
    /// the same picture without them, at the same place and size, and removes an SVG picture that
    /// does not parse or sits in a group. The engine has already rasterized a picture that came
    /// without a raster image; the replacement drops that image with the picture.
    /// </summary>
    /// <returns>The references removed and the pictures removed.</returns>
    internal static (int References, int Pictures) CleanLoaded(Workbook workbook, Action<string> checkDeadline)
    {
        int references = 0;
        int pictures = 0;
        foreach (Worksheet sheet in workbook.Worksheets)
        {
            for (int index = sheet.Pictures.Count - 1; index >= 0; index--)
            {
                checkDeadline("document-resource");
                Picture picture = sheet.Pictures[index];
                byte[]? data = picture.Data;
                if (picture.ImageType != ImageType.Svg && !IsSvg(data))
                {
                    continue;
                }

                SvgReferences.Cleaned? cleaned = data is null ? null : SvgReferences.KeepInternal(data);
                if (cleaned is { Removed.Count: 0 })
                {
                    continue;
                }
                if (cleaned is not { } kept || picture.IsInGroup)
                {
                    sheet.Pictures.RemoveAt(index);
                    pictures++;
                    continue;
                }

                Replace(sheet, index, kept.Content);
                references += kept.Removed.Count;
            }
        }
        return (references, pictures);
    }

    // The engine cannot change an SVG picture's SVG: setting Data replaces only its raster image.
    private static void Replace(Worksheet sheet, int index, byte[] svg)
    {
        Picture old = sheet.Pictures[index];
        int row = old.UpperLeftRow;
        int column = old.UpperLeftColumn;
        int top = old.Top;
        int left = old.Left;
        int width = old.Width;
        int height = old.Height;
        string name = old.Name;
        string alternativeText = old.AlternativeText;
        string title = old.Title;
        PlacementType placement = old.Placement;
        double rotation = old.RotationAngle;
        (string Address, string TextToDisplay, string ScreenTip)? link = old.Hyperlink is { } hyperlink
            ? (hyperlink.Address, hyperlink.TextToDisplay, hyperlink.ScreenTip)
            : null;
        sheet.Pictures.RemoveAt(index);

        var picture = (Picture)sheet.Shapes.AddSvg(row, top, column, left, height, width, svg, null);
        picture.Width = width;
        picture.Height = height;
        picture.Name = name;
        picture.AlternativeText = alternativeText;
        picture.Title = title;
        picture.Placement = placement;
        picture.RotationAngle = rotation;
        if (link is { } kept)
        {
            Hyperlink added = picture.AddHyperlink(kept.Address);
            added.TextToDisplay = kept.TextToDisplay;
            added.ScreenTip = kept.ScreenTip;
        }
    }
}
