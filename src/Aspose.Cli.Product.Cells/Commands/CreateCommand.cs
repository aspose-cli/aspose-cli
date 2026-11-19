using System.CommandLine;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Cells.Commands;

/// <summary><c>cells create</c>: the engine's new workbook, or a template saved as a new workbook.</summary>
internal static class CreateCommand
{
    public static CommandDefinition<CellsCreateRequest, CellsCreateResult> Create()
    {
        var template = new Option<string?>("--template") { Description = "A workbook to save as the new one. Default: the engine's new workbook, with one sheet." }.WithInput(InputKind.File);
        return new(
            "create",
            "Create a workbook: the engine's new workbook, or a copy of a template saved in the output's format.",
            new CommandTraits
            {
                Output = OutputTarget.CreatedFile("Path of the workbook to create, e.g. report.xlsx.", CellsFormats.Editable),
                Encrypt = CellsInputs.EncryptedWorkbook,
            },
            [template],
            (parse, standard) => new CellsCreateRequest
            {
                Output = standard.Output,
                Template = standard.Source(template),
                EncryptPassword = standard.EncryptPassword(),
            })
        {
            Examples =
            [
                "cells create book.xlsx",
                "cells create report.xlsx --template brand.xltx",
            ],
            Links =
            [
                CommandHelpLink.Docs(CellsModule.Manifest, "overview", "what each Cells command is for"),
            ],
        };
    }
}
