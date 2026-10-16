using Aspose.Cells;
using Aspose.Cells.Rendering;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Rendering;
using Aspose.Cli.Sdk.Views;
using static Aspose.Cli.Product.Cells.Engine.CellsEngineSupport;

namespace Aspose.Cli.Product.Cells.Engine;

/// <summary>
/// The worksheet image and interactive workbook views, and the layout facts their review
/// checks; the view adapter calls them.
/// </summary>
internal static class CellsView
{
    /// <summary>Renders the parts of one view, opening the workbook once.</summary>
    public static ViewManifest Render(
        CellsSession session,
        string filePath,
        ViewRenderRequest request,
        IViewArtifactSink artifacts)
    {
        const string workbookFile = "workbook.html";
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(artifacts);

        _ = session.Outputs.License;
        using LoadedWorkbook loaded = session.Loader.Open(filePath, request.Password);
        Workbook workbook = loaded.Workbook;
        InputInfo source = BuildSource(filePath, loaded);
        if (request.View == CellsViews.Workbook)
        {
            WorkbookGridExporter.Export(workbook, artifacts, session.Budgets, workbookFile);
            session.Outputs.ShowsInView(workbook, "html");
            return new ViewManifest
            {
                View = CellsViews.Workbook,
                SourceFormat = source.Format,
                SourceSizeBytes = source.SizeBytes,
                SourceEncrypted = loaded.IsEncrypted,
                TotalPartCount = 1,
                Parts =
                [
                    new ViewPart
                    {
                        Id = CellsViews.Workbook,
                        Label = Path.GetFileName(filePath),
                        File = workbookFile,
                        Kind = ViewPartKinds.Html,
                    },
                ],
                // The workbook view opens on the active sheet.
                Warnings = loaded.Warnings(loaded.SkippedSheetWarning(defaultedToActiveSheet: true, "Read another sheet with 'aspose-cli cells read --sheet' and the sheet's name from inspect.")),
            };
        }

        Worksheet[] visible = workbook.Worksheets
            .Cast<Worksheet>()
            .Where(static sheet => sheet.IsVisible)
            .ToArray();
        var parts = new List<ViewPart>();
        var partial = new List<Warning>();
        foreach (Worksheet sheet in visible.Take(request.MaxPartCount))
        {
            string file = string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"sheet-{sheet.Index + 1:0000}.png");
            (int width, int height, Warning? window) = RenderSheetPart(session.Budgets, sheet, RenderPixelGuard.DefaultDpi, file, artifacts, filePath);
            if (window is not null)
            {
                partial.Add(window);
            }
            parts.Add(new ViewPart
            {
                Id = sheet.Name,
                Label = sheet.Name,
                File = file,
                Kind = ViewPartKinds.Image,
                Width = width,
                Height = height,
            });
        }

        session.Outputs.ShowsInView(workbook, "png");
        return new ViewManifest
        {
            View = CellsViews.Sheets,
            SourceFormat = source.Format,
            SourceSizeBytes = source.SizeBytes,
            SourceEncrypted = loaded.IsEncrypted,
            TotalPartCount = visible.Length,
            Parts = parts,
            Warnings = loaded.Warnings([.. partial]),
        };
    }

    /// <summary>
    /// Writes one worksheet image and returns its CSS layout size. A sheet
    /// without printable content becomes a blank placeholder rather than an
    /// error, so an empty sheet never hides the rest of the workbook.
    /// </summary>
    private static (int Width, int Height, Warning? Window) RenderSheetPart(
        ResourceBudgetLedger budgets,
        Worksheet sheet,
        int dpi,
        string file,
        IViewArtifactSink artifacts,
        string input)
    {
        var options = new ImageOrPrintOptions
        {
            ImageType = Aspose.Cells.Drawing.ImageType.Png,
            OnePagePerSheet = true,
            HorizontalResolution = dpi,
            VerticalResolution = dpi,
        };
        var render = new SheetRender(sheet, options);
        if (render.PageCount == 0)
        {
            artifacts.Write(file, stream => stream.Write(BlankPng));
            return (320, 120, null);
        }

        try
        {
            // A review shows every sheet, and a model reads each image in full: a sheet longer
            // than CellsRender.LargeImageSide on a side, or over the pixel budget, contributes its
            // first window of rows and columns rather than an unreadable image or a failed
            // review. The print area only limits this in-memory render; the workbook is never saved.
            Warning? window = null;
            (long width, long height) = Pixels(render, dpi);
            double side = Math.Min(CellsRender.LargeImageSide, Math.Sqrt(budgets.Limit(ResourceBudgetKinds.RasterPixels)));
            if (Math.Max(width, height) > side && Drawn(sheet) is { } drawn)
            {
                RangeRef shown = drawn;
                // The window is sized by its share of the image; a row or column larger than the
                // rest takes another, smaller pass.
                for (int pass = 0; pass < 8 && Math.Max(width, height) > side; pass++)
                {
                    shown = CellsRender.FirstWindow(shown, width, height, 0.9 * side);
                    sheet.PageSetup.PrintArea = A1.FormatRange(shown);
                    render = new SheetRender(sheet, options);
                    (width, height) = Pixels(render, dpi);
                }

                window = shown == drawn ? null : WindowShown(sheet, drawn, shown, input);
            }

            window ??= CellsRender.PrintAreasLeftOut(sheet, render, input);
            CellsRender.EnsureRenderable(budgets, render, dpi, sheet, printArea: null);
            float[] inches = render.GetPageSizeInch(0);
            artifacts.Write(file, stream => render.ToImage(0, stream));
            return (
                Math.Max(1, (int)Math.Ceiling(inches[0] * 96)),
                Math.Max(1, (int)Math.Ceiling(inches[1] * 96)),
                window);
        }
        catch (CellsException exception)
        {
            throw CellsErrors.RenderFailed(sheet.Name, exception.Message);
        }
    }

    private static (long Width, long Height) Pixels(SheetRender render, int dpi)
    {
        float[] inches = render.GetPageSizeInch(0);
        return ((long)Math.Ceiling(inches[0] * dpi), (long)Math.Ceiling(inches[1] * dpi));
    }

    /// <summary>The cells a sheet image draws: the first area of its print area, or its used range.</summary>
    private static RangeRef? Drawn(Worksheet sheet)
    {
        if (SheetLayoutProjection.PrintArea(sheet.PageSetup)?.Range is { } printArea)
        {
            try
            {
                return A1.ParseRange(A1.SplitList(printArea)[0]).Range;
            }
            catch (CliException)
            {
                // A print area that is no bounded range draws as the engine reads it; the used
                // range bounds the window instead.
            }
        }

        return Sheets.UsedRange(sheet);
    }

    /// <summary>
    /// <c>SHEET_PARTIALLY_RENDERED</c> for a sheet whose image shows the window
    /// <paramref name="shown"/> of <paramref name="drawn"/>, with the command that renders the
    /// next window: the rows below it, or once the rows are all shown, the columns to its right.
    /// </summary>
    private static Warning WindowShown(Worksheet sheet, RangeRef drawn, RangeRef shown, string input)
    {
        RangeRef next = shown.End.Row < drawn.End.Row
            ? new RangeRef(
                new CellRef(shown.End.Row + 1, shown.Start.Column),
                new CellRef(Math.Min(drawn.End.Row, shown.End.Row + shown.RowCount), shown.End.Column))
            : new RangeRef(
                new CellRef(shown.Start.Row, shown.End.Column + 1),
                new CellRef(shown.End.Row, Math.Min(drawn.End.Column, shown.End.Column + shown.ColumnCount)));
        return new Warning(CellsDiagnostics.SheetPartiallyRendered,
            $"Worksheet '{sheet.Name}' is too large for one readable review image; it shows {A1.FormatRange(shown)} of {A1.FormatRange(drawn)}.")
        {
            Hint = $"Render the next window, then the ones after it the same way: aspose-cli cells render \"{input}\" --range \"{CellsRender.Qualified(sheet, next)}\"; or trust 'cells read' for the data.",
            Location = sheet.Name,
            AffectsCompleteness = true,
        };
    }

    // A valid one-pixel white PNG for a worksheet without printable content.
    private static readonly byte[] BlankPng = System.Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Wl2VQAAAABJRU5ErkJggg==");

    /// <summary>The sheet, dimension, print-area and chart layout facts the review checks.</summary>
    public static CellsReviewLayout Layout(CellsSession session, string filePath, Secret? password)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        _ = session.Outputs.License;
        using LoadedWorkbook loaded = session.Loader.Open(filePath, password);
        return ReviewLayoutProjection.Inspect(loaded.Workbook) with { Warnings = loaded.Warnings() };
    }
}
