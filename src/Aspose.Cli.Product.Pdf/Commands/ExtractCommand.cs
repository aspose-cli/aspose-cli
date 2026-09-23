using System.CommandLine;
using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class ExtractCommand
{
    public static Command Create(IProductCommandHost<IPdfEngine> host)
    {
        Argument<string> file = PdfOptions.File();
        var what = new Option<string>("--what") { Required = true, Description = "images, attachments, text, tables or forms." }.WithInput(InputKind.None);
        what.AcceptOnlyFromAmong([.. PdfExtractKinds.All, "forms"]);
        var pages = new Option<string?>("--pages") { Description = "Optional page range for images, text or tables." }.WithInput(InputKind.None);
        var outDirectory = new OutputDirectoryOption("Safe extraction directory; required unless --what forms.", required: false);
        var to = new Option<string?>("--to") { Description = "Form export format: json, fdf or xfdf; only with --what forms." }.WithInput(InputKind.None);
        to.AcceptOnlyFromAmong("json", "fdf", "xfdf");
        var outFile = new Option<string?>("--out", "-o") { Description = "Form-data output file; only with --what forms. Default extension follows --to." }.WithInput(InputKind.None);
        Option<bool> overwrite = OutputOptions.Overwrite();
        var password = new PasswordOptions("--password", "the PDF");
        var command = new Command("extract", "Extract bounded PDF assets, text, tables or form data.");
        command.Arguments.Add(file);
        command.Options.Add(what);
        command.Options.Add(pages);
        outDirectory.AddTo(command);
        command.Options.Add(to);
        command.Options.Add(outFile);
        command.Options.Add(overwrite);
        password.AddTo(command);
        command.SetAction(parse => host.Run(parse, context =>
        {
            string kind = parse.GetRequiredValue(what);
            string? pageText = parse.GetValue(pages);
            string? format = parse.GetValue(to);
            string? outPath = parse.GetValue(outFile);
            string input = context.Paths.ResolveInput(parse.GetRequiredValue(file));
            if (string.Equals(kind, "forms", StringComparison.Ordinal))
            {
                if (pageText is not null || outDirectory.IsGiven(parse))
                {
                    throw CliErrors.OptionInvalid("--what", "forms cannot be combined with --pages or --out-dir", "Use --what forms --to <json|fdf|xfdf> and optionally --out.");
                }
                if (format is null)
                {
                    throw CliErrors.OptionInvalid("--to", "is required with --what forms", "Use --to json, --to fdf or --to xfdf.");
                }

                return context.Port.ExportForm(input, new PdfFormExportRequest
                {
                    TargetFormatId = format,
                    OutputPath = outPath is null
                        ? Path.ChangeExtension(input, "." + format)
                        : context.Paths.ResolveOutput(outPath),
                    Overwrite = parse.GetValue(overwrite),
                    Password = password.Resolve(parse, context.Inputs, context.ReadEnvironment),
                });
            }

            if (format is not null || outPath is not null || parse.GetValue(overwrite))
            {
                throw CliErrors.OptionInvalid("--to/--out/--overwrite", "form-output options are only valid with --what forms", "Remove them or use --what forms.");
            }
            return context.Port.Extract(
                input,
                new PdfExtractRequest
                {
                    What = kind,
                    OutputDirectory = outDirectory.ResolveRequired(parse, context.Paths),
                    Pages = pageText is null ? null : PageRange.Parse(pageText),
                    Password = password.Resolve(parse, context.Inputs, context.ReadEnvironment),
                });
        }));
        return command;
    }
}
