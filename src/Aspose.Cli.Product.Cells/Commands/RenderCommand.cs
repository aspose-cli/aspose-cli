using System.CommandLine;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Tables;

namespace Aspose.Cli.Product.Cells.Commands;

/// <summary><c>cells render</c>: every sheet, or the ones named, each drawn by the engine as one image.</summary>
internal static class RenderCommand
{
    public static CommandDefinition<CellsRenderRequest, CellsRenderResult> Create()
    {
        var sheets = new Option<string[]>(CellsOptionNames.Sheet)
        {
            Description = "A sheet to render, by its name; repeat for several. Default: every sheet of the workbook. Several sheets are each written to <stem>.<sheet>.<ext>.",
        }.WithInput(InputKind.None);
        var area = new Option<string?>(CellsOptionNames.Area)
        {
            Description = "An area of the one sheet named, such as A1:G20, set as its print area and drawn alone. Default: the whole sheet.",
        }.WithInput(InputKind.None);
        var dpi = new DpiOption();
        return new(
            "render",
            "Render sheets, or an area of one, to images as the engine draws them.",
            new CommandTraits
            {
                Input = CellsInputs.Workbook,
                Output = OutputTarget.File("Output path. Default: the input path with the image extension. "
                    + "Several sheets write <stem>.<sheet>.<ext> beside it."),
                UsesFonts = true,
                Target = TargetFormat.Render(CellsFormats.Definitions),
            },
            [sheets, area, .. dpi.Options],
            (parse, standard) =>
            {
                string[] named = parse.GetValue(sheets) ?? [];
                string? drawn = parse.GetValue(area);
                if (drawn is not null)
                {
                    if (named.Length != 1)
                    {
                        throw CliErrors.OptionInvalid(CellsOptionNames.Area, $"an area is of the one sheet --sheet names, and --sheet named {named.Length}");
                    }

                    if (!Key.IsWrittenAs(KeyPart.Area, drawn))
                    {
                        throw CliErrors.OptionInvalid(CellsOptionNames.Area, $"'{drawn}' is not an area such as A1:G20");
                    }
                }

                return new CellsRenderRequest
                {
                    Input = standard.Input,
                    Output = standard.Output,
                    Sheets = named.Length == 0 ? null : named,
                    Area = drawn,
                    Dpi = dpi.Read(parse, standard.Output.Format),
                    Password = standard.InputPassword,
                };
            })
        {
            Examples =
            [
                "cells render book.xlsx --out book.png",
                "cells render book.xlsx --sheet Dashboard --area A1:H30 --out dashboard.png",
                "cells render book.xlsx --sheet Data --sheet Summary --out check.png",
            ],
            Links =
            [
                CommandHelpLink.Schema(CellsModule.Manifest, "render-result", "the fields of the render result"),
            ],
        };
    }
}
