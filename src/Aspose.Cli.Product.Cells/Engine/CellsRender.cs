using System.Text;
using Aspose.Cells;
using Aspose.Cells.Rendering;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Rendering;
using Aspose.Cli.Sdk.Results;
using static Aspose.Cli.Product.Cells.Engine.CellsEngineSupport;

namespace Aspose.Cli.Product.Cells.Engine;

/// <summary><c>cells render</c>: a sheet, a range of it, or every visible sheet drawn as images.</summary>
internal static class CellsRender
{
    public static RenderResult Run(CellsSession session, RenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrEmpty(request.Input);

        LicenseState licenseState = session.Outputs.License;
        using LoadedWorkbook loaded = session.Loader.Open(request.Input, request.Password);
        Workbook workbook = loaded.Workbook;
        SourceInfo input = BuildSource(request.Input, workbook);

        if (request.AllSheets)
        {
            return RenderAllSheets(session, workbook, input, request, licenseState, loaded);
        }

        Worksheet sheet = Sheets.Resolve(workbook, request.SheetName);
        bool isRaster = CellsEngineFormats.IsRaster(request.Output.Format.Id);

        // A hidden sheet renders to zero pages (and would crash the page-size
        // probe below). The caller explicitly asked for THIS sheet, so reveal it
        // in memory — the render never saves the workbook, so the file is
        // untouched — turning a lookup/backing sheet into a real image.
        // --all-sheets deliberately never reveals: it takes the workbook as a
        // reader sees it.
        if (!sheet.IsVisible)
        {
            sheet.IsVisible = true;
        }

        string? renderedRange = null;
        if (request.Range is { } range)
        {
            renderedRange = A1.FormatRange(range);
        }

        using OutputSet<Workbook> transaction = session.Outputs.BeginSet([request.Output.Directory], "cells-render");
        long sizeBytes = StageSheet(session.Budgets, transaction, sheet, request, renderedRange, request.Output.Path).SizeBytes;
        transaction.Commit();

        return new RenderResult
        {
            Input = input,
            Output = new OutputInfo
            {
                Path = request.Output.Path,
                Format = request.Output.Format.Id,
                SizeBytes = sizeBytes,
            },
            Sheet = sheet.Name,
            Range = renderedRange,
            Dpi = isRaster ? request.Dpi : null,
            License = EnvelopeParts.License(licenseState),
            Warnings = EnvelopeParts.CombineWarnings(loaded.Resources.CoverageWarning, loaded.CalculatedOnOpen,
                loaded.SkippedSheetWarning(request.SheetName is null)),
        };
    }

    /// <summary>
    /// The shared per-sheet render pipeline: build the renderer, refuse an
    /// empty sheet (<c>RENDER_EMPTY</c>), probe the raster size
    /// (<c>RENDER_TOO_LARGE</c>), write through the safe writer, and translate
    /// an engine rasterization crash into <c>RENDER_FAILED</c>. Both the
    /// single-sheet path and <c>--all-sheets</c> run through it.
    /// </summary>
    private static StagedOutput StageSheet(ResourceBudgetLedger budgets, OutputSet<Workbook> transaction, Worksheet sheet, RenderRequest request, string? printArea, string outputPath)
    {
        bool isRaster = CellsEngineFormats.IsRaster(request.Output.Format.Id);

        var imageOptions = new ImageOrPrintOptions
        {
            ImageType = CellsEngineFormats.Image(request.Output.Format.Id),
            OnePagePerSheet = true,
        };

        if (isRaster)
        {
            imageOptions.HorizontalResolution = request.Dpi;
            imageOptions.VerticalResolution = request.Dpi;
        }

        if (printArea is not null)
        {
            // Rendering a sub-range goes through the print area; OnlyArea
            // keeps the output at exactly that area instead of a full page.
            sheet.PageSetup.PrintArea = printArea;
            imageOptions.OnlyArea = true;
        }

        var render = new SheetRender(sheet, imageOptions);

        // An empty sheet produces no page; indexing page 0 below (here or in the
        // size probe) would throw ArgumentOutOfRangeException and surface as an
        // INTERNAL "please report a bug" for the ordinary act of rendering a
        // blank sheet. Turn it into a clean, actionable error instead.
        if (render.PageCount == 0)
        {
            throw CellsErrors.RenderEmpty(sheet.Name);
        }

        try
        {
            if (isRaster)
            {
                EnsureRenderable(budgets, render, request.Dpi);
            }

            // The image shows the one sheet, which carries evaluation marks only when it is a
            // warning sheet; it then renders the workbook's marks (CellsEvaluationProfile).
            return transaction.Stage(outputPath, request.Output.Overwrite,
                CellsEvaluation.IsWarningSheet(sheet) ? sheet.Workbook : null,
                tempPath => render.ToImage(0, tempPath), rendering: true);
        }
        catch (CellsException ex)
        {
            // The engine failed on this sheet's content (a real corpus
            // dashboard dies with "Chart/Picture to image Error!" over one
            // defective embedded object while its sibling sheets render fine).
            // That is a property of the file, not a CLI bug — INTERNAL is
            // reserved for our own faults. CliExceptions (RENDER_TOO_LARGE,
            // output errors) pass through untouched.
            throw CellsErrors.RenderFailed(sheet.Name, ex.Message);
        }
    }

    /// <summary>
    /// The <c>--all-sheets</c> path: renders every visible sheet, each to its
    /// own file derived from the single output path, in workbook order. A
    /// hidden sheet is skipped silently by design — revealing is reserved for
    /// an explicit <c>--sheet</c> request. A sheet only IT cannot produce
    /// (empty, or its content defeats the rasterizer) is skipped and reported
    /// in a <c>SHEETS_SKIPPED</c> warning; a caller mistake
    /// (<c>OUTPUT_EXISTS</c>, <c>RENDER_TOO_LARGE</c>, …) fails the whole
    /// command. If every candidate was skipped, the first skip reason is
    /// rethrown so an all-empty workbook still surfaces <c>RENDER_EMPTY</c>.
    /// </summary>
    private static RenderResult RenderAllSheets(
        CellsSession session, Workbook workbook, SourceInfo input, RenderRequest request, LicenseState licenseState, LoadedWorkbook loaded)
    {
        var candidates = new List<Worksheet>();
        foreach (Worksheet sheet in workbook.Worksheets)
        {
            if (sheet.IsVisible)
            {
                candidates.Add(sheet);
            }
        }

        IReadOnlyList<string> outputPaths = DerivePerSheetPaths(request.Output, candidates);

        using OutputSet<Workbook> transaction = session.Outputs.BeginSet([request.Output.Directory], "cells-render");
        var rendered = new List<SheetRenderOutput>();
        var skipped = new List<string>();
        CliException? firstSkip = null;

        for (int i = 0; i < candidates.Count; i++)
        {
            Worksheet sheet = candidates[i];
            try
            {
                long sizeBytes = StageSheet(session.Budgets, transaction, sheet, request, printArea: null, outputPaths[i]).SizeBytes;
                rendered.Add(new SheetRenderOutput
                {
                    Sheet = sheet.Name,
                    Path = outputPaths[i],
                    SizeBytes = sizeBytes,
                });
            }
            catch (CliException ex) when (
                ex.Code == CellsDiagnostics.RenderEmpty || ex.Code == ErrorCodes.RenderFailed)
            {
                // Only this sheet's own content is at fault; its siblings can
                // still render. Record the reason and carry on.
                firstSkip ??= ex;
                skipped.Add($"'{sheet.Name}' ({DescribeSkip(ex)})");
            }
        }

        if (rendered.Count == 0)
        {
            throw firstSkip
                ?? CellsErrors.RenderEmpty(Sheets.Resolve(workbook, sheetName: null).Name);
        }

        transaction.Commit();
        Warning? sheetsSkipped = skipped.Count == 0 ? null : new Warning(CellsDiagnostics.SheetsSkipped, $"Skipped {skipped.Count} sheet(s): {string.Join(", ", skipped)}.")
        {
            Hint = "Render a skipped sheet alone with --sheet to get the full error.",
        };

        return new RenderResult
        {
            Input = input,
            Output = new OutputInfo
            {
                Path = rendered[0].Path,
                Format = request.Output.Format.Id,
                SizeBytes = rendered[0].SizeBytes,
            },
            Sheet = rendered[0].Sheet,
            Dpi = CellsEngineFormats.IsRaster(request.Output.Format.Id) ? request.Dpi : null,
            Outputs = rendered,
            License = EnvelopeParts.License(licenseState),
            Warnings = EnvelopeParts.CombineWarnings([.. loaded.Warnings() ?? [], sheetsSkipped]),
        };
    }

    /// <summary>The one-line reason a sheet was skipped, for the warning message.</summary>
    private static string DescribeSkip(CliException exception) =>
        exception.Code == ErrorCodes.RenderFailed
            && exception.Details?["engineMessage"]?.GetValue<string>() is { } engineMessage
            ? $"{exception.Code.Name}: {engineMessage}"
            : exception.Code.Name;

    /// <summary>
    /// Derives one output file per sheet from the single output path the
    /// caller gave (or the default): <c>report.png</c> with sheets
    /// <c>Data</c> and <c>Summary</c> becomes <c>report.Data.png</c> and
    /// <c>report.Summary.png</c>. Characters no file system accepts are
    /// replaced with one underscore (a run of them collapses to one) using a
    /// fixed, platform-independent set so the same workbook names the same
    /// files everywhere; a collision after sanitizing appends the sheet's
    /// zero-based workbook index.
    /// </summary>
    private static IReadOnlyList<string> DerivePerSheetPaths(ResolvedOutput output, IReadOnlyList<Worksheet> sheets)
    {
        var paths = new string[sheets.Count];
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < sheets.Count; i++)
        {
            string label = SanitizeFileNamePart(sheets[i].Name);
            if (!taken.Add(label))
            {
                label = $"{label}{sheets[i].Index}";
                for (int suffix = 2; !taken.Add(label); suffix++)
                {
                    // Degenerate double collision (a sheet literally named like
                    // another's fallback); keep appending until unique so two
                    // sheets can never target the same file.
                    label = $"{SanitizeFileNamePart(sheets[i].Name)}{sheets[i].Index}-{suffix}";
                }
            }

            paths[i] = output.Part(label);
        }

        return paths;
    }

    /// <summary>
    /// Characters stripped from a sheet name before it becomes part of a file
    /// name: the Windows-invalid set, the strictest of the supported
    /// platforms, so derived names are identical on every OS. Excel already
    /// forbids most of these in sheet names; the ones it allows
    /// (<c>&lt;</c>, <c>&gt;</c>, <c>|</c>, <c>"</c>) are exactly why this
    /// sanitizer exists.
    /// </summary>
    private static readonly char[] FileNameUnsafeChars = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    private static string SanitizeFileNamePart(string sheetName)
    {
        var builder = new StringBuilder(sheetName.Length);
        bool previousWasReplacement = false;
        foreach (char c in sheetName)
        {
            if (c < ' ' || Array.IndexOf(FileNameUnsafeChars, c) >= 0)
            {
                if (!previousWasReplacement)
                {
                    builder.Append('_');
                    previousWasReplacement = true;
                }
            }
            else
            {
                builder.Append(c);
                previousWasReplacement = false;
            }
        }

        return builder.Length == 0 ? "_" : builder.ToString();
    }

    /// <summary>Rejects raster output whose bitmap would exceed the shared pixel budget.</summary>
    internal static void EnsureRenderable(ResourceBudgetLedger budgets, SheetRender render, int dpi)
    {
        float[] inches = render.GetPageSizeInch(0);
        RenderPixelGuard.EnsureFits(
            budgets,
            (long)Math.Ceiling(inches[0] * dpi),
            (long)Math.Ceiling(inches[1] * dpi),
            dpi,
            "Render a window of the sheet with --range (e.g. --range A1:H50), or lower --dpi.");
    }


}
