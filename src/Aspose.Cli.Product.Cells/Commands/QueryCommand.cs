using System.CommandLine;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Cells.Commands;

/// <summary>Groups strongly typed, read-only Cells projections.</summary>
internal static class QueryCommand
{
    public static Command Create(IProductCommandHost<ICellsEngine> host)
    {
        var query = new Command(
            "query",
            "Read bounded workbook data without mutating the source file.");
        query.Subcommands.Add(ReadCommand.Create(host));
        query.Subcommands.Add(SearchCommand.Create(host));
        return query;
    }
}
