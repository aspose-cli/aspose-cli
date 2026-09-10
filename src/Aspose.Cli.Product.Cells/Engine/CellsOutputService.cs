using System.Text;
using Aspose.Cells;
using Aspose.Cells.Rendering;
using Aspose.Cli.Product.Cells.Addressing;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Ports;
using Aspose.Cli.Sdk.Preview;
using Aspose.Cli.Sdk.Results;
using static Aspose.Cli.Product.Cells.Engine.CellsEngineSupport;

namespace Aspose.Cli.Product.Cells.Engine;

/// <summary>
/// Owns workbook conversion, rendering, and preview production.
/// </summary>
internal sealed class CellsOutputService
{
    /// <summary>
    /// Pixel ceiling for one raster render — 256 megapixels, about 1 GiB of
    /// BGRA. Comfortably above any image a person or an agent looks at, and
    /// below where the allocation starts failing.
    /// </summary>
    private const long MaxRenderPixels = 256L * 1024 * 1024;

    private readonly ILicenseGate _licenseGate;
    private readonly SafeFileWriter _fileWriter;
    private readonly WorkbookLoadService _loader;
    private readonly WorkbookSaveService _saver;

    internal CellsOutputService(
        ILicenseGate licenseGate,
        SafeFileWriter fileWriter,
        WorkbookLoadService loader,
        WorkbookSaveService saver)
    {
        ArgumentNullException.ThrowIfNull(licenseGate);
        ArgumentNullException.ThrowIfNull(fileWriter);
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(saver);
        _licenseGate = licenseGate;
        _fileWriter = fileWriter;
        _loader = loader;
        _saver = saver;
    }

    /// <inheritdoc />
    internal ConvertResult Convert(string filePath, ConvertRequest request)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(request);

        LicenseState licenseState = _licenseGate.EnsureApplied();
        using var workbook = _loader.Open(filePath, request.Password);

        // Capture before saving: Workbook.FileFormat mutates to the target
        // format once the workbook is saved.
        SourceInfo input = BuildSource(filePath, workbook);

        SaveFormat saveFormat = FormatMapper.ToSaveFormat(request.TargetFormatId);
        string? resolvedSheetName = null;
        SaveOptions? saveOptions = null;
        Warning? sheetsDropped = null;

        // A modern sheet can overflow the legacy xls grid (65,536 rows × 256
        // columns); the engine's save then silently discards everything past it.
        // Detect it off the in-memory used range BEFORE saving so the warning
        // names the exact rows/columns that would be lost — never silent, as the
        // contract demands and as Excel itself warns when saving down to xls.
        Warning? dataTruncated = _saver.DetectGridTruncation(workbook, saveFormat);

        if (request.TargetFormatId is "html")
        {
            // A plain HTML save writes the page PLUS a sibling "<file>_files"
            // directory and links to it by the name it saw at save time — which
            // here is the atomic writer's temp file, so the deliverable ends up
            // pointing at a hidden, GUID-named directory that any copy or zip
            // drops (probed on 26.6.0). One self-contained file has no
            // companion to lose, survives the temp-then-move, and is what a
            // deliverable should be; mhtml remains the archive form.
            saveOptions = new HtmlSaveOptions
            {
                SaveAsSingleFile = true,
                ExportImagesAsBase64 = true,
            };
        }

        if (request.SheetName is not null)
        {
            Worksheet sheet = Sheets.Resolve(workbook, request.SheetName);
            resolvedSheetName = sheet.Name;

            if (request.TargetFormatId is "csv" or "tsv" or "md")
            {
                // Text formats export the active sheet.
                workbook.Worksheets.ActiveSheetIndex = sheet.Index;
            }
            else if (request.TargetFormatId is "pdf")
            {
                saveOptions = new PdfSaveOptions { SheetSet = new SheetSet([sheet.Index]) };
            }
            else
            {
                // Command-layer validation prevents this; guard against future drift.
                throw CliErrors.OptionInvalid(
                    "--sheet",
                    $"the '{request.TargetFormatId}' format always converts the whole workbook",
                    $"Drop --sheet, or convert to {string.Join(", ", CellsFormats.SheetScopedConvertIds)} for a single-sheet export.");
            }
        }

        if (request.TargetFormatId is "csv" or "tsv" or "md")
        {
            // Text formats export the active sheet as a faithful data extract: the
            // engine's default (CellStyle) renders a 12-digit id as "1E+11" and a
            // long decimal display-rounded — silent data loss — and formats a
            // month-name date in the machine's locale, breaking
            // output determinism. CellValueFormatStrategy.None writes numbers at
            // full precision, and dates pre-normalized to invariant ISO stay
            // meaningful instead of collapsing to a serial number. Both now match
            // what `read --scope values` returns for the same cell.
            //
            // TrimLeadingBlankRowAndColumn defaults true, which drops a leading
            // empty column A and shifts every field left — so CSV field N no longer
            // lines up with sheet column N, and the extract disagrees with read's
            // A1-anchored used range. Turn it off to keep positions faithful.
            Worksheet active = workbook.Worksheets[workbook.Worksheets.ActiveSheetIndex];
            _saver.NormalizeDatesForTextExport(active);
            saveOptions = request.TargetFormatId is "md"
                ? new MarkdownSaveOptions { FormatStrategy = CellValueFormatStrategy.None }
                : new TxtSaveOptions(saveFormat)
                {
                    FormatStrategy = CellValueFormatStrategy.None,
                    TrimLeadingBlankRowAndColumn = false,
                };

            // A text format holds one sheet, so a multi-sheet workbook loses the
            // rest silently — the tool's promise is to never do that quietly, and
            // Excel warns here too. (The eval "Evaluation Warning" sheet is not in
            // the workbook at open time, so this count is honest in both modes.)
            int otherSheets = workbook.Worksheets.Count - 1;
            if (otherSheets > 0)
            {
                sheetsDropped = new Warning
                {
                    Code = CellsDiagnostics.SheetsDropped,
                    Message = $"Only the active sheet '{active.Name}' was exported; a {request.TargetFormatId} file "
                        + $"holds one sheet, so {otherSheets} other sheet(s) were not written.",
                    Hint = "Export a specific sheet with --sheet, or convert to a multi-sheet format "
                        + "(xlsx, xlsb, ods, pdf) to keep them all.",
                };
            }
        }

        int refsBefore = _saver.CountRefFormulas(workbook);
        long sizeBytes = _saver.Write(request.OutputPath, request.Overwrite, tempPath =>
        {
            if (saveOptions is not null)
            {
                workbook.Save(tempPath, saveOptions);
            }
            else
            {
                workbook.Save(tempPath, saveFormat);
            }
        });
        Warning? formulasBroken = _saver.BuildBrokenFormulaWarning(
            refsBefore,
            _saver.CountRefFormulas(workbook),
            request.TargetFormatId);

        return new ConvertResult
        {
            Input = input,
            Output = new OutputInfo
            {
                Path = request.OutputPath,
                Format = request.TargetFormatId,
                SizeBytes = sizeBytes,
            },
            Sheet = resolvedSheetName,
            License = EnvelopeParts.License(licenseState),
            Warnings = CombineWarnings(licenseState, sheetsDropped, dataTruncated, formulasBroken),
        };
    }

    /// <inheritdoc />
    internal RenderResult Render(string filePath, RenderRequest request)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(request);

        LicenseState licenseState = _licenseGate.EnsureApplied();
        using var workbook = _loader.Open(filePath, request.Password);
        SourceInfo input = BuildSource(filePath, workbook);

        if (request.AllSheets)
        {
            return RenderAllSheets(workbook, input, request, licenseState);
        }

        Worksheet sheet = Sheets.Resolve(workbook, request.SheetName);
        bool isRaster = FormatMapper.IsRaster(request.TargetFormatId);

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

        long sizeBytes = RenderSheetToFile(sheet, request, renderedRange, request.OutputPath);

        return new RenderResult
        {
            Input = input,
            Output = new OutputInfo
            {
                Path = request.OutputPath,
                Format = request.TargetFormatId,
                SizeBytes = sizeBytes,
            },
            Sheet = sheet.Name,
            Range = renderedRange,
            Dpi = isRaster ? request.Dpi : null,
            License = EnvelopeParts.License(licenseState),
            Warnings = EnvelopeParts.OutputWarnings(licenseState),
        };
    }

    /// <summary>
    /// The shared per-sheet render pipeline: build the renderer, refuse an
    /// empty sheet (<c>RENDER_EMPTY</c>), probe the raster size
    /// (<c>RENDER_TOO_LARGE</c>), write through the safe writer, and translate
    /// an engine rasterization crash into <c>RENDER_FAILED</c>. Both the
    /// single-sheet path and <c>--all-sheets</c> run through it.
    /// </summary>
    private long RenderSheetToFile(Worksheet sheet, RenderRequest request, string? printArea, string outputPath)
    {
        bool isRaster = FormatMapper.IsRaster(request.TargetFormatId);

        var imageOptions = new ImageOrPrintOptions
        {
            ImageType = FormatMapper.ToImageType(request.TargetFormatId),
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
                EnsureRenderable(render, request.Dpi);
            }

            return _fileWriter.Write(
                outputPath,
                request.Overwrite,
                tempPath => render.ToImage(0, tempPath));
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
    private RenderResult RenderAllSheets(
        Workbook workbook, SourceInfo input, RenderRequest request, LicenseState licenseState)
    {
        var candidates = new List<Worksheet>();
        foreach (Worksheet sheet in workbook.Worksheets)
        {
            if (sheet.IsVisible)
            {
                candidates.Add(sheet);
            }
        }

        IReadOnlyList<string> outputPaths = DerivePerSheetPaths(request.OutputPath, candidates);

        var rendered = new List<SheetRenderOutput>();
        var skipped = new List<string>();
        CliException? firstSkip = null;

        for (int i = 0; i < candidates.Count; i++)
        {
            Worksheet sheet = candidates[i];
            try
            {
                long sizeBytes = RenderSheetToFile(sheet, request, printArea: null, outputPaths[i]);
                rendered.Add(new SheetRenderOutput
                {
                    Sheet = sheet.Name,
                    Path = outputPaths[i],
                    SizeBytes = sizeBytes,
                });
            }
            catch (CliException ex) when (
                ex.Code == ErrorCodes.RenderEmpty || ex.Code == ErrorCodes.RenderFailed)
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

        Warning? sheetsSkipped = skipped.Count == 0 ? null : new Warning
        {
            Code = CellsDiagnostics.SheetsSkipped,
            Message = $"Skipped {skipped.Count} sheet(s): {string.Join(", ", skipped)}.",
            Hint = "Render a skipped sheet alone with --sheet to get the full error.",
        };

        return new RenderResult
        {
            Input = input,
            Output = new OutputInfo
            {
                Path = rendered[0].Path,
                Format = request.TargetFormatId,
                SizeBytes = rendered[0].SizeBytes,
            },
            Sheet = rendered[0].Sheet,
            Dpi = FormatMapper.IsRaster(request.TargetFormatId) ? request.Dpi : null,
            Outputs = rendered,
            License = EnvelopeParts.License(licenseState),
            Warnings = CombineWarnings(licenseState, sheetsSkipped),
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
    private static IReadOnlyList<string> DerivePerSheetPaths(string outputPath, IReadOnlyList<Worksheet> sheets)
    {
        string directory = Path.GetDirectoryName(outputPath) ?? string.Empty;
        string baseName = Path.GetFileNameWithoutExtension(outputPath);
        string extension = Path.GetExtension(outputPath);

        var paths = new string[sheets.Count];
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < sheets.Count; i++)
        {
            string stem = $"{baseName}.{SanitizeFileNamePart(sheets[i].Name)}";
            string fileName = stem + extension;
            if (!taken.Add(fileName))
            {
                fileName = $"{stem}{sheets[i].Index}{extension}";
                for (int suffix = 2; !taken.Add(fileName); suffix++)
                {
                    // Degenerate double collision (a sheet literally named like
                    // another's fallback); keep appending until unique so two
                    // sheets can never target the same file.
                    fileName = $"{stem}{sheets[i].Index}-{suffix}{extension}";
                }
            }

            paths[i] = Path.Combine(directory, fileName);
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

    /// <inheritdoc />
    internal PreviewRenderOutcome RenderPreview(
        string filePath,
        PreviewRenderRequest request,
        IPreviewArtifactSink artifacts)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(artifacts);

        _licenseGate.EnsureApplied();
        using var workbook = _loader.Open(filePath, request.Password);
        SourceInfo source = BuildSource(filePath, workbook);

        if (request.View == CellsPreviewViews.Sheet)
        {
            Worksheet sheet = Sheets.Resolve(workbook, request.SheetName);
            string sheetEntryFileName = PreviewExporter.ExportImage(
                workbook,
                sheet,
                artifacts);
            return new PreviewRenderOutcome(sheetEntryFileName, source.Format, source.SizeBytes);
        }

        // The whole-workbook representation is the default view. The command
        // layer restricts
        // --view to the known vocabulary, so an unrecognized value cannot reach
        // this point today; if drift ever produces one, falling back to the
        // workbook export keeps the live session serving instead of failing it.
        string entryFileName = PreviewExporter.Export(workbook, artifacts);
        return new PreviewRenderOutcome(entryFileName, source.Format, source.SizeBytes);
    }

    /// <summary>Rejects raster output whose bitmap would exceed the allocation budget.</summary>
    private static void EnsureRenderable(SheetRender render, int dpi)
    {
        float[] inches = render.GetPageSizeInch(0);
        long width = (long)Math.Ceiling(inches[0] * dpi);
        long height = (long)Math.Ceiling(inches[1] * dpi);
        if (width * height > MaxRenderPixels)
        {
            throw CellsEngineErrors.RenderTooLarge(width, height, dpi);
        }
    }



}
