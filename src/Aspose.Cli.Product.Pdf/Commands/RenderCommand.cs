using System.CommandLine;
using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class RenderCommand
{
    public static Command Create(IProductCommandHost<IPdfEngine> host)
    {
        Argument<string> file = PdfOptions.File();
        var to = new Option<string>("--to") { DefaultValueFactory = _ => "png", Description = "png, jpeg or svg." }.WithInput(InputKind.None);
        to.AcceptOnlyFromAmong([.. PdfFormats.Definitions.IdsFor(FormatUse.Render)]);
        var pages = new PartSelectionOptions("page");
        var dpi = new DpiOption();
        var output = new OutputFileOptions("Output path; multi-page output adds .pN before the extension.");
        var password = new PasswordOptions("--password", "the PDF");
        var fonts = new FontDirectoryOptions();
        var command = new Command("render", "Render one or more PDF pages.");
        command.Arguments.Add(file);
        command.Options.Add(to);
        pages.AddTo(command);
        dpi.AddTo(command);
        output.AddTo(command);
        password.AddTo(command);
        fonts.AddTo(command);
        command.SetAction(parse => host.Run(parse, context =>
        {
            PartSelection selection = pages.Read(parse);
            int resolution = dpi.Read(parse);
            string format = parse.GetValue(to) ?? "png";
            string input = context.Paths.ResolveInput(parse.GetRequiredValue(file));
            using IDisposable fontScope = fonts.Use(parse, context);
            return context.Port.Render(input, new PdfRenderRequest
            {
                TargetFormatId = format,
                OutputPath = output.ResolvePath(parse, context.Paths, input, PdfFormats.Definitions.ExtensionFor(format)),
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
