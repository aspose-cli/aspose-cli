using System.CommandLine;
using Aspose.Cli.Sdk.Extensibility.Commanding;

namespace Aspose.Cli.Product.Slides.Commands;

internal static class SlidesOptions
{
    public static Argument<string> File() =>
        new Argument<string>("file") { Description = "Presentation file path." }.WithInput(InputKind.File);
}
