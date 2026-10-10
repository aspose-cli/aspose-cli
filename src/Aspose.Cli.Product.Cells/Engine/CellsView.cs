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
        SourceInfo source = BuildSource(filePath, workbook);
        if (request.View == CellsViews.Workbook)
        {
            WorkbookGridExporter.Export(workbook, artifacts, session.Budgets, workbookFile);
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
                Warnings = loaded.Warnings(loaded.SkippedSheetWarning(defaultedToActiveSheet: true)),
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
            (int width, int height, Warning? window) = RenderSheetPart(session.Budgets, sheet, RenderPixelGuard.DefaultDpi, file, artifacts);
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
        IViewArtifactSink artifacts)
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
            // A review shows every sheet, so a sheet too large for one image contributes its
            // first rows rather than failing the review; the workbook is never saved here.
            Warning? window = null;
            float[] whole = render.GetPageSizeInch(0);
            double pixels = Math.Ceiling(whole[0] * dpi) * Math.Ceiling(whole[1] * dpi);
            long maxPixels = budgets.Limit(ResourceBudgetKinds.RasterPixels);
            int lastRow = sheet.Cells.MaxDataRow;
            if (pixels > maxPixels && lastRow > 0)
            {
                int rows = Math.Clamp((int)(0.9 * (lastRow + 1) * maxPixels / pixels), 1, lastRow);
                sheet.PageSetup.PrintArea = "A1:" + CellsHelper.CellIndexToName(rows - 1, Math.Max(0, sheet.Cells.MaxDataColumn));
                render = new SheetRender(sheet, options);
                window = new Warning(CellsDiagnostics.SheetPartiallyRendered, $"Worksheet '{sheet.Name}' is too large for one review image; only rows 1-{rows} of {lastRow + 1} were rendered.")
                {
                    Hint = $"Check the rest with 'cells render --sheet \"{sheet.Name}\" --range' windows, or trust 'cells query' for the data.",
                    Location = sheet.Name,
                    AffectsCompleteness = true,
                };
            }

            CellsRender.EnsureRenderable(budgets, render, dpi);
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
