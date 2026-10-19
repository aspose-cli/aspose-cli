using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Sdk.IO;

/// <summary>Publishes one staged file while preserving the exact displaced target.</summary>
internal static class FilePublicationAtomicSwap
{
    /// <summary>
    /// Moves <paramref name="temporary"/> onto <paramref name="target"/>. A replaced target is
    /// kept at <paramref name="displacedPath"/>, which the caller owns; after a failed swap the
    /// path that held <paramref name="temporary"/> receives whatever the rollback moved aside.
    /// Returns the verified snapshots of the published target and of the displaced file, which
    /// is null when no target was replaced.
    /// </summary>
    public static (FilePublicationSnapshot Published, FilePublicationSnapshot? Displaced) Publish(
        string temporary,
        string target,
        bool overwrite,
        FilePublicationSnapshot expectedTarget,
        FilePublicationSnapshot metadataSource,
        string? displacedPath,
        FilePublicationSnapshot expectedStage)
    {
        ArgumentException.ThrowIfNullOrEmpty(temporary);
        ArgumentException.ThrowIfNullOrEmpty(target);
        ArgumentNullException.ThrowIfNull(expectedTarget);
        ArgumentNullException.ThrowIfNull(metadataSource);
        ArgumentNullException.ThrowIfNull(expectedStage);
        OutputPathValidator.EnsureSafeFile(target);
        metadataSource.Metadata?.Apply(temporary);
        FilePublicationSnapshot staged = FilePublicationSnapshot.Capture(temporary);
        if (!expectedStage.VersionEquals(staged))
        {
            throw new IOException(
                $"Staged file '{temporary}' changed after verification.");
        }

        if (!expectedTarget.Exists)
        {
            File.Move(temporary, target, overwrite: false);
            return (VerifyPublished(target, staged, displacedPath: null), null);
        }

        if (!overwrite)
        {
            throw CliErrors.OutputExists(target);
        }

        ArgumentException.ThrowIfNullOrEmpty(displacedPath);
        return Replace(
            temporary,
            target,
            expectedTarget,
            staged,
            displacedPath);
    }

    private static (FilePublicationSnapshot Published, FilePublicationSnapshot Displaced) Replace(
        string temporary,
        string target,
        FilePublicationSnapshot expectedTarget,
        FilePublicationSnapshot staged,
        string displacedPath)
    {
        try
        {
            File.Replace(
                temporary,
                target,
                displacedPath,
                ignoreMetadataErrors: false);
        }
        catch (IOException exception) when (FileAccessProbe.IsSharingViolation(exception))
        {
            throw CliErrors.OutputUnwritable(
                target, "another application has the file open", exception, "replace");
        }
        FilePublicationSnapshot published;
        try
        {
            published = VerifyPublished(target, staged, displacedPath);
        }
        catch (Exception verificationFailure)
        {
            RestoreAfterVerificationFailure(
                temporary,
                target,
                displacedPath,
                verificationFailure);
            throw;
        }
        FilePublicationSnapshot displaced = FilePublicationSnapshot.Capture(displacedPath);
        if (expectedTarget.VersionEquals(displaced))
        {
            return (published, displaced);
        }

        try
        {
            File.Replace(
                displacedPath,
                target,
                temporary,
                ignoreMetadataErrors: false);
        }
        catch (Exception restoreFailure)
        {
            throw new IOException(
                $"Target '{target}' changed during publication. The displaced file was preserved at '{displacedPath}'.",
                restoreFailure);
        }

        if (!displaced.VersionEquals(FilePublicationSnapshot.Capture(target)))
        {
            throw new IOException(
                $"Target '{target}' changed during conflict recovery. Files were preserved for manual recovery.");
        }

        if (!FilePublicationOwnedDelete.TryDelete(temporary, staged))
        {
            throw new IOException(
                $"Target '{target}' changed during publication. Conflicting content was preserved at '{temporary}'.");
        }

        throw PublicationErrors.OutputConflict(target, expectedTarget, displaced);
    }

    private static void RestoreAfterVerificationFailure(
        string temporary,
        string target,
        string displacedPath,
        Exception verificationFailure)
    {
        FilePublicationSnapshot displaced =
            FilePublicationSnapshot.Capture(displacedPath);
        try
        {
            File.Replace(
                displacedPath,
                target,
                temporary,
                ignoreMetadataErrors: false);
        }
        catch (Exception restoreFailure)
        {
            throw new IOException(
                $"Published target '{target}' failed verification and its prior content remains at '{displacedPath}'.",
                new AggregateException(verificationFailure, restoreFailure));
        }
        if (!displaced.VersionEquals(FilePublicationSnapshot.Capture(target)))
        {
            throw new IOException(
                $"Published target '{target}' failed verification and rollback could not be verified.",
                verificationFailure);
        }
        throw new IOException(
            $"Published target '{target}' failed verification and was rolled back.",
            verificationFailure);
    }

    private static FilePublicationSnapshot VerifyPublished(
        string target,
        FilePublicationSnapshot staged,
        string? displacedPath)
    {
        FilePublicationSnapshot published =
            FilePublicationSnapshot.Capture(target);
        if (!staged.VersionEquals(published))
        {
            string preserved = displacedPath is null
                ? string.Empty
                : $" The original file remains at '{displacedPath}'.";
            throw new IOException(
                $"Published target '{target}' changed before verification.{preserved}");
        }
        return published;
    }
}
