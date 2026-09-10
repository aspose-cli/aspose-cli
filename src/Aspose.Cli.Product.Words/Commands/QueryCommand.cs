using System.CommandLine;
using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Words.Commands;

/// <summary>Groups strongly typed, read-only Words projections.</summary>
internal static class QueryCommand
{
    public static Command Create(IProductCommandHost<IDocumentEngine> host)
    {
        var query = new Command(
            "query",
            "Read bounded document projections without mutating the source.");
        query.Subcommands.Add(ReadCommand.Create(host));
        query.Subcommands.Add(SearchCommand.Create(host));
        return query;
    }
}
