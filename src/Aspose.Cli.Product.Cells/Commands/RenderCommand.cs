using System.CommandLine;
using Aspose.Cli.Product.Cells.Addressing;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Cells.Commands;

/// <summary>
/// <c>aspose-cli cells render</c> — draw a sheet (or a range of it) as an image.
/// This is how an agent "looks at" a spreadsheet to verify layout and charts.
/// </summary>
internal static class RenderCommand
{
    private const int MinDpi = 24;
    private const int MaxDpi = 1200;

    public static Command Create(IProductCommandHost<IWorkbookEngine> host)
    {
        var fileArgument = new Argument<string>("file")
        {
            Description = "Workbook to render.",
        };

        var toOption = new Option<string>("--to")
        {
            Description = $"Image format: {string.Join(", ", CellsModule.Formats.IdsFor(FormatUse.Render))}.",
            DefaultValueFactory = _ => "png",
        };

        var output = new OutputFileOptions("Output path. Default: the input path with the image extension. "
            + "With --all-sheets it is the naming template: <base>.<Sheet><ext>.");

        var sheetOption = new Option<string?>("--sheet")
        {
            Description = "Sheet to render. Default: the active sheet.",
        };

        var rangeOption = new Option<string?>("--range")
        {
            Description = "Render only this range, e.g. A1:G20 or Sales!A1:G20.",
        };

        var allSheetsOption = new Option<bool>("--all-sheets")
        {
            Description = "Render every visible sheet, one image per sheet named <out-base>.<Sheet><ext>.",
        };

        var dpiOption = new Option<int>("--dpi")
        {
            Description = $"Raster resolution ({MinDpi}-{MaxDpi}); ignored for svg.",
            DefaultValueFactory = _ => 192,
        };

        var password = new PasswordOptions("--password", "the workbook");

        var render = new Command("render", "Render a sheet, a range, or every visible sheet to images.");
        render.Arguments.Add(fileArgument);
        render.Options.Add(toOption);
        render.Options.Add(sheetOption);
        render.Options.Add(rangeOption);
        render.Options.Add(allSheetsOption);
        render.Options.Add(dpiOption);
        output.AddTo(render);
        password.AddTo(render);

        render.SetAction(parseResult => host.Run(parseResult, context =>
        {
            FormatInfo format = ResolveFormat(parseResult, toOption, output);

            int dpi = parseResult.GetValue(dpiOption);
            OptionGuards.EnsureInRange("--dpi", dpi, MinDpi, MaxDpi,
                "Use 96 for screen-quality output or 192 (default) for crisp text.");

            bool allSheets = parseResult.GetValue(allSheetsOption);
            if (allSheets && parseResult.GetValue(sheetOption) is not null)
            {
                throw CliErrors.OptionInvalid(
                    "--all-sheets",
                    "cannot be combined with --sheet",
                    "Choose one: render every visible sheet (--all-sheets) or one named sheet (--sheet).");
            }

            if (allSheets && parseResult.GetValue(rangeOption) is not null)
            {
                throw CliErrors.OptionInvalid(
                    "--all-sheets",
                    "cannot be combined with --range",
                    "Choose one: render every visible sheet (--all-sheets) or a window of one sheet (--range).");
            }

            (string? sheetName, RangeRef? range) = SheetRangeInput.Resolve(
                parseResult.GetValue(sheetOption), parseResult.GetValue(rangeOption));

            string inputPath = context.Paths.ResolveInput(parseResult.GetRequiredValue(fileArgument));
            string outputPath = output.ResolvePath(parseResult, context.Paths, inputPath, format.Extension);

            return context.Port.Render(inputPath, new RenderRequest
            {
                TargetFormatId = format.Id,
                OutputPath = outputPath,
                Overwrite = output.Overwrite(parseResult),
                SheetName = sheetName,
                Range = range,
                AllSheets = allSheets,
                Dpi = dpi,
                Password = password.Resolve(parseResult, context.Inputs),
            });
        }));

        return render;
    }

    /// <summary>
    /// An explicit <c>--to</c> wins; otherwise an explicit <c>--out</c> picks the
    /// format from its extension, exactly as the mutating verbs do. Without this
    /// the png default silently wins and <c>--out chart.svg</c> writes PNG bytes
    /// into a file named .svg — the result even reports "format": "png", so
    /// nothing about it looks wrong until something tries to read the SVG. An
    /// extension that names no render format (say .dat) still gets the default.
    /// </summary>
    private static FormatInfo ResolveFormat(
        ParseResult parseResult,
        Option<string> toOption,
        OutputFileOptions output)
    {
        if (parseResult.GetResult(toOption) is { Implicit: false })
        {
            return CellsFormats.ResolveRender(parseResult.GetRequiredValue(toOption));
        }

        if (output.RequestedExtension(parseResult) is { } extension
            && CellsFormats.TryResolveRender(extension) is { } fromExtension)
        {
            return fromExtension;
        }

        return CellsFormats.ResolveRender(parseResult.GetRequiredValue(toOption));
    }
}
