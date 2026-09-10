using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Sdk.Tests.IO;

public sealed class SafeFileWriterTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly SafeFileWriter _writer = TestBudgets.Writer();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Write_ProducesFileAndReportsSize()
    {
        string target = _temp.File("out.txt");

        long size = _writer.Write(target, overwrite: false, temp => File.WriteAllText(temp, "hello"));

        Assert.Equal(5, size);
        Assert.Equal("hello", File.ReadAllText(target));
    }

    [Fact]
    public void Write_CreatesMissingParentDirectories()
    {
        string target = _temp.File(Path.Combine("nested", "deeper", "out.txt"));

        _writer.Write(target, overwrite: false, temp => File.WriteAllText(temp, "x"));

        Assert.True(File.Exists(target));
    }

    [Fact]
    public void Write_LongPathStillBindsTheTemporaryFileIdentity()
    {
        string segment = new('a', 48);
        string target = _temp.File(Path.Combine(
            segment,
            segment,
            segment,
            segment,
            "out.txt"));
        Assert.True(target.Length > 260);

        _writer.Write(
            target,
            overwrite: false,
            temp => File.WriteAllText(temp, "long-path"));

        Assert.Equal("long-path", File.ReadAllText(target));
    }

    [Fact]
    public void Write_ExistingTarget_WithoutOverwrite_Throws()
    {
        string target = _temp.File("out.txt");
        File.WriteAllText(target, "original");

        CliException exception = Assert.Throws<CliException>(
            () => _writer.Write(target, overwrite: false, temp => File.WriteAllText(temp, "new")));

        Assert.Equal(ErrorCodes.OutputExists, exception.Code);
        Assert.Equal("original", File.ReadAllText(target));
    }

    [Fact]
    public void Write_ExistingTarget_WithOverwrite_Replaces()
    {
        string target = _temp.File("out.txt");
        File.WriteAllText(target, "original");

        _writer.Write(target, overwrite: true, temp => File.WriteAllText(temp, "new"));

        Assert.Equal("new", File.ReadAllText(target));
    }

    [Fact]
    public void Write_WithBackup_CreatesItBeforeReplacingTarget()
    {
        string target = _temp.File("out.txt");
        string backup = _temp.File("out.backup.txt");
        File.WriteAllText(target, "original");

        SafeWriteResult result = _writer.Write(
            target,
            overwrite: true,
            backup,
            temp => File.WriteAllText(temp, "new"));

        Assert.Equal("new", File.ReadAllText(target));
        Assert.Equal("original", File.ReadAllText(backup));
        Assert.NotNull(result.Backup);
        Assert.True(result.Backup.Created);
    }

    [Fact]
    public void Write_WithExistingBackup_NeverOverwritesIt()
    {
        string target = _temp.File("out.txt");
        string backup = _temp.File("out.backup.txt");
        File.WriteAllText(target, "current");
        File.WriteAllText(backup, "first-session");

        SafeWriteResult result = _writer.Write(
            target,
            overwrite: true,
            backup,
            temp => File.WriteAllText(temp, "new"));

        Assert.Equal("new", File.ReadAllText(target));
        Assert.Equal("first-session", File.ReadAllText(backup));
        Assert.False(result.Backup!.Created);
    }

    [Fact]
    public void Write_WhenBackupFails_DoesNotReplaceTheTarget()
    {
        string target = _temp.File("out.txt");
        string backup = _temp.File("backup-target");
        File.WriteAllText(target, "original");
        Directory.CreateDirectory(backup);

        CliException exception = Assert.Throws<CliException>(() => _writer.Write(
            target,
            overwrite: true,
            backup,
            temp => File.WriteAllText(temp, "new")));

        Assert.Equal(ErrorCodes.OutputUnwritable, exception.Code);
        Assert.Equal("backup", exception.Details!["phase"]!.GetValue<string>());
        Assert.Equal("original", File.ReadAllText(target));
    }

    [Fact]
    public void Write_CallbackFailure_LeavesNoTargetAndNoTempFiles()
    {
        string target = _temp.File("out.txt");

        Assert.Throws<InvalidOperationException>(
            () => _writer.Write(target, overwrite: false, temp =>
            {
                File.WriteAllText(temp, "partial");
                throw new InvalidOperationException("engine failure");
            }));

        Assert.False(File.Exists(target));
        Assert.Empty(Directory.GetFiles(_temp.Path));
    }

    [Fact]
    public void Write_CallbackFailure_KeepsExistingTargetIntact()
    {
        string target = _temp.File("out.txt");
        File.WriteAllText(target, "original");

        Assert.Throws<InvalidOperationException>(
            () => _writer.Write(target, overwrite: true, _ => throw new InvalidOperationException("boom")));

        Assert.Equal("original", File.ReadAllText(target));
    }

    [Fact]
    public void WriteBound_InspectsAndVerifiesTheOwnedStageBeforePublication()
    {
        string target = _temp.File("out.txt");

        _writer.WriteBound(
            target,
            overwrite: false,
            backupPath: null,
            inputPrecondition: null,
            temp => File.WriteAllText(temp, "draft"),
            (_, stream) =>
            {
                stream.SetLength(0);
                stream.Write("ready"u8);
            },
            temp => Assert.Equal("ready", File.ReadAllText(temp)));

        Assert.Equal("ready", File.ReadAllText(target));
    }

    [Fact]
    public void WriteBound_InspectionFailureKeepsExistingTargetIntact()
    {
        string target = _temp.File("out.txt");
        File.WriteAllText(target, "original");

        Assert.Throws<InvalidOperationException>(() => _writer.WriteBound(
            target,
            overwrite: true,
            backupPath: null,
            inputPrecondition: null,
            temp => File.WriteAllText(temp, "draft"),
            (_, _) => throw new InvalidOperationException("inspection failed"),
            _ => throw new InvalidOperationException("verification must not run")));

        Assert.Equal("original", File.ReadAllText(target));
        Assert.Single(Directory.GetFiles(_temp.Path));
    }

    [Fact]
    public void OwnedTemporaryFile_RejectsIdentityChangesAfterTheProducedFileIsBound()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string path = _temp.File("stage.tmp");
        string replacement = _temp.File("replacement.tmp");
        string displaced = _temp.File("displaced.tmp");
        using (var temporary = OwnedTemporaryFile.Create(path))
        {
            File.WriteAllText(path, "first");
            temporary.BindProducedFile();
            File.WriteAllText(replacement, "second");
            File.Replace(replacement, path, displaced);
            File.Delete(displaced);

            Assert.Throws<IOException>(temporary.BindProducedFile);
        }

        Assert.Equal("second", File.ReadAllText(path));
        File.Delete(path);
    }

    [Fact]
    public void AtomicPublish_RejectsAStageReplacedAfterVerification()
    {
        string stage = _temp.File("stage.tmp");
        string target = _temp.File("out.txt");
        File.WriteAllText(stage, "verified");
        FilePublicationSnapshot verified = FilePublicationSnapshot.Capture(stage);
        File.Delete(stage);
        File.WriteAllText(stage, "replacement");

        Assert.Throws<IOException>(() => FilePublicationAtomicSwap.Publish(
            stage,
            target,
            overwrite: false,
            FilePublicationSnapshot.Missing,
            FilePublicationSnapshot.Missing,
            expectedStage: verified));

        Assert.False(File.Exists(target));
        Assert.Equal("replacement", File.ReadAllText(stage));
    }

    [Fact]
    public void AtomicPublish_RestoresTheOriginalWhenStageChangesImmediatelyBeforeSwap()
    {
        string stage = _temp.File("stage.tmp");
        string target = _temp.File("out.txt");
        File.WriteAllText(stage, "verified");
        File.WriteAllText(target, "original");
        FilePublicationSnapshot verified = FilePublicationSnapshot.Capture(stage);
        FilePublicationSnapshot original = FilePublicationSnapshot.Capture(target);

        Assert.Throws<IOException>(() => FilePublicationAtomicSwap.Publish(
            stage,
            target,
            overwrite: true,
            original,
            original,
            expectedStage: verified,
            beforeSwap: () =>
            {
                File.Delete(stage);
                File.WriteAllText(stage, "replacement");
            }));

        Assert.Equal("original", File.ReadAllText(target));
        Assert.DoesNotContain(
            Directory.EnumerateFiles(_temp.Path),
            path => path.Contains("unverified", StringComparison.Ordinal));
    }

    [Fact]
    public void Write_ReservedOutputName_IsRejectedBeforeCreation()
    {
        string target = _temp.File("CON.txt");

        CliException error = Assert.Throws<CliException>(() =>
            _writer.Write(target, overwrite: false, temp =>
                File.WriteAllText(temp, "content")));

        Assert.Equal(ErrorCodes.OutputUnwritable, error.Code);
        Assert.Equal("path", error.Details!["phase"]!.GetValue<string>());
        Assert.False(File.Exists(target));
    }

    [Fact]
    public void Write_LinkedOutputDirectory_IsRejected()
    {
        string actual = _temp.File("actual");
        string linked = _temp.File("linked");
        Directory.CreateDirectory(actual);
        try
        {
            Directory.CreateSymbolicLink(linked, actual);
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException
                or IOException
                or PlatformNotSupportedException)
        {
            return;
        }

        try
        {
            string target = Path.Combine(linked, "output.txt");
            CliException error = Assert.Throws<CliException>(() =>
                _writer.Write(target, overwrite: false, temp =>
                    File.WriteAllText(temp, "content")));

            Assert.Equal(ErrorCodes.OutputUnwritable, error.Code);
            Assert.False(File.Exists(Path.Combine(actual, "output.txt")));
        }
        finally
        {
            Directory.Delete(linked, recursive: false);
        }
    }

    [Fact]
    public void Write_DanglingLinkedOutputDirectory_IsRejected()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string missing = _temp.File("missing-target");
        string linked = _temp.File("dangling-link");
        try
        {
            Directory.CreateSymbolicLink(linked, missing);
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException
                or IOException
                or PlatformNotSupportedException)
        {
            return;
        }

        try
        {
            CliException error = Assert.Throws<CliException>(() =>
                _writer.Write(
                    Path.Combine(linked, "output.txt"),
                    overwrite: false,
                    temp => File.WriteAllText(temp, "content")));

            Assert.Equal(ErrorCodes.OutputUnwritable, error.Code);
            Assert.False(Directory.Exists(missing));
        }
        finally
        {
            Directory.Delete(linked, recursive: false);
        }
    }

    [Fact]
    public void OwnedDelete_PreservesADanglingFileLink()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string link = _temp.File("dangling-file-link.txt");
        try
        {
            File.CreateSymbolicLink(
                link,
                _temp.File("missing-file.txt"));
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException
                or IOException
                or PlatformNotSupportedException)
        {
            return;
        }

        try
        {
            Assert.False(FilePublicationOwnedDelete.TryDelete(
                link,
                FilePublicationSnapshot.Missing));
            Assert.True(
                FilePublicationOwnedDelete.TryGetAttributesNoFollow(link)
                    ?.HasFlag(FileAttributes.ReparsePoint));
        }
        finally
        {
            File.Delete(link);
        }
    }

    [Fact]
    public void Write_DoesNotExposeRawIoExceptionText()
    {
        const string privateReason = "private-sentinel-do-not-expose";
        string target = _temp.File("out.txt");

        CliException error = Assert.Throws<CliException>(() =>
            _writer.Write(
                target,
                overwrite: false,
                _ => throw new IOException(privateReason)));

        Assert.Equal(ErrorCodes.OutputUnwritable, error.Code);
        Assert.Equal(
            "the staged output could not be written",
            error.Details!["reason"]!.GetValue<string>());
        Assert.DoesNotContain(privateReason, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_CallbackReplaceOnSaveBindsTheProducedFile()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string target = _temp.File("out.txt");
        string? reserved = null;

        _writer.Write(target, overwrite: false, temp =>
        {
            reserved = temp;
            File.WriteAllText(temp, "initial-content");
            string replacement = temp + ".replacement";
            string displaced = temp + ".displaced";
            File.WriteAllText(replacement, "produced-content");
            File.Replace(replacement, temp, displaced);
            File.Delete(displaced);
        });

        Assert.NotNull(reserved);
        Assert.False(File.Exists(reserved));
        Assert.Equal("produced-content", File.ReadAllText(target));
    }

    [Fact]
    public void Write_TargetChangedDuringProduction_ReturnsConflictWithoutOverwriting()
    {
        string target = _temp.File("out.txt");
        File.WriteAllText(target, "original");

        CliException error = Assert.Throws<CliException>(() =>
            _writer.Write(target, overwrite: true, temp =>
            {
                File.WriteAllText(temp, "replacement");
                File.WriteAllText(target, "external-update");
            }));

        Assert.Equal(ErrorCodes.OutputConflict, error.Code);
        Assert.Equal("external-update", File.ReadAllText(target));
        Assert.DoesNotContain(
            Directory.EnumerateFiles(_temp.Path),
            path => path.EndsWith(".tmp", StringComparison.Ordinal));
    }

    [Fact]
    public void Write_InputChangedAfterAdmission_RejectsPublication()
    {
        string input = _temp.File("input.txt");
        string target = _temp.File("out.txt");
        File.WriteAllText(input, "admitted");
        FileWritePrecondition precondition = FileWritePrecondition.Capture(input);

        CliException error = Assert.Throws<CliException>(() =>
            _writer.Write(target, overwrite: false, backupPath: null, precondition, temp =>
            {
                File.WriteAllText(temp, "derived-output");
                File.WriteAllText(input, "external-update");
            }));

        Assert.Equal(ErrorCodes.InputChanged, error.Code);
        Assert.Equal("external-update", File.ReadAllText(input));
        Assert.False(File.Exists(target));
    }

    [Fact]
    public void Write_InPlaceInputChangedAfterAdmission_DoesNotOverwriteExternalUpdate()
    {
        string input = _temp.File("input.txt");
        File.WriteAllText(input, "admitted");
        FileWritePrecondition precondition = FileWritePrecondition.Capture(input);
        File.WriteAllText(input, "external-update");

        CliException error = Assert.Throws<CliException>(() =>
            _writer.Write(input, overwrite: true, backupPath: null, precondition, temp =>
                File.WriteAllText(temp, "derived-output")));

        Assert.Equal(ErrorCodes.InputChanged, error.Code);
        Assert.Equal("external-update", File.ReadAllText(input));
    }

    [Fact]
    public void Write_OverwritePreservesPortableMetadata()
    {
        string target = _temp.File("out.txt");
        File.WriteAllText(target, "original");
        if (OperatingSystem.IsWindows())
        {
            File.SetAttributes(target, File.GetAttributes(target) | FileAttributes.Hidden);
        }
        else
        {
            File.SetUnixFileMode(
                target,
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        _writer.Write(target, overwrite: true, temp => File.WriteAllText(temp, "replacement"));

        if (OperatingSystem.IsWindows())
        {
            Assert.True(File.GetAttributes(target).HasFlag(FileAttributes.Hidden));
        }
        else
        {
            Assert.Equal(
                UnixFileMode.UserRead | UnixFileMode.UserWrite,
                File.GetUnixFileMode(target));
        }
    }
}
