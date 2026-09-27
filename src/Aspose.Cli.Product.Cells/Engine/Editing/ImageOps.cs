using Aspose.Cli.Sdk.Operations;
using Aspose.Cli.Sdk.IO;
using Aspose.Cells;
using Aspose.Cells.Drawing;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Product.Cells.Contracts.Addressing;
using Aspose.Cli.Sdk.Errors;

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
        byte[] image = NetworkReferenceGuard.ReadImage(inputs, op.Path);
        int index = sheet.Pictures.Add(anchor.Row, anchor.Column, new MemoryStream(image, writable: false));
        Picture picture = sheet.Pictures[index];

        if (op.Width is { } width)
        {
            picture.Width = width;
        }

        if (op.Height is { } height)
        {
            picture.Height = height;
        }

        return null;
    }
}
