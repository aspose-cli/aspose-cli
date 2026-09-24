using System.CommandLine;
using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Words.Commands;

internal static class ConvertCommand
{
    public static Command Create(IProductCommandHost<IDocumentEngine> host)
    {
        Argument<string> file = WordsOptions.File();
        var to = new Option<string>("--to") { Required = true, Description = "Target document format." }.WithInput(InputKind.None);
        to.AcceptOnlyFromAmong([.. WordsFormats.Definitions.IdsFor(FormatUse.Convert)]);
        var pages = new Option<string?>("--pages") { Description = "1-based pages for fixed-page targets only." }.WithInput(InputKind.None);
        var output = new OutputFileOptions("Output path; defaults to a sibling using the target extension.");
        var password = new PasswordOptions("--password", "the document");
        var encrypt = new PasswordOptions("--encrypt", "the output document", allowStdin: false);
        var fonts = new FontDirectoryOptions();

        var command = new Command("convert", "Convert a document using the detected input format.");
        command.Arguments.Add(file);
        command.Options.Add(to);
        command.Options.Add(pages);
        output.AddTo(command);
        password.AddTo(command);
        encrypt.AddTo(command);
        fonts.AddTo(command);
        command.SetAction(parse => host.Run(parse, context =>
        {
            string format = parse.GetRequiredValue(to);
            string? pageText = parse.GetValue(pages);
            if (pageText is not null && !WordsFormats.FixedPageConvertIds.Contains(format, StringComparer.Ordinal))
            {
                throw CliErrors.OptionInvalid("--pages", $"'{format}' is a flow format", "Use --pages only with PDF, XPS, OpenXPS, PS or PCL.");
            }

            string input = context.Paths.ResolveInput(parse.GetRequiredValue(file));
            using IDisposable fontScope = fonts.Use(parse, context);
            return context.Port.Convert(input, new WordsConvertRequest
            {
                TargetFormatId = format,
                OutputPath = output.ResolvePath(parse, context.Paths, input, WordsFormats.Definitions.ExtensionFor(format)),
                Overwrite = output.Overwrite(parse),
                Pages = pageText is null ? null : PageRange.Parse(pageText),
                Password = password.Resolve(parse, context.Inputs, context.ReadEnvironment),
                EncryptPassword = encrypt.Resolve(parse, context.Inputs, context.ReadEnvironment),
            });
        }));
        return command;
    }
}
