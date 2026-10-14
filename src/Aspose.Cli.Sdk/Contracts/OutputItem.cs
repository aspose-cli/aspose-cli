namespace Aspose.Cli.Sdk.Contracts;

/// <summary>
/// One file of an output written as several files, the item of a flat <c>outputs</c> list:
/// <c>{&lt;unit&gt;?, path, format, sizeBytes, fingerprint, encrypted?}</c>. A product derives one record per
/// unit, such as a page, that declares only the unit member, and builds it from the
/// <see cref="OutputInfo"/> <c>FileObjects.Output</c> describes; the record may be inherited
/// across assemblies.
/// </summary>
public abstract record OutputItem
{
    /// <summary>Copies the file facts of <paramref name="file"/>.</summary>
    /// <param name="file">The published file, as <c>FileObjects.Output</c> describes it.</param>
    protected OutputItem(OutputInfo file)
    {
        ArgumentNullException.ThrowIfNull(file);
        Path = file.Path;
        Format = file.Format;
        SizeBytes = file.SizeBytes;
        Fingerprint = file.Fingerprint;
        Encrypted = file.Encrypted;
    }

    /// <summary>Absolute path of the produced file.</summary>
    public string Path { get; init; }

    /// <summary>Format id of the produced file, e.g. <c>png</c>.</summary>
    public string Format { get; init; }

    /// <summary>File size in bytes.</summary>
    [Minimum(0)]
    public long SizeBytes { get; init; }

    /// <summary>SHA-256 identity of the published file.</summary>
    public FileFingerprint Fingerprint { get; init; }

    /// <summary>
    /// <c>true</c> when the produced file is encrypted and opens only with its password;
    /// omitted when its format cannot carry a password.
    /// </summary>
    public bool? Encrypted { get; init; }
}
