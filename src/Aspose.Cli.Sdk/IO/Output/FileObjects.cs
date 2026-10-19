using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Formats;
using Aspose.Cli.Sdk.IO.Files;

namespace Aspose.Cli.Sdk.IO.Output;

/// <summary>
/// The one factory of the file objects a result reports: the <see cref="InputInfo"/> of a file a
/// command read, the <see cref="SourceInput"/> of a file an option named, and the
/// <see cref="OutputInfo"/> of a file it produced. Each makes the path
/// absolute and takes the size and the fingerprint from the file itself, or from the staged
/// copy its publication wrote, so every result describes its files the same way; a product
/// supplies only what the file cannot say, its format and whether it is encrypted. Several produced files become a flat <c>outputs</c> list
/// of a product's <see cref="OutputItem"/> records, built from these descriptions.
/// </summary>
public static class FileObjects
{
    /// <summary>Describes a file a command read, as it is now.</summary>
    /// <param name="path">The file; it is made absolute.</param>
    /// <param name="format">The format id the product detected.</param>
    /// <param name="encrypted">Whether the file opens only with its password.</param>
    public static InputInfo Input(string path, string format, bool encrypted)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(format);
        string fullPath = Path.GetFullPath(path);
        FileFingerprint fingerprint = FileFingerprints.Capture(fullPath);
        return new InputInfo
        {
            Path = fullPath,
            Format = format,
            SizeBytes = new FileInfo(fullPath).Length,
            Fingerprint = fingerprint,
            Encrypted = encrypted,
        };
    }

    /// <summary>
    /// Describes the files a command read as the values of its options, as they are now, in the
    /// order the command line named them; a source the command line left out is null and is not
    /// reported.
    /// </summary>
    /// <param name="sources">Each file, with the format id it was read as and whether it opens only with its password.</param>
    public static IReadOnlyList<SourceInput> Sources(params (SourceFile? File, string Format, bool Encrypted)[] sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        return [.. sources
            .Where(static source => source.File is not null)
            .OrderBy(static source => source.File!.Position)
            .Select(static source =>
            {
                InputInfo input = Input(source.File!.Path, source.Format, source.Encrypted);
                return new SourceInput
                {
                    Path = input.Path,
                    Format = input.Format,
                    SizeBytes = input.SizeBytes,
                    Fingerprint = input.Fingerprint,
                    Encrypted = input.Encrypted,
                    Option = source.File.Option,
                };
            })];
    }

    /// <summary>Describes a published file of <paramref name="format"/>.</summary>
    /// <param name="file">The file as its publication wrote it.</param>
    /// <param name="format">The format the file is written in.</param>
    /// <param name="encrypted">Whether the product wrote the file with a password; reported only when the format can carry one.</param>
    public static OutputInfo Output(PublishedFile file, FormatDescriptor format, bool encrypted)
    {
        ArgumentNullException.ThrowIfNull(format);
        return Describe(file, format.Id, format.Protectable ? encrypted : null);
    }

    /// <summary>
    /// Describes a file whose format is its file extension: a companion the engine wrote beside
    /// an output, such as a script of an HTML5 deck, or a file an extraction took out of a
    /// document, such as an image, a table or an attachment.
    /// </summary>
    /// <param name="file">The file as its publication wrote it.</param>
    public static OutputInfo Companion(PublishedFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        string extension = Path.GetExtension(file.Path).TrimStart('.').ToLowerInvariant();
        return Describe(file, extension.Length == 0 ? "bin" : extension, encrypted: null);
    }

    private static OutputInfo Describe(PublishedFile file, string format, bool? encrypted)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentException.ThrowIfNullOrWhiteSpace(format);
        return new OutputInfo
        {
            Path = Path.GetFullPath(file.Path),
            Format = format,
            SizeBytes = file.SizeBytes,
            Fingerprint = file.Fingerprint,
            Encrypted = encrypted,
        };
    }
}
