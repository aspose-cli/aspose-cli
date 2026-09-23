using System.CommandLine;
using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Words.Commands;

internal static class RenderCommand
{
    public static Command Create(IProductCommandHost<IDocumentEngine> host)
    {
        Argument<string> file = WordsOptions.File();
        var to = new Option<string>("--to") { DefaultValueFactory = _ => "png", Description = "png, jpeg or svg." }.WithInput(InputKind.None);
        to.AcceptOnlyFromAmong([.. WordsFormats.Definitions.IdsFor(FormatUse.Render)]);
        var pages = new PartSelectionOptions("page");
        var dpi = new DpiOption();
        var output = new OutputFileOptions("Output path; multi-page output adds .pN before the extension.");
        var password = new PasswordOptions("--password", "the document");

        var command = new Command("render", "Render one or more document pages.");
        command.Arguments.Add(file);
        command.Options.Add(to);
        pages.AddTo(command);
        dpi.AddTo(command);
        output.AddTo(command);
        password.AddTo(command);
        command.SetAction(parse => host.Run(parse, context =>
        {
            PartSelection selection = pages.Read(parse);
            int resolution = dpi.Read(parse);
            string format = parse.GetValue(to) ?? "png";
            string input = context.Paths.ResolveInput(parse.GetRequiredValue(file));
            return context.Port.Render(input, new WordsRenderRequest
            {
                TargetFormatId = format,
                OutputPath = output.ResolvePath(parse, context.Paths, input, WordsFormats.Definitions.ExtensionFor(format)),
                Overwrite = output.Overwrite(parse),
                Pages = selection.Range,
                AllPages = selection.All,
                Dpi = resolution,
                Password = password.Resolve(parse, context.Inputs, context.ReadEnvironment),
            });
        }));
        return command;
    }
}
