using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.TestKit;

/// <summary>Reads a product's format declarations the way a test names an output file.</summary>
public static class DeclaredFormats
{
    /// <summary>The extension a file in the declared format <paramref name="formatId"/> is written with.</summary>
    public static string ExtensionOf(this IEnumerable<FormatDescriptor> descriptors, string formatId)
    {
        FormatDescriptor descriptor = descriptors.Single(format =>
            string.Equals(format.Id, formatId, StringComparison.OrdinalIgnoreCase));
        return descriptor.PreferredExtension
            ?? throw new InvalidOperationException($"Format '{descriptor.Id}' has no extension.");
    }
}
