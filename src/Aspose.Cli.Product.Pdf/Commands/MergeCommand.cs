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
        var output = new Option<string>("--out", "-o") { Required = true, Description = "Merged PDF output path." }.WithInput(InputKind.None);
        Option<bool> overwrite = OutputOptions.Overwrite();
        var bookmarks = new Option<string>("--bookmarks")
        {
            DefaultValueFactory = _ => "preserve",
            Description = "preserve or drop input bookmarks.",
        }.WithInput(InputKind.None);
        bookmarks.AcceptOnlyFromAmong("preserve", "drop");
        var password = new PasswordOptions("--password", "all input PDFs");
        var command = new Command("merge", "Merge PDF inputs in order.");
        command.Arguments.Add(inputs);
        command.Options.Add(output);
        command.Options.Add(overwrite);
        command.Options.Add(bookmarks);
        password.AddTo(command);
        command.SetAction(parse => host.Run(parse, context =>
            context.Port.Merge(new PdfMergeRequest
            {
                InputPaths = parse.GetRequiredValue(inputs).Select(context.Paths.ResolveInput).ToArray(),
                OutputPath = context.Paths.ResolveOutput(parse.GetRequiredValue(output)),
                Overwrite = parse.GetValue(overwrite),
                PreserveBookmarks = (parse.GetValue(bookmarks) ?? "preserve") == "preserve",
                Password = password.Resolve(parse, context.Inputs, context.ReadEnvironment),
            })));
        return command;
    }
}
