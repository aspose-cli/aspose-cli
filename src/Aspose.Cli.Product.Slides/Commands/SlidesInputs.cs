using Aspose.Cli.Sdk.Extensibility.Commanding;

namespace Aspose.Cli.Product.Slides.Commands;

/// <summary>The documents the Slides commands read and write.</summary>
internal static class SlidesInputs
{
    /// <summary>The presentation every reading command opens.</summary>
    public static readonly InputDocument Presentation = new("Presentation file path.", "the presentation");

    /// <summary>The password a writing command can put on its presentation.</summary>
    public static readonly EncryptedOutput EncryptedPresentation = new("the output presentation");
}
