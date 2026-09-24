using System.CommandLine;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;

namespace Aspose.Cli.Product.Cells.Commands;

/// <summary>
/// <c>cells query search</c>: finds cells whose value or formula matches a
/// pattern, without dumping whole sheets. Hits are budgeted (<c>--max-hits</c>)
/// so a search over a 40-sheet workbook stays affordable for an agent.
/// </summary>
internal static class SearchCommand
{
    private const string InValues = "values";
    private const string InFormulas = "formulas";
    private const string InBoth = "both";

    public static Command Create(IProductCommandHost<IWorkbookEngine> host)
    {
        var search = new SearchOptions(new SearchScopeGrammar(
            "Where to search: values (default), formulas or both.",
            [InValues, InFormulas, InBoth],
            InValues));
        var sheet = new Option<string?>("--sheet") { Description = "Restrict to one sheet. Default: all sheets." }.WithInput(InputKind.None);
        return StandardCommand.Create(
            host,
            "search",
            "Find cells whose value or formula matches a pattern.",
            new CommandTraits { Input = CellsCommands.Workbook("Workbook to search.") },
            [.. search.Options, sheet],
            (parse, standard) =>
            {
                SearchQuery query = search.Read(parse);
                return standard.OpenEngine().Search(standard.Input, new SearchRequest
                {
                    Pattern = query.Text.Pattern,
                    Regex = query.Text.Expression is not null,
                    In = query.Scope switch
                    {
                        InFormulas => SearchIn.Formulas,
                        InBoth => SearchIn.Both,
                        _ => SearchIn.Values,
                    },
                    SheetName = parse.GetValue(sheet),
                    MaxHits = query.MaxHits,
                    CaseSensitive = query.Text.CaseSensitive,
                    Password = standard.InputPassword,
                });
            }).WithExamples(
            [
                "cells query search book.xlsx --pattern TODO --output json",
                "cells query search book.xlsx --pattern SUM --scope formulas --max-hits 10",
            ]);
    }
}
