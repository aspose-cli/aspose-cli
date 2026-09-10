using System.CommandLine;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Cells.Commands;

/// <summary><c>aspose-cli cells create</c> — create a blank workbook.</summary>
internal static class NewCommand
{
    public static Command Create(IProductCommandHost<IWorkbookEngine> host)
    {
        var fileArgument = new Argument<string>("file")
        {
            Description = "Path of the workbook to create, e.g. report.xlsx.",
        };

        var sheetsOption = new Option<string?>("--sheets")
        {
            Description = "Comma-separated sheet names, e.g. \"Data,Summary\". Default: one sheet named Sheet1.",
        };

        var overwriteOption = OutputOptions.Overwrite();
        var encrypt = new PasswordOptions("--encrypt", "the output file", allowStdin: false);

        var create = new Command("create", "Create a new workbook.");
        create.Arguments.Add(fileArgument);
        create.Options.Add(sheetsOption);
        create.Options.Add(overwriteOption);
        encrypt.AddTo(create);

        create.SetAction(parseResult => host.Run(parseResult, context =>
        {
            IReadOnlyList<string> sheetNames = ParseSheetNames(parseResult.GetValue(sheetsOption));
            string outputPath = context.Paths.ResolveOutput(parseResult.GetRequiredValue(fileArgument));

            return context.Port.CreateWorkbook(new NewWorkbookRequest
            {
                OutputPath = outputPath,
                Overwrite = parseResult.GetValue(overwriteOption),
                SheetNames = sheetNames,
                EncryptPassword = encrypt.Resolve(parseResult, context.Inputs),
            });
        }));

        return create;
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
