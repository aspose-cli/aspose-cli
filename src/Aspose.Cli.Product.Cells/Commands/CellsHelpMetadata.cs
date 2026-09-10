using System.CommandLine;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Cells.Commands;

internal static class CellsHelpMetadata
{
    public static void Attach(Command root)
    {
        root.WithExamples(
            ["aspose-cli cells inspect book.xlsx --output json"],
            [
                new("aspose-cli docs editing", "the edit-operation vocabulary and recipes"),
                new("aspose-cli docs workbook-standards", "professional workbook construction guidance"),
                new("aspose-cli docs verification", "the spreadsheet delivery verification protocol"),
                new("aspose-cli schema v2/cells/ops", "the operations JSON Schema"),
            ]);

        Find(root, "inspect").WithExamples(
            [
                "aspose-cli cells inspect book.xlsx --output json",
                "aspose-cli cells inspect book.xlsx --detail charts names --preview",
            ]);
        Command query = Find(root, "query");
        Find(query, "range").WithExamples(
            [
                "aspose-cli cells query range book.xlsx --range Sales!A1:D10 --scope values --output json",
                "aspose-cli cells query range book.xlsx --sheet Sales --scope formulas --output json",
            ]);
        Find(query, "search").WithExamples(
            [
                "aspose-cli cells query search book.xlsx TODO --output json",
                "aspose-cli cells query search book.xlsx SUM --in formulas --max-hits 10",
            ]);
        Find(root, "convert").WithExamples(
            [
                "aspose-cli cells convert sales.csv --to xlsx",
                "aspose-cli cells convert book.xlsx --to pdf --out report.pdf",
            ]);
        Find(root, "render").WithExamples(
            [
                "aspose-cli cells render book.xlsx --range Sales!A1:G20 --out sales.png",
                "aspose-cli cells render book.xlsx --sheet Dashboard --out dashboard.png",
                "aspose-cli cells render book.xlsx --all-sheets --out check.png",
            ]);
        Find(root, "create").WithExamples(
            ["aspose-cli cells create book.xlsx --sheets \"Data,Summary\""]);
        Find(root, "edit").WithExamples(
            [
                "aspose-cli cells edit book.xlsx --in-place --set \"Sales!B3=42\" --set \"Sales!G2==E2*F2\"",
                "aspose-cli cells edit book.xlsx --in-place --ops '{\"ops\":[{\"op\":\"set_values\",\"sheet\":\"Sales\",\"range\":\"A1\",\"values\":[[1]]}]}'",
                "aspose-cli cells edit book.xlsx --in-place --ops '{\"ops\":[{\"op\":\"recalculate\"}]}'",
            ],
            [
                new("aspose-cli docs editing", "recipes for every operation family"),
                new("aspose-cli schema v2/cells/ops", "the operations JSON vocabulary"),
                new("aspose-cli docs verification", "verification before delivering the file"),
            ]);
        Find(root, "compare").WithExamples(
            [
                "aspose-cli cells compare old.xlsx new.xlsx --output json",
                "aspose-cli cells compare old.xlsx new.xlsx --compare values --max-diffs 50",
            ]);
    }

    private static Command Find(Command root, string name) =>
        root.Subcommands.Single(command =>
            string.Equals(command.Name, name, StringComparison.Ordinal));
}
