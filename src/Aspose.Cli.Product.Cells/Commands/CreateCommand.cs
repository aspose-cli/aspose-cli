using System.CommandLine;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility.Output;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Cells.Commands;

/// <summary><c>cells create</c> — create a blank workbook.</summary>
internal static class CreateCommand
{
    public static CommandDefinition<NewWorkbookRequest, CreateResult> Create()
    {
        var sheets = new Option<string?>("--sheets")
        {
            Description = "Comma-separated sheet names, e.g. \"Data,Summary\". Default: one sheet named Sheet1.",
        }.WithInput(InputKind.None);
        return new(
            "create",
            "Create a new workbook.",
            new CommandTraits
            {
                Output = OutputTarget.CreatedFile("Path of the workbook to create, e.g. report.xlsx.", CellsFormats.Convertible),
                Encrypt = CellsInputs.EncryptedWorkbook,
            },
            [sheets],
            (parse, standard) =>
            {
                IReadOnlyList<string> sheetNames = ParseSheetNames(parse.GetValue(sheets));
                ResolvedOutput output = standard.Output;
                Secret? encryptPassword = standard.EncryptPassword();
                return new NewWorkbookRequest
                {
                    Output = output,
                    SheetNames = sheetNames,
                    EncryptPassword = encryptPassword,
                };
            },
            Table)
        {
            Examples = ["cells create book.xlsx --sheets \"Data,Summary\""],
        };
    }

    internal static void Table(CreateResult create, TableSurface surface) =>
        ResultText.Produced(surface, create.Output, $"sheets: {string.Join(", ", create.Sheets)}");

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
                "--sheets", "sheet names must not be empty",
                "Pass a comma-separated list such as \"Data,Summary\".");
        }

        if (names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Length)
        {
            throw CliErrors.OptionInvalid(
                "--sheets", "sheet names must be unique (case-insensitive)",
                "Rename the duplicates; Excel does not allow two sheets with the same name.");
        }

        return names;
    }
}
