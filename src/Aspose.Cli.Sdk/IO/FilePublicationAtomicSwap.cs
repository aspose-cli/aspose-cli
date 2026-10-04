using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Sdk.IO;

/// <summary>Publishes one staged file while preserving the exact displaced target.</summary>
internal static class FilePublicationAtomicSwap
{
    public static FilePublicationSnapshot Publish(
        string temporary,
        string target,
        bool overwrite,
        FilePublicationSnapshot expectedTarget,
        FilePublicationSnapshot metadataSource,
        string? retainedDisplacedPath = null,
        FilePublicationSnapshot? expectedStage = null,
        Action? beforeSwap = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(temporary);
        ArgumentException.ThrowIfNullOrEmpty(target);
        ArgumentNullException.ThrowIfNull(expectedTarget);
        ArgumentNullException.ThrowIfNull(metadataSource);
        OutputPathValidator.EnsureSafeFile(target);
        metadataSource.Metadata?.Apply(temporary);
        FilePublicationSnapshot staged = FilePublicationSnapshot.Capture(temporary);
        if (expectedStage is not null
            && !expectedStage.VersionEquals(staged))
        {
            throw new IOException(
                $"Staged file '{temporary}' changed after verification.");
        }
        beforeSwap?.Invoke();

        if (!expectedTarget.Exists)
        {
            File.Move(temporary, target, overwrite: false);
            return VerifyPublished(target, staged, displacedPath: null);
        }

        if (!overwrite)
        {
            throw CliErrors.OutputExists(target);
        }

        if (OperatingSystem.IsWindows())
        {
            return PublishWindows(
                temporary,
                target,
                expectedTarget,
                staged,
                retainedDisplacedPath);
        }

        return PublishPortable(
            temporary,
            target,
            expectedTarget,
            staged,
            retainedDisplacedPath);
    }

    private static FilePublicationSnapshot PublishWindows(
        string temporary,
        string target,
        FilePublicationSnapshot expectedTarget,
        FilePublicationSnapshot staged,
        string? retainedDisplacedPath)
    {
        string displacedPath = retainedDisplacedPath
            ?? CreateSiblingPath(target, "displaced");
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
            RestoreWindowsAfterVerificationFailure(
                temporary,
                target,
                displacedPath,
                retainedDisplacedPath,
                verificationFailure);
            throw;
        }
        FilePublicationSnapshot displaced = FilePublicationSnapshot.Capture(displacedPath);
        if (expectedTarget.VersionEquals(displaced))
        {
            if (retainedDisplacedPath is null)
            {
                DeleteOwnedOrThrow(displacedPath, displaced);
            }
            return published;
        }

        string publishedPath = retainedDisplacedPath is null
            ? CreateSiblingPath(target, "conflict")
            : temporary;
        try
        {
            File.Replace(
                displacedPath,
                target,
                publishedPath,
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

        if (!FilePublicationOwnedDelete.TryDelete(publishedPath, staged))
        {
            throw new IOException(
                $"Target '{target}' changed during publication. Conflicting content was preserved at '{publishedPath}'.");
        }

        throw CliErrors.OutputConflict(target, expectedTarget, displaced);
    }

    private static FilePublicationSnapshot PublishPortable(
        string temporary,
        string target,
        FilePublicationSnapshot expectedTarget,
        FilePublicationSnapshot staged,
        string? retainedDisplacedPath)
    {
        string displacedPath = retainedDisplacedPath
            ?? CreateSiblingPath(target, "displaced");
        File.Move(target, displacedPath, overwrite: false);
        FilePublicationSnapshot displaced = FilePublicationSnapshot.Capture(displacedPath);
        if (!expectedTarget.VersionEquals(displaced))
        {
            RestorePortable(displacedPath, target);
            throw CliErrors.OutputConflict(target, expectedTarget, displaced);
        }

        try
        {
            File.Move(temporary, target, overwrite: false);
        }
        catch
        {
            RestorePortable(displacedPath, target);
            throw;
        }

        FilePublicationSnapshot published;
        try
        {
            published = VerifyPublished(target, staged, displacedPath);
        }
        catch (Exception verificationFailure)
        {
            RestorePortableAfterVerificationFailure(
                temporary,
                target,
                displacedPath,
                retainedDisplacedPath,
                verificationFailure);
            throw;
        }

        if (retainedDisplacedPath is not null)
        {
            return published;
        }

        if (!FilePublicationOwnedDelete.TryDelete(displacedPath, displaced))
        {
            string publishedPath = CreateSiblingPath(target, "conflict");
            File.Move(target, publishedPath, overwrite: false);
            RestorePortable(displacedPath, target);
            if (!FilePublicationOwnedDelete.TryDelete(publishedPath, staged))
            {
                throw new IOException(
                    $"Published content was preserved at '{publishedPath}' after cleanup ownership changed.");
            }
            throw new IOException(
                $"The displaced target '{displacedPath}' changed during publication cleanup.");
        }
        return published;
    }

    private static void RestoreWindowsAfterVerificationFailure(
        string temporary,
        string target,
        string displacedPath,
        string? retainedDisplacedPath,
        Exception verificationFailure)
    {
        FilePublicationSnapshot displaced =
            FilePublicationSnapshot.Capture(displacedPath);
        FilePublicationSnapshot unverified =
            FilePublicationSnapshot.Capture(target);
        string unverifiedPath = retainedDisplacedPath is null
            ? CreateSiblingPath(target, "unverified")
            : temporary;
        try
        {
            File.Replace(
                displacedPath,
                target,
                unverifiedPath,
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
        if (retainedDisplacedPath is null
            && !FilePublicationOwnedDelete.TryDelete(unverifiedPath, unverified))
        {
            throw new IOException(
                $"Published target '{target}' was restored, but unverified content was preserved at '{unverifiedPath}'.",
                verificationFailure);
        }
        throw new IOException(
            $"Published target '{target}' failed verification and was rolled back.",
            verificationFailure);
    }

    private static void RestorePortableAfterVerificationFailure(
        string temporary,
        string target,
        string displacedPath,
        string? retainedDisplacedPath,
        Exception verificationFailure)
    {
        FilePublicationSnapshot displaced =
            FilePublicationSnapshot.Capture(displacedPath);
        FilePublicationSnapshot unverified =
            FilePublicationSnapshot.Capture(target);
        string unverifiedPath = retainedDisplacedPath is null
            ? CreateSiblingPath(target, "unverified")
            : temporary;
        try
        {
            File.Move(target, unverifiedPath, overwrite: false);
            RestorePortable(displacedPath, target);
        }
        catch (Exception restoreFailure)
        {
            throw new IOException(
                $"Published target '{target}' failed verification and rollback was incomplete.",
                new AggregateException(verificationFailure, restoreFailure));
        }
        if (!displaced.VersionEquals(FilePublicationSnapshot.Capture(target)))
        {
            throw new IOException(
                $"Published target '{target}' failed verification and rollback could not be verified.",
                verificationFailure);
        }
        if (retainedDisplacedPath is null
            && !FilePublicationOwnedDelete.TryDelete(unverifiedPath, unverified))
        {
            throw new IOException(
                $"Published target '{target}' was restored, but unverified content was preserved at '{unverifiedPath}'.",
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

    private static void RestorePortable(string displacedPath, string target)
    {
        if (File.Exists(target))
        {
            throw new IOException(
                $"Target '{target}' was recreated while publication recovery was in progress. The prior file remains at '{displacedPath}'.");
        }
        File.Move(displacedPath, target, overwrite: false);
    }

    private static void DeleteOwnedOrThrow(
        string path,
        FilePublicationSnapshot expected)
    {
        if (!FilePublicationOwnedDelete.TryDelete(path, expected))
        {
            throw new IOException(
                $"A displaced publication file changed and was preserved at '{path}'.");
        }
    }

    private static string CreateSiblingPath(string target, string kind) =>
        Path.Combine(
            Path.GetDirectoryName(target)
                ?? throw new IOException($"Target '{target}' has no parent directory."),
            $".{Path.GetFileName(target)}.{Guid.NewGuid():N}.{kind}");
}
