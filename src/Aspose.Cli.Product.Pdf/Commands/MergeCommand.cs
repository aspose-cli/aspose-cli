using System.CommandLine;
using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class MergeCommand
{
    public static Command Create(IProductCommandHost<IPdfEngine> host)
    {
        var inputs = new Argument<string[]>("files")
        {
            Description = "Two or more PDF inputs in merge order.",
            Arity = ArgumentArity.OneOrMore,
        }.WithInput(InputKind.File);
        var output = new OutputFileOptions("Merged PDF output path.", required: true);
        var bookmarks = new Option<string>("--bookmarks")
        {
            DefaultValueFactory = _ => "preserve",
            Description = "preserve or drop input bookmarks.",
        }.WithInput(InputKind.None);
        bookmarks.AcceptOnlyFromAmong("preserve", "drop");
        var password = new PasswordOptions("--password", "all input PDFs");
        var command = new Command("merge", "Merge PDF inputs in order.");
        command.Arguments.Add(inputs);
        output.AddTo(command);
        command.Options.Add(bookmarks);
        password.AddTo(command);
        command.SetAction(parse => host.Run(parse, context =>
        {
            string[] inputPaths = parse.GetRequiredValue(inputs).Select(context.Paths.ResolveInput).ToArray();
            return context.Port.Merge(new PdfMergeRequest
            {
                InputPaths = inputPaths,
                OutputPath = output.ResolveRequired(parse, context.Paths, inputPaths),
                Overwrite = output.Overwrite(parse),
                PreserveBookmarks = (parse.GetValue(bookmarks) ?? "preserve") == "preserve",
                Password = password.Resolve(parse, context.Inputs, context.ReadEnvironment),
            });
        }));
        return command;
    }
}
