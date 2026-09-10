using System.CommandLine;

namespace Aspose.Cli.Product.Slides.Commands;

internal static class SlidesOptions
{
    public static Argument<string> File() =>
        new("file") { Description = "Presentation file path." };
}
