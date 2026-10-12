using Aspose.Cli.Sdk.Operations;
using Aspose.Cli.Sdk.IO;
using Aspose.Cells;
using Aspose.Cells.Drawing;

namespace Aspose.Cli.Product.Cells.Engine.Editing;

/// <summary>Inserts pictures from a file, anchored at a cell.</summary>
internal static class ImageOps
{
    public static long? InsertImage(Worksheet sheet, InsertImageOp op, InputResourceScope inputs)
    {
        if (!File.Exists(op.Path))
        {
            // The executor attaches the op index to this domain error.
            throw new OperationInvalidException($"image file not found: {op.Path}");
        }

        CellRef anchor = A1.ParseCell(op.At);
        // Adding an SVG picture fetches its external images, and WorkbookSettings.ResourceProvider
        // does not govern that request (known issue CELLS-SVG-EGRESS, KNOWN-ISSUES.md); refuse first.
        byte[] image = OperationImages.Read(inputs, op.Path, refuseNetworkReferences: true);
        int index = sheet.Pictures.Add(anchor.Row, anchor.Column, new MemoryStream(image, writable: false));
        Picture picture = sheet.Pictures[index];

        // The contract measures in points; the engine sizes a picture in pixels at 96 per inch.
        if (op.Width is { } width)
        {
            picture.Width = Pixels(width);
        }

        if (op.Height is { } height)
        {
            picture.Height = Pixels(height);
        }

        return null;
    }

    private static int Pixels(double points) => Math.Max(1, (int)Math.Round(points * 96 / 72));
}
