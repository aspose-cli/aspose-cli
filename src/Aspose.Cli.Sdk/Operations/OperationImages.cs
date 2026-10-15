using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Sdk.Operations;

/// <summary>
/// Images that edit operations insert, read and checked as <see cref="ImageInput"/> reads and
/// checks them. A file that is not an intact image makes its operation invalid, which the
/// operation runner numbers, and the batch writes nothing.
/// </summary>
public static class OperationImages
{
    /// <summary>Reads an operation's image file as <see cref="ImageInput.Read(InputSource, string)"/> does.</summary>
    /// <param name="inputs">The invocation's input reader.</param>
    /// <param name="path">The image file the operation names.</param>
    /// <returns>The image's bytes, for the engine.</returns>
    /// <exception cref="OperationInvalidException">The file is not an intact image.</exception>
    public static byte[] Read(InputSource inputs, string path) => ImageInput.Read(inputs, path, Invalid);

    /// <summary>
    /// Reads an operation's image file as <see cref="ImageInput.Read(InputResourceScope, string, bool)"/> does.
    /// </summary>
    /// <param name="inputs">The operation inputs.</param>
    /// <param name="path">The image file the operation names.</param>
    /// <param name="refuseNetworkReferences">Whether an SVG that names a network address is refused,
    /// for an engine that would fetch it with no resource hook.</param>
    /// <returns>The image's bytes, for the engine.</returns>
    /// <exception cref="OperationInvalidException">The file is not an intact image.</exception>
    public static byte[] Read(InputResourceScope inputs, string path, bool refuseNetworkReferences = false) =>
        ImageInput.Read(inputs, path, refuseNetworkReferences, Invalid);

    private static Exception Invalid(string path, string reason) =>
        new OperationInvalidException($"image '{path}' is not a valid image: {reason}", ImageInput.Hint) { Path = path };
}
