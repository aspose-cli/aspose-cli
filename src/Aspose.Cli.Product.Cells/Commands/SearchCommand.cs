using System.CommandLine;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Output;
using Aspose.Cli.Sdk.Text;

namespace Aspose.Cli.Product.Cells.Commands;

/// <summary>
/// <c>cells search</c>: finds cells whose value or formula matches a
/// pattern, without dumping whole sheets. Hits are budgeted (<c>--max-hits</c>)
/// so a search over a 40-sheet workbook stays affordable for an agent.
/// </summary>
internal static class SearchCommand
{
    private const string InValues = "values";
    private const string InFormulas = "formulas";
    private const string InAll = "all";

    public static CommandDefinition<CellsSearchRequest, CellsSearchResult> Create()
    {
        var search = new SearchOptions(new SearchScopeGrammar(
            "What to search: the cell results, the formula text or all of both.",
            [InValues, InFormulas, InAll],
            InValues,
            "--content"));
        var sheet = new Option<string?>("--sheet") { Description = "Restrict to one sheet. Default: all sheets." }.WithInput(InputKind.None);
        return new(
            "search",
            "Find cells whose value or formula matches a pattern.",
            new CommandTraits { Input = CellsInputs.Workbook("Workbook to search.") },
            [.. search.Options, sheet],
            (parse, standard) =>
            {
                SearchQuery query = search.Read(parse);
                return new CellsSearchRequest
                {
                    Input = standard.Input,
                    Query = query,
                    In = query.Scope switch
                    {
                        InFormulas => CellsSearchIn.Formulas,
                        InAll => CellsSearchIn.All,
                        _ => CellsSearchIn.Values,
                    },
                    SheetName = parse.GetValue(sheet),
                    Password = standard.InputPassword,
                };
            },
            Table)
        {
            MistakenFor = ["query"],
            Finish = static (_, request, result, standard) =>
            {
                ContinuationCommand resume = standard.Continuation();
                if (request.SheetName is { } sheetName)
                {
                    resume.Option("--sheet", sheetName);
                }

                return result with { Window = SearchOptions.Continue(request.Query, result.Window, resume) };
            },
            Examples =
            [
                "cells search book.xlsx --pattern TODO --output-format json",
                "cells search book.xlsx --pattern SUM --content formulas --max-hits 10",
            ],
            Links =
            [
                CommandHelpLink.Docs(CellsModule.Manifest, "overview", "what each Cells command is for"),
                new("aspose-cli docs reading", "reading every product within bounded windows"),
            ],
        };
    }

    internal static void Table(CellsSearchResult search, TableSurface surface)
    {
        surface.Out.WriteLine($"{search.Hits.Count} hit(s) for '{search.Pattern}' in {search.Input.Path}");

        if (search.Hits.Count > 0)
        {
            surface.Out.WriteLine();
            var table = new TextTable("sheet", "cell", "text");
            foreach (CellsSearchHit hit in search.Hits)
            {
                table.AddRow(hit.Sheet, hit.Cell, hit.Context);
            }

            table.WriteTo(surface.Out, surface.Format);
        }
    }
}
