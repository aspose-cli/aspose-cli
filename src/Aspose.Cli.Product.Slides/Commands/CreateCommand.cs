using System.CommandLine;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;

namespace Aspose.Cli.Product.Slides.Commands;

internal static class CreateCommand
{
    public static CommandDefinition<SlidesCreateRequest, SlidesCreateResult> Create()
    {
        var template = new Option<string?>("--template") { Description = "A presentation to save as the new one. Default: the engine's new, empty presentation." }.WithInput(InputKind.File);
        return new(
            "create",
            "Create a presentation: the engine's new presentation, or a copy of a template saved in the output's format.",
            new CommandTraits
            {
                Output = OutputTarget.CreatedFile("Presentation path to create.", SlidesFormats.Writable),
                Encrypt = SlidesInputs.EncryptedPresentation,
                UsesFonts = true,
            },
            [template],
            (parse, standard) => new SlidesCreateRequest
            {
                Output = standard.Output,
                Template = standard.Source(template),
                EncryptPassword = standard.EncryptPassword(),
            })
        {
            Examples =
            [
                "slides create blank.pptx",
                "slides create deck.pptx --template brand.potx",
            ],
            Links =
            [
                CommandHelpLink.Schema(SlidesModule.Manifest, "create-result", "the fields of the create result"),
            ],
        };
    }
}
