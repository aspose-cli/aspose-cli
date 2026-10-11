using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Sdk.IO;

/// <summary>
/// The facts of one file a publication writes, taken from its staged copy: a supervised worker
/// hands its outputs to the parent, which publishes them after the worker exits, so the target
/// itself may not exist yet when the result describes it. <see cref="FileObjects"/> turns it
/// into the <see cref="OutputInfo"/> a result reports.
/// </summary>
/// <param name="Path">The absolute target path.</param>
/// <param name="SizeBytes">The file size in bytes.</param>
/// <param name="Fingerprint">The SHA-256 identity of the file's content.</param>
public sealed record PublishedFile(string Path, long SizeBytes, FileFingerprint Fingerprint);
