using System.Text;
using Aspose.Cells;
using Aspose.Cells.Rendering;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Rendering;
using Aspose.Cli.Sdk.Results;
using static Aspose.Cli.Product.Cells.Engine.CellsEngineSupport;

namespace Aspose.Cli.Product.Cells.Engine;

/// <summary><c>cells render</c>: sheets, or a range of one, drawn as images.</summary>
internal static class CellsRender
{
    public static CellsRenderResult Run(CellsSession session, CellsRenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrEmpty(request.Input);

        LicenseState licenseState = session.Outputs.License;
        using LoadedWorkbook loaded = session.Loader.Open(request.Input, request.Password);
        Workbook workbook = loaded.Workbook;
        InputInfo input = BuildSource(request.Input, loaded);
        (IReadOnlyList<Worksheet> sheets, bool defaulted) = Select(workbook, request);
        string? renderedRange = request.Range is { } range ? A1.FormatRange(range) : null;

        // A hidden sheet renders to zero pages (and would crash the page-size probe). A sheet
        // the caller selected is revealed in memory — the render never saves the workbook, so
        // the file is untouched — turning a lookup or backing sheet into a real image. The
        // default takes the workbook as a reader sees it and selects visible sheets only.
        foreach (Worksheet sheet in sheets)
        {
            sheet.IsVisible = true;
        }

        IReadOnlyList<string> outputPaths = DerivePerSheetPaths(request.Output, sheets);
        using OutputSet<Workbook> transaction = session.Outputs.BeginSet([request.Output.Directory], "cells-render");
        var rendered = new List<CellsSheetOutput>();
        var skipped = new List<string>();
        CliException? firstSkip = null;
        for (int i = 0; i < sheets.Count; i++)
        {
            Worksheet sheet = sheets[i];
            try
            {
                StagedOutput staged = StageSheet(session.Budgets, transaction, sheet, request, renderedRange, outputPaths[i]);
                rendered.Add(new CellsSheetOutput(FileObjects.Output(staged.Published, request.Output.Format, encrypted: false))
                {
                    Sheet = sheet.Name,
                });
            }
            catch (CliException ex) when (sheets.Count > 1
                && (ex.Code == CellsDiagnostics.RenderEmpty || ex.Code == ErrorCodes.RenderFailed))
            {
                // Only this sheet's own content is at fault; its siblings can still render.
                firstSkip ??= ex;
                skipped.Add($"'{sheet.Name}' ({DescribeSkip(ex)})");
            }
        }

        if (rendered.Count == 0)
        {
            throw firstSkip ?? Empty(Sheets.Resolve(workbook, sheetName: null));
        }

        transaction.Commit();
        Warning? sheetsSkipped = skipped.Count == 0 ? null : new Warning(CellsDiagnostics.SheetsSkipped, $"Skipped {skipped.Count} sheet(s): {string.Join(", ", skipped)}.")
        {
            Hint = "Render a skipped sheet alone with --sheets to get the full error.",
        };

        return new CellsRenderResult
        {
            Input = input,
            Outputs = rendered,
            Range = renderedRange,
            Dpi = CellsEngineFormats.IsRaster(request.Output.Format.Id) ? request.Dpi : null,
            License = EnvelopeParts.License(licenseState),
            Warnings = loaded.Warnings(loaded.SkippedSheetWarning(defaulted, "Pass --sheets with the sheet's position from inspect to render another sheet."), sheetsSkipped),
        };
    }

    /// <summary>
    /// The sheets a render draws, in workbook order, and whether it defaulted to the active
    /// sheet. A range draws one sheet: the one it names, the one <c>--sheets</c> selects, or the
    /// active sheet. Otherwise <c>--sheets</c> selects sheets by 1-based position; omitted or
    /// <c>all</c>, it draws every visible sheet, leaving out the warning sheets an evaluation
    /// save added, as every command's default does, unless the workbook shows nothing else.
    /// </summary>
    private static (IReadOnlyList<Worksheet> Sheets, bool Defaulted) Select(Workbook workbook, CellsRenderRequest request)
    {
        if (request.Sheets is null && request.SheetsText is { } byName)
        {
            throw Sheets.SelectedByName(workbook, byName, range: true);
        }

        bool all = request.Sheets is null || ReferenceEquals(request.Sheets, PageRange.All);
        if (request.Range is not null)
        {
            if (request.SheetName is not null || all)
            {
                return ([Sheets.Resolve(workbook, request.SheetName)], request.SheetName is null);
            }

            IReadOnlyList<int> one = Positions(workbook, request);
            if (one.Count != 1)
            {
                throw CliErrors.OptionInvalid(
                    "--sheets",
                    $"--range renders one sheet, but the selection holds {one.Count}",
                    "Select one sheet, such as --sheets 2, or qualify the range, such as Sales!A1:G20.");
            }

            return ([workbook.Worksheets[one[0] - 1]], false);
        }

        if (!all)
        {
            return ([.. Positions(workbook, request).Select(position => workbook.Worksheets[position - 1])], false);
        }

        var visible = new List<Worksheet>();
        foreach (Worksheet sheet in workbook.Worksheets)
        {
            if (sheet.IsVisible)
            {
                visible.Add(sheet);
            }
        }

        if (visible.Any(static sheet => !CellsEvaluation.IsWarningSheet(sheet)))
        {
            visible.RemoveAll(CellsEvaluation.IsWarningSheet);
        }

        if (visible.Count > RenderSelection.MaximumParts)
        {
            throw CliErrors.SelectionTooLarge(
                "--sheets",
                "sheet",
                visible.Count,
                RenderSelection.MaximumParts,
                $"Select at most {RenderSelection.MaximumParts} sheets, such as --sheets 1-{RenderSelection.MaximumParts}, or render part of one with --range.");
        }

        return (visible, false);
    }

    /// <summary>
    /// <c>RENDER_EMPTY</c> for <paramref name="sheet"/>, pointing to the first other sheet that
    /// holds cell data or a drawing, leaving out the warning sheets an evaluation save added.
    /// </summary>
    private static CliException Empty(Worksheet sheet)
    {
        WorksheetCollection worksheets = sheet.Workbook.Worksheets;
        for (int index = 0; index < worksheets.Count; index++)
        {
            Worksheet other = worksheets[index];
            if (other.Index != sheet.Index && !CellsEvaluation.IsWarningSheet(other)
                && (other.Cells.MaxDataRow >= 0 || other.Shapes.Count > 0))
            {
                return CellsErrors.RenderEmpty(sheet.Name, (index + 1, other.Name));
            }
        }

        return CellsErrors.RenderEmpty(sheet.Name, withData: null);
    }

    /// <summary>The 1-based positions a selection names, in workbook order, within the render limit.</summary>
    private static IReadOnlyList<int> Positions(Workbook workbook, CellsRenderRequest request) =>
        [.. Sheets.Selected(workbook, request.SheetsText, range: true,
            () => RenderSelection.Resolve(request.Sheets!, workbook.Worksheets.Count, CellsDiagnostics.SheetNotFound, "sheet")).Distinct().Order()];

    /// <summary>
    /// The shared per-sheet render pipeline: build the renderer, refuse an
    /// empty sheet (<c>RENDER_EMPTY</c>), probe the raster size
    /// (<c>RENDER_TOO_LARGE</c>), write through the safe writer, and translate
    /// an engine rasterization crash into <c>RENDER_FAILED</c>. Both the
    /// single-sheet path and a render of several sheets run through it.
    /// </summary>
    private static StagedOutput StageSheet(ResourceBudgetLedger budgets, OutputSet<Workbook> transaction, Worksheet sheet, CellsRenderRequest request, string? printArea, string outputPath)
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
            throw Empty(sheet);
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
    /// 1-based workbook position. A single sheet is written to the output path itself.
    /// </summary>
    private static IReadOnlyList<string> DerivePerSheetPaths(ResolvedOutput output, IReadOnlyList<Worksheet> sheets)
    {
        if (sheets.Count == 1)
        {
            return [output.Path];
        }

        var paths = new string[sheets.Count];
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < sheets.Count; i++)
        {
            string label = SanitizeFileNamePart(sheets[i].Name);
            if (!taken.Add(label))
            {
                label = $"{label}{sheets[i].Index + 1}";
                for (int suffix = 2; !taken.Add(label); suffix++)
                {
                    // Degenerate double collision (a sheet literally named like
                    // another's fallback); keep appending until unique so two
                    // sheets can never target the same file.
                    label = $"{SanitizeFileNamePart(sheets[i].Name)}{sheets[i].Index + 1}-{suffix}";
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
