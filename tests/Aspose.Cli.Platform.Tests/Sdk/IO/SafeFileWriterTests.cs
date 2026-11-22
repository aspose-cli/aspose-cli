using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO.Files;
using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.IO.Publication;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.TestKit;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
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

        PublishedFile published = _writer.Write(target, overwrite: false, temp => File.WriteAllText(temp, "hello"));

        Assert.Equal(5, published.SizeBytes);
        Assert.Equal(target, published.Path);
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
        Assert.Empty(Directory.EnumerateFileSystemEntries(_temp.Path));
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
    public void OwnedTemporaryFile_RejectsIdentityChangesAfterTheProducedFileIsBound()
    {
        Requires.Windows();

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
        FileSystemLinks.CreateDirectoryLink(linked, actual);

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
        Requires.Windows();

        string missing = _temp.File("missing-target");
        string linked = _temp.File("dangling-link");
        FileSystemLinks.CreateDirectoryLink(linked, missing);

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
        Requires.Windows();

        string link = _temp.File("dangling-file-link.txt");
        FileSystemLinks.CreateFileSymbolicLink(link, _temp.File("missing-file.txt"));

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
        Assert.Contains("(the staged output could not be written)", error.Message, StringComparison.Ordinal);
        Assert.Null(error.Details!["reason"]);
        Assert.DoesNotContain(privateReason, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_CallbackReplaceOnSaveBindsTheProducedFile()
    {
        Requires.Windows();

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
            _writer.Write(target, overwrite: false, precondition, temp =>
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
            _writer.Write(input, overwrite: true, precondition, temp =>
                File.WriteAllText(temp, "derived-output")));

        Assert.Equal(ErrorCodes.InputChanged, error.Code);
        Assert.Equal("external-update", File.ReadAllText(input));
    }

    [Fact]
    public void Write_InPlaceInputChangedDuringProduction_ReportsTheInputChange()
    {
        string input = _temp.File("input.txt");
        File.WriteAllText(input, "admitted");
        FileWritePrecondition precondition = FileWritePrecondition.Capture(input);

        CliException error = Assert.Throws<CliException>(() =>
            _writer.Write(input, overwrite: true, precondition, temp =>
            {
                File.WriteAllText(temp, "derived-output");
                File.WriteAllText(input, "external-update");
            }));

        Assert.Equal(ErrorCodes.InputChanged, error.Code);
        Assert.Equal("external-update", File.ReadAllText(input));
    }

    [Fact]
    public void Write_InPlaceInputDeletedDuringProduction_ReportsTheMissingInput()
    {
        string input = _temp.File("input.txt");
        File.WriteAllText(input, "admitted");
        FileWritePrecondition precondition = FileWritePrecondition.Capture(input);

        CliException error = Assert.Throws<CliException>(() =>
            _writer.Write(input, overwrite: true, precondition, temp =>
            {
                File.WriteAllText(temp, "derived-output");
                File.Delete(input);
            }));

        Assert.Equal(ErrorCodes.FileNotFound, error.Code);
        Assert.False(File.Exists(input));
    }

    /// <summary>
    /// A target whose access rules deny the current user writing its attributes, and that is not
    /// read-only, is replaced, whether it is another file or, as in an in-place edit, the input
    /// itself, and its folder holds what it held before.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [SupportedOSPlatform("windows")]
    public void Write_TargetThatDeniesAttributeWrites_IsReplacedAndLeavesItsFolderAsItWas(bool inPlace)
    {
        Requires.Windows();

        string target = _temp.File("out.txt");
        File.WriteAllText(target, "original");
        var deny = new FileSystemAccessRule(
            WindowsIdentity.GetCurrent().User!, FileSystemRights.WriteAttributes, AccessControlType.Deny);
        FileSecurity denying = new FileInfo(target).GetAccessControl(AccessControlSections.Access);
        denying.AddAccessRule(deny);
        new FileInfo(target).SetAccessControl(denying);
        try
        {
            FileWritePrecondition? input = inPlace ? FileWritePrecondition.Capture(target) : null;

            _writer.Write(target, overwrite: true, input, temp => File.WriteAllText(temp, "replacement"));

            Assert.Equal("replacement", File.ReadAllText(target));
            Assert.Equal([target], Directory.EnumerateFileSystemEntries(_temp.Path));
        }
        finally
        {
            foreach (string file in Directory.EnumerateFiles(_temp.Path, "*", SearchOption.AllDirectories))
            {
                FileSecurity rules = new FileInfo(file).GetAccessControl(AccessControlSections.Access);
                rules.RemoveAccessRuleSpecific(deny);
                new FileInfo(file).SetAccessControl(rules);
            }
        }
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
