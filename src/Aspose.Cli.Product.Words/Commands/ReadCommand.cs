using System.CommandLine;
using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Words.Commands;

internal static class ReadCommand
{
    public static Command Create(IProductCommandHost<IDocumentEngine> host)
    {
        Argument<string> file = WordsOptions.File();
        var blocks = new Option<string?>("--blocks") { Description = "1-based block range, e.g. 1-20,25." }.WithInput(InputKind.None);
        var section = new Option<int?>("--section") { Description = "Read one 1-based section." };
        var scope = new Option<string>("--scope")
        {
            Description = "Projection: text, full or outline.",
            DefaultValueFactory = _ => "text",
        }.WithInput(InputKind.None);
        scope.AcceptOnlyFromAmong([.. DocumentReadScopes.All]);
        var maxChars = new Option<int>("--max-chars") { DefaultValueFactory = _ => 20_000, Description = "Maximum returned content characters, including repeated text/run projections." };
        var maxBlocks = new Option<int>("--max-blocks") { DefaultValueFactory = _ => 200, Description = "Maximum projected blocks." };
        var password = new PasswordOptions("--password", "the document");

        var command = new Command("blocks", "Read a bounded, stable window of document blocks.");
        command.Arguments.Add(file);
        command.Options.Add(blocks);
        command.Options.Add(section);
        command.Options.Add(scope);
        command.Options.Add(maxChars);
        command.Options.Add(maxBlocks);
        password.AddTo(command);
        command.SetAction(parse => host.Run(parse, context =>
        {
            string? range = parse.GetValue(blocks);
            int? selectedSection = parse.GetValue(section);
            if (range is not null && selectedSection is not null)
            {
                throw CliErrors.OptionInvalid("--section", "cannot be combined with --blocks", "Choose a section or a block range.");
            }

            int characters = parse.GetValue(maxChars);
            int count = parse.GetValue(maxBlocks);
            OptionGuards.EnsureInRange("--max-chars", characters, 1, 10_000_000, "Use a positive bounded character budget.");
            OptionGuards.EnsureInRange("--max-blocks", count, 1, 100_000, "Use a positive bounded block budget.");
            return context.Port.Read(
                context.Paths.ResolveInput(parse.GetRequiredValue(file)),
                new DocumentReadRequest
                {
                    Blocks = range is null ? null : PageRange.Parse(range),
                    Section = selectedSection,
                    Scope = parse.GetValue(scope) ?? "text",
                    MaxCharacters = characters,
                    MaxBlocks = count,
                    Password = password.Resolve(parse, context.Inputs, context.ReadEnvironment),
                });
        }));
        return command;
    }
}
