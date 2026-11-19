using System.Text;
using System.Text.Json.Nodes;
using Aspose.Cells;
using Aspose.Cells.Rendering;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk;
using Aspose.Cli.Sdk.IO.Publication;
using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Rendering;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.Sdk.Tables;

namespace Aspose.Cli.Product.Cells.Engine;

/// <summary>
/// <c>cells render</c>: each sheet named, or every sheet, drawn by the engine's <see cref="SheetRender"/> as one
/// page, at the resolution given; an area given is set as the sheet's print area and drawn alone
/// (<see cref="ImageOrPrintOptions.OnlyArea"/>). The workbook is not saved.
/// </summary>
internal static class CellsRender
{
    internal static CellsRenderResult Run(CellsSession session, CellsRenderRequest request)
    {
        LicenseState state = session.Outputs.License;
        var log = new EngineWarningLog();
        using LoadedWorkbook loaded = session.Loader.Open(request.Input, request.Password, log);
        Workbook workbook = loaded.Workbook;
        Worksheet[] sheets = request.Sheets is { } names
            ? [.. names.Select(name => CellsSheets.Find(workbook, name))]
            : [.. workbook.Worksheets];
        var options = new ImageOrPrintOptions
        {
            ImageType = CellsEngineFormats.Image(request.Output.Format.Id),
            OnePagePerSheet = true,
        };
        if (request.Output.Format.Raster)
        {
            options.HorizontalResolution = request.Dpi;
            options.VerticalResolution = request.Dpi;
        }

        if (request.Area is { } area)
        {
            sheets[0].PageSetup.PrintArea = area;
            options.OnlyArea = true;
        }

        // A sheet the engine lays out on no page, such as an empty or a hidden one, has no image:
        // the render stops at it before anything is written, whether it was named or not.
        SheetRender[] renders = [.. sheets.Select(sheet => new SheetRender(sheet, options))];
        for (int i = 0; i < sheets.Length; i++)
        {
            if (renders[i].PageCount == 0)
            {
                throw CliErrors.RenderFailed($"Sheet '{sheets[i].Name}'", "SheetRender.PageCount is 0: the engine lays out no page for it",
                    new JsonObject { ["sheet"] = sheets[i].Name });
            }
        }

        IReadOnlyList<string> paths = Paths(request.Output, sheets);
        log.During = "save";
        using OutputSet<Workbook> set = session.Outputs.BeginSet([request.Output.Directory], "cells-render");
        var outputs = new List<CellsSheetOutput>();
        for (int i = 0; i < sheets.Length; i++)
        {
            SheetRender render = renders[i];
            if (request.Output.Format.Raster)
            {
                EnsureRenderable(session.Budgets, render, request.Dpi);
            }

            StagedOutput staged = set.Stage(paths[i], request.Output.Format, request.Output.Overwrite, workbook,
                temp =>
                {
                    render.ToImage(0, temp);
                    loaded.Resources.ThrowIfFailed();
                },
                rendering: true);
            outputs.Add(new CellsSheetOutput(FileObjects.Output(staged.Published, request.Output.Format, encrypted: false)) { Sheet = sheets[i].Name });
        }

        set.Commit();
        return new CellsRenderResult
        {
            Input = FileObjects.Input(request.Input, loaded.FormatId, loaded.Encrypted),
            Outputs = outputs,
            Area = request.Area,
            Dpi = request.Output.Format.Raster ? request.Dpi : null,
            EngineWarnings = log.Warnings.Count == 0 ? null : log.Warnings,
            License = EnvelopeParts.License(state),
            Warnings = loaded.Warnings.Count == 0 ? null : loaded.Warnings,
        };
    }

    /// <summary>Refuses a raster image whose bitmap would exceed the shared pixel budget.</summary>
    private static void EnsureRenderable(ResourceBudgetLedger budgets, SheetRender render, int dpi)
    {
        float[] inches = render.GetPageSizeInch(0);
        RenderPixelGuard.EnsureFits(budgets, (long)Math.Ceiling(inches[0] * dpi), (long)Math.Ceiling(inches[1] * dpi), dpi);
    }

    /// <summary>
    /// One output per sheet: the output path itself for one sheet; for several,
    /// <c>&lt;stem&gt;.&lt;sheet&gt;&lt;ext&gt;</c>, with each character no file system takes
    /// replaced by one underscore (a run of them by one), and the sheet's position from 1 added
    /// after a name another sheet already took.
    /// </summary>
    private static IReadOnlyList<string> Paths(ResolvedOutput output, IReadOnlyList<Worksheet> sheets)
    {
        if (sheets.Count == 1)
        {
            return [output.Path];
        }

        var paths = new string[sheets.Count];
        var taken = new HashSet<string>(FilePaths.Comparer);
        for (int i = 0; i < sheets.Count; i++)
        {
            string label = FileNamePart(sheets[i].Name);
            for (int suffix = 1; !taken.Add(label); suffix++)
            {
                label = $"{FileNamePart(sheets[i].Name)}{sheets[i].Index + 1}{(suffix > 1 ? $"-{suffix}" : string.Empty)}";
            }

            paths[i] = output.Part(label);
        }

        return paths;
    }

    private static readonly char[] FileNameUnsafe = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    private static string FileNamePart(string sheetName)
    {
        var builder = new StringBuilder(sheetName.Length);
        bool replaced = false;
        foreach (char c in sheetName)
        {
            bool unsafeCharacter = c < ' ' || Array.IndexOf(FileNameUnsafe, c) >= 0;
            if (!unsafeCharacter)
            {
                builder.Append(c);
            }
            else if (!replaced)
            {
                builder.Append('_');
            }

            replaced = unsafeCharacter;
        }

        return builder.Length == 0 ? "_" : builder.ToString();
    }
}
