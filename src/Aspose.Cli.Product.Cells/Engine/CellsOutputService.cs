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
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.Sdk.Views;
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
        using LoadedWorkbook loaded = _loader.Open(filePath, request.Password);
        Workbook workbook = loaded.Workbook;

        // Capture before saving: Workbook.FileFormat mutates to the target
        // format once the workbook is saved.
        SourceInfo input = BuildSource(filePath, workbook);

        SaveFormat saveFormat = FormatMapper.ToSaveFormat(request.TargetFormatId);
        string? resolvedSheetName = null;
        int? selectedSheet = null;

        // A modern sheet can overflow the legacy xls grid (65,536 rows × 256
        // columns); the engine's save then silently discards everything past it.
        // Detect it off the in-memory used range BEFORE saving so the warning
        // names the exact rows/columns that would be lost — never silent, as the
        // contract demands and as Excel itself warns when saving down to xls.
        Warning? dataTruncated = _saver.DetectGridTruncation(workbook, saveFormat);

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
                selectedSheet = sheet.Index;
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

        if (request.TargetFormatId is "csv" or "tsv" or "md"
            && licenseState == LicenseState.Evaluation && request.SheetName is not null)
        {
            // The evaluation SDK writes the first sheet regardless of ActiveSheetIndex.
            Worksheet active = workbook.Worksheets[workbook.Worksheets.ActiveSheetIndex];
            Worksheet first = workbook.Worksheets[0];
            if (active.Index != 0)
            { throw CellsErrors.TextExportEvaluationLimit(request.TargetFormatId, active.Name, first.Name); }
        }

        int refsBefore = _saver.CountRefFormulas(workbook);
        WorkbookSavePlan savePlan = WorkbookSavePlan.Create(request.TargetFormatId, licenseState,
            inputPassword: loaded.IsEncrypted ? request.Password : null, selectedSheet: selectedSheet);
        Warning? sheetsDropped = savePlan.DetectSheetLoss(workbook);
        long sizeBytes = _saver.Write(request.OutputPath, request.Overwrite,
            path => _saver.Produce(workbook, savePlan, path));
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
            Warnings = CombineWarnings(licenseState, loaded.Resources.CoverageWarning, sheetsDropped, dataTruncated, formulasBroken, savePlan.EncryptionWarning),
        };
    }

    /// <inheritdoc />
    internal RenderResult Render(string filePath, RenderRequest request)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(request);

        LicenseState licenseState = _licenseGate.EnsureApplied();
        using LoadedWorkbook loaded = _loader.Open(filePath, request.Password);
        Workbook workbook = loaded.Workbook;
        SourceInfo input = BuildSource(filePath, workbook);

        if (request.AllSheets)
        {
            return RenderAllSheets(workbook, input, request, licenseState, loaded.Resources.CoverageWarning);
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

        using var transaction = new AtomicOutputSetWriter(_fileWriter, Path.GetDirectoryName(request.OutputPath)!, "cells-render");
        long sizeBytes = StageSheet(transaction, sheet, request, renderedRange, request.OutputPath).SizeBytes;
        transaction.Commit();

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
            Warnings = CombineWarnings(licenseState, loaded.Resources.CoverageWarning),
        };
    }

    /// <summary>
    /// The shared per-sheet render pipeline: build the renderer, refuse an
    /// empty sheet (<c>RENDER_EMPTY</c>), probe the raster size
    /// (<c>RENDER_TOO_LARGE</c>), write through the safe writer, and translate
    /// an engine rasterization crash into <c>RENDER_FAILED</c>. Both the
    /// single-sheet path and <c>--all-sheets</c> run through it.
    /// </summary>
    private StagedOutput StageSheet(AtomicOutputSetWriter transaction, Worksheet sheet, RenderRequest request, string? printArea, string outputPath)
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

            return transaction.Stage(
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
        Workbook workbook, SourceInfo input, RenderRequest request, LicenseState licenseState, Warning? resourceOmission)
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

        using var transaction = new AtomicOutputSetWriter(_fileWriter, Path.GetDirectoryName(request.OutputPath)!, "cells-render");
        var rendered = new List<SheetRenderOutput>();
        var skipped = new List<string>();
        CliException? firstSkip = null;

        for (int i = 0; i < candidates.Count; i++)
        {
            Worksheet sheet = candidates[i];
            try
            {
                long sizeBytes = StageSheet(transaction, sheet, request, printArea: null, outputPaths[i]).SizeBytes;
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

        transaction.Commit();
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
            Warnings = CombineWarnings(licenseState, resourceOmission, sheetsSkipped),
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

    /// <summary>Renders the parts of one view, opening the workbook once.</summary>
    internal ViewManifest RenderView(
        string filePath,
        ViewRenderRequest request,
        IViewArtifactSink artifacts)
    {
        const int dpi = 192;
        const string workbookFile = "workbook.html";
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(artifacts);

        _licenseGate.EnsureApplied();
        using LoadedWorkbook loaded = _loader.Open(filePath, request.Password);
        Workbook workbook = loaded.Workbook;
        SourceInfo source = BuildSource(filePath, workbook);
        if (request.View == CellsViews.Workbook)
        {
            WorkbookGridExporter.Export(workbook, artifacts, workbookFile);
            return new ViewManifest
            {
                View = CellsViews.Workbook,
                SourceFormat = source.Format,
                SourceSizeBytes = source.SizeBytes,
                TotalParts = 1,
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
                Warnings = loaded.Warnings(),
            };
        }

        Worksheet[] visible = workbook.Worksheets
            .Cast<Worksheet>()
            .Where(static sheet => sheet.IsVisible)
            .ToArray();
        var parts = new List<ViewPart>();
        foreach (Worksheet sheet in visible.Take(request.MaxParts))
        {
            string file = string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"sheet-{sheet.Index + 1:0000}.png");
            (int width, int height) = RenderSheetPart(sheet, dpi, file, artifacts);
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
            TotalParts = visible.Length,
            Parts = parts,
            Warnings = loaded.Warnings(),
        };
    }

    /// <summary>
    /// Writes one worksheet image and returns its CSS layout size. A sheet
    /// without printable content becomes a blank placeholder rather than an
    /// error, so an empty sheet never hides the rest of the workbook.
    /// </summary>
    private static (int Width, int Height) RenderSheetPart(
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
            return (320, 120);
        }

        try
        {
            EnsureRenderable(render, dpi);
            float[] inches = render.GetPageSizeInch(0);
            artifacts.Write(file, stream => render.ToImage(0, stream));
            return (
                Math.Max(1, (int)Math.Ceiling(inches[0] * 96)),
                Math.Max(1, (int)Math.Ceiling(inches[1] * 96)));
        }
        catch (CellsException exception)
        {
            throw CellsErrors.RenderFailed(sheet.Name, exception.Message);
        }
    }

    // A valid one-pixel white PNG for a worksheet without printable content.
    private static readonly byte[] BlankPng = System.Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Wl2VQAAAABJRU5ErkJggg==");

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
