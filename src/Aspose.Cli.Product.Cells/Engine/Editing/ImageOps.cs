using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO.Input;
using Aspose.Cli.Sdk.Operations;
using Aspose.Cells;
using Aspose.Cells.Drawing;

namespace Aspose.Cli.Product.Cells.Engine.Editing;

/// <summary>Inserts pictures from a file, anchored at a cell.</summary>
internal static class ImageOps
{
    public static void InsertImage(Worksheet sheet, InsertImageOp op, InputResourceScope inputs)
    {
        if (!File.Exists(op.Path))
        {
            // The runner attaches the operation's id and name, as for a missing image in every product.
            throw CliErrors.FileNotFound(op.Path);
        }

        CellRef anchor = A1.ParseCell(op.At);
        // Adding an SVG picture fetches its external images, and WorkbookSettings.ResourceProvider
        // does not govern that request (known issue CELLS-SVG-EGRESS, KNOWN-ISSUES.md); refuse first.
        byte[] image = OperationImages.Read(inputs, op.Path, refuseNetworkReferences: true);
        int index = sheet.Pictures.Add(anchor.Row, anchor.Column, new MemoryStream(image, writable: false));
        Picture picture = sheet.Pictures[index];

        // The contract measures in points; the engine sizes a picture in pixels at 96 per inch.
        // One side given scales the other by the image's own proportions.
        int naturalWidth = picture.Width;
        int naturalHeight = picture.Height;
        switch (op.Width, op.Height)
        {
            case ({ } width, { } height):
                picture.Width = Pixels(width);
                picture.Height = Pixels(height);
                break;
            case ({ } width, null):
                picture.Width = Pixels(width);
                picture.Height = Math.Max(1, (int)Math.Round((double)picture.Width * naturalHeight / Math.Max(naturalWidth, 1)));
                break;
            case (null, { } height):
                picture.Height = Pixels(height);
                picture.Width = Math.Max(1, (int)Math.Round((double)picture.Height * naturalWidth / Math.Max(naturalHeight, 1)));
                break;
        }
    }

    private static int Pixels(double points) => Math.Max(1, (int)Math.Round(points * 96 / 72));
}
