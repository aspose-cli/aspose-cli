using System.CommandLine;
using Aspose.Cli.Sdk.Extensibility.Output;
using Aspose.Cli.Sdk.Text;

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

    public static CommandDefinition<SearchRequest, SearchResult> Create()
    {
        var search = new SearchOptions(new SearchScopeGrammar(
            "Where to search: values (default), formulas or both.",
            [InValues, InFormulas, InBoth],
            InValues));
        var sheet = new Option<string?>("--sheet") { Description = "Restrict to one sheet. Default: all sheets." }.WithInput(InputKind.None);
        return new(
            "search",
            "Find cells whose value or formula matches a pattern.",
            new CommandTraits { Input = CellsTraits.Workbook("Workbook to search.") },
            [.. search.Options, sheet],
            (parse, standard) =>
            {
                SearchQuery query = search.Read(parse);
                return new SearchRequest
                {
                    Input = standard.Input,
                    Query = query,
                    In = query.Scope switch
                    {
                        InFormulas => SearchIn.Formulas,
                        InBoth => SearchIn.Both,
                        _ => SearchIn.Values,
                    },
                    SheetName = parse.GetValue(sheet),
                    Password = standard.InputPassword,
                };
            },
            Table)
        {
            Finish = static (_, request, result, standard) =>
            {
                ContinuationCommand resume = standard.Continuation();
                if (request.SheetName is { } sheetName)
                {
                    resume.Option("--sheet", sheetName);
                }

                return result with { Window = SearchOptions.Continue(request.Query, result.Window!, resume) };
            },
            Examples =
            [
                "cells query search book.xlsx --pattern TODO --output json",
                "cells query search book.xlsx --pattern SUM --scope formulas --max-hits 10",
            ],
        };
    }

    internal static void Table(SearchResult search, TableSurface surface)
    {
        surface.Out.WriteLine($"{search.Hits.Count} hit(s) for '{search.Pattern}' in {search.Source.Path}");

        if (search.Hits.Count > 0)
        {
            surface.Out.WriteLine();
            var table = new TextTable("sheet", "cell", "value");
            foreach (SearchHit hit in search.Hits)
            {
                table.AddRow(hit.Sheet, hit.Cell, hit.Value);
            }

            table.WriteTo(surface.Out, surface.Format);
        }
    }
}
