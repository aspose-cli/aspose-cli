using System.CommandLine;
using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Words.Commands;

internal static class RenderCommand
{
    public static Command Create(IProductCommandHost<IDocumentEngine> host)
    {
        Argument<string> file = WordsOptions.File();
        var to = new Option<string>("--to") { DefaultValueFactory = _ => "png", Description = "png, jpeg or svg." };
        to.AcceptOnlyFromAmong([.. WordsModule.Formats.IdsFor(FormatUse.Render)]);
        var pages = new Option<string?>("--pages") { Description = "1-based page range." };
        var allPages = new Option<bool>("--all-pages") { Description = "Render every page." };
        var dpi = new Option<int>("--dpi") { DefaultValueFactory = _ => 192, Description = "Raster resolution." };
        var output = new OutputFileOptions("Output path; multi-page output adds .pN before the extension.");
        var password = new PasswordOptions("--password", "the document");

        var command = new Command("render", "Render one or more document pages.");
        command.Arguments.Add(file);
        command.Options.Add(to);
        command.Options.Add(pages);
        command.Options.Add(allPages);
        command.Options.Add(dpi);
        output.AddTo(command);
        password.AddTo(command);
        command.SetAction(parse => host.Run(parse, context =>
        {
            string? pageText = parse.GetValue(pages);
            bool everyPage = parse.GetValue(allPages);
            if (pageText is not null && everyPage)
            {
                throw CliErrors.OptionInvalid("--all-pages", "cannot be combined with --pages", "Choose a range or all pages.");
            }

            int resolution = parse.GetValue(dpi);
            OptionGuards.EnsureInRange("--dpi", resolution, 36, 1200, "Use 36-1200 DPI.");
            string format = parse.GetValue(to) ?? "png";
            string input = context.Paths.ResolveInput(parse.GetRequiredValue(file));
            return context.Port.Render(input, new WordsRenderRequest
            {
                TargetFormatId = format,
                OutputPath = output.ResolvePath(parse, context.Paths, input, WordsModule.Formats.ExtensionFor(format)),
                Overwrite = output.Overwrite(parse),
                Pages = pageText is null ? null : PageRange.Parse(pageText),
                AllPages = everyPage,
                Dpi = resolution,
                Password = password.Resolve(parse, context.Inputs),
            });
        }));
        return command;
    }
}
