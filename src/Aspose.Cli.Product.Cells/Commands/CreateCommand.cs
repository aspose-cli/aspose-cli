using System.CommandLine;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.IO.Output;

namespace Aspose.Cli.Product.Cells.Commands;

/// <summary><c>cells create</c> — create a blank workbook.</summary>
internal static class CreateCommand
{
    public static CommandDefinition<CellsCreateRequest, CellsCreateResult> Create()
    {
        var sheets = new Option<string?>("--sheet-names")
        {
            Description = "Comma-separated sheet names, e.g. \"Data,Summary\". Default: one sheet named Sheet1.",
        }.WithInput(InputKind.None);
        return new(
            "create",
            "Create a new workbook.",
            new CommandTraits
            {
                Output = OutputTarget.CreatedFile("Path of the workbook to create, e.g. report.xlsx.", CellsFormats.Editable),
                Encrypt = CellsInputs.EncryptedWorkbook,
            },
            [sheets],
            (parse, standard) =>
            {
                IReadOnlyList<string> sheetNames = ParseSheetNames(parse.GetValue(sheets));
                ResolvedOutput output = standard.Output;
                Secret? encryptPassword = standard.EncryptPassword();
                return new CellsCreateRequest
                {
                    Output = output,
                    SheetNames = sheetNames,
                    EncryptPassword = encryptPassword,
                };
            })
        {
            Examples =
            [
                "cells create book.xlsx --sheet-names \"Data,Summary\"",
                "cells create budget.xlsx --overwrite",
            ],
            Links =
            [
                CommandHelpLink.Docs(CellsModule.Manifest, "workbook-standards", "professional workbook construction guidance"),
            ],
        };
    }

    private static IReadOnlyList<string> ParseSheetNames(string? sheets)
    {
        if (string.IsNullOrWhiteSpace(sheets))
        {
            return ["Sheet1"];
        }

        string[] names = sheets.Split(',', StringSplitOptions.TrimEntries);
        if (names.Any(string.IsNullOrEmpty))
        {
            throw CliErrors.OptionInvalid(
                "--sheet-names", "sheet names must not be empty");
        }

        if (names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Length)
        {
            throw CliErrors.OptionInvalid(
                "--sheet-names", "sheet names must be unique (case-insensitive)");
        }

        return names;
    }
}
