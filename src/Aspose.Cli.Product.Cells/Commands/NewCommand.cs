using System.CommandLine;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;

namespace Aspose.Cli.Product.Cells.Commands;

/// <summary><c>cells create</c> — create a blank workbook.</summary>
internal static class NewCommand
{
    public static Command Create(IProductCommandHost<IWorkbookEngine> host)
    {
        var sheets = new Option<string?>("--sheets")
        {
            Description = "Comma-separated sheet names, e.g. \"Data,Summary\". Default: one sheet named Sheet1.",
        }.WithInput(InputKind.None);
        return StandardCommand.Create(
            host,
            "create",
            "Create a new workbook.",
            new CommandTraits
            {
                Output = OutputTarget.CreatedFile("Path of the workbook to create, e.g. report.xlsx."),
                Encrypt = CellsCommands.EncryptedWorkbook,
            },
            [sheets],
            (parse, standard) =>
            {
                IReadOnlyList<string> sheetNames = ParseSheetNames(parse.GetValue(sheets));
                string outputPath = standard.CreatedPath;
                string? encryptPassword = standard.EncryptPassword(CellsFormats.ForOutputPath(outputPath));
                return standard.Port.CreateWorkbook(new NewWorkbookRequest
                {
                    OutputPath = outputPath,
                    Overwrite = standard.Overwrite,
                    SheetNames = sheetNames,
                    EncryptPassword = encryptPassword,
                });
            }).WithExamples(["cells create book.xlsx --sheets \"Data,Summary\""]);
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
