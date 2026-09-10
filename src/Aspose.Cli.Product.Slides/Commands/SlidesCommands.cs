using System.CommandLine;
using Aspose.Cli.Product.Slides.Contracts;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Slides.Commands;

/// <summary>The Slides product command group.</summary>
internal static class SlidesCommands
{
    public static Command Create(IProductCommandHost<IPresentationEngine> host)
    {
        var slides = new Command("slides", "Presentation automation with slide, layout and notes semantics.");
        slides.Subcommands.Add(InfoCommand.Create(host));
        slides.Subcommands.Add(QueryCommand.Create(host));
        slides.Subcommands.Add(ConvertCommand.Create(host));
        slides.Subcommands.Add(RenderCommand.Create(host));
        slides.Subcommands.Add(NewCommand.Create(host));
        slides.Subcommands.Add(EditCommand.Create(host));
        slides.Subcommands.Add(ExtractCommand.Create(host));
        SlidesHelpMetadata.Attach(slides);
        return slides;
    }
}
