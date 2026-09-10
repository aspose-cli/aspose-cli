using System.CommandLine;
using Aspose.Cli.Product.Slides.Contracts;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Slides.Commands;

/// <summary>Groups strongly typed, read-only Slides projections.</summary>
internal static class QueryCommand
{
    public static Command Create(IProductCommandHost<IPresentationEngine> host)
    {
        var query = new Command(
            "query",
            "Read bounded presentation projections without mutating the source.");
        query.Subcommands.Add(ReadCommand.Create(host));
        query.Subcommands.Add(SearchCommand.Create(host));
        return query;
    }
}
