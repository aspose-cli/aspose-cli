using System.CommandLine;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Cells.Commands;

/// <summary>
/// <c>aspose-cli cells query search</c>: finds cells whose value or formula matches a
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
        var fileArgument = new Argument<string>("file") { Description = "Workbook to search." }.WithInput(InputKind.File);
        var search = new SearchOptions(new SearchScopeGrammar(
            "Where to search: values (default), formulas or both.",
            [InValues, InFormulas, InBoth],
            InValues));
        var sheetOption = new Option<string?>("--sheet") { Description = "Restrict to one sheet. Default: all sheets." }.WithInput(InputKind.None);
        var password = new PasswordOptions("--password", "the workbook");

        var command = new Command("search", "Find cells whose value or formula matches a pattern.");
        command.Arguments.Add(fileArgument);
        search.AddTo(command);
        command.Options.Add(sheetOption);
        password.AddTo(command);

        command.SetAction(parseResult => host.Run(parseResult, context =>
        {
            SearchQuery query = search.Read(parseResult);
            string inputPath = context.Paths.ResolveInput(parseResult.GetRequiredValue(fileArgument));
            return context.Port.Search(inputPath, new SearchRequest
            {
                Pattern = query.Text.Pattern,
                Regex = query.Text.Expression is not null,
                In = query.Scope switch
                {
                    InFormulas => SearchIn.Formulas,
                    InBoth => SearchIn.Both,
                    _ => SearchIn.Values,
                },
                SheetName = parseResult.GetValue(sheetOption),
                MaxHits = query.MaxHits,
                CaseSensitive = query.Text.CaseSensitive,
                Password = password.Resolve(parseResult, context.Inputs, context.ReadEnvironment),
            });
        }));

        return command;
    }
}
