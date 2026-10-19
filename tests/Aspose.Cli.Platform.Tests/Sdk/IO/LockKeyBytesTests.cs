using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Host.Updating;
using Aspose.Cli.Sdk.IO.Paths;
using Aspose.Cli.Sdk.IO.Publication;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Sdk.Tests.IO;

/// <summary>
/// Fixes the bytes of every key derived from a path: the publication directory lock files and
/// the publication journal mutex, which other Aspose CLI distributions share, the local service
/// marker lock, the license identity and the update status file name, which older versions of
/// this CLI share. Each expected value is computed here from the literal rule, never through
/// the code under test, so a change to how a key folds case, trims or hashes fails here.
/// </summary>
public sealed class LockKeyBytesTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    // The rule shared with other distributions: upper-case invariant on Windows, as spelled elsewhere.
    private static string Folded(string value) => OperatingSystem.IsWindows() ? value.ToUpperInvariant() : value;

    private static string Sha256Hex(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    /// <summary>Spellings of a path below the test directory: case variants, a trailing separator, non-ASCII and long names.</summary>
    public static TheoryData<string> Spellings => new()
    {
        "plain",
        "UPPER",
        "lower|case",
        "trailing|",
        "Ünïcödé-éßıσς",
        string.Join("|", Enumerable.Repeat("long-segment-" + new string('x', 40), 6)),
    };

    private string Spelled(string spelling, bool create)
    {
        string relative = spelling.Replace('|', Path.DirectorySeparatorChar);
        string path = _temp.File(relative);
        if (create) { Directory.CreateDirectory(path); }
        return spelling switch
        {
            "UPPER" => path.ToUpperInvariant(),
            "lower|case" => path.ToLowerInvariant(),
            _ => path,
        };
    }

    [Theory]
    [MemberData(nameof(Spellings))]
    public void PublicationDirectoryLease_HoldsTheFileNamedForItsFoldedTrimmedFullPath(string spelling)
    {
        string directory = Spelled(spelling, create: false);
        string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        string lockRoot = Path.Combine(UserStorage.PublicationLockRoot(), "publication-locks");
        string leaf = Path.Combine(lockRoot, Sha256Hex("directory:" + Folded(full)) + ".lock");
        string driveRoot = Path.Combine(lockRoot, Sha256Hex("directory:" + Folded(Path.GetPathRoot(full)!)) + ".lock");

        using (PublicationDirectoryLease.Acquire(directory))
        {
            Assert.True(File.Exists(leaf), $"No lock file {leaf} for '{directory}'.");
            Assert.True(File.Exists(driveRoot), $"No lock file {driveRoot} for the drive root of '{directory}'.");
            if (OperatingSystem.IsWindows())
            {
                Assert.True(IsHeld(leaf), "The lease does not hold the lock file of its directory.");
                Assert.True(IsHeld(driveRoot), "The lease does not hold the lock file of its drive root.");
            }
        }

        if (OperatingSystem.IsWindows()) { Assert.False(IsHeld(leaf), "The released lease still holds its lock file."); }
    }

    [Theory]
    [MemberData(nameof(Spellings))]
    [InlineData("drive-root")]
    public void PublicationJournalLock_OwnsTheMutexNamedForItsFoldedFullPath(string spelling)
    {
        string journal = spelling == "drive-root"
            ? Path.GetPathRoot(_temp.Path)!
            : Path.Combine(Spelled(spelling, create: false), "journal.json");
        if (spelling == "trailing|") { journal = Spelled(spelling, create: false); }
        // GetFullPath keeps a trailing separator, and so does the key.
        string name = "aspose-publication-journal-" + Sha256Hex(Folded(Path.GetFullPath(journal)));
        var options = new NamedWaitHandleOptions { CurrentUserOnly = true, CurrentSessionOnly = false };

        using (PublicationJournalLock? held = PublicationJournalLock.TryAcquire(journal))
        {
            Assert.NotNull(held);
            Assert.True(Mutex.TryOpenExisting(name, options, out Mutex? opened), $"No mutex {name} for '{journal}'.");
            opened!.Dispose();
        }

        Assert.False(Mutex.TryOpenExisting(name, options, out Mutex? left), "The released journal lock left its mutex.");
        left?.Dispose();
    }

    [Theory]
    [MemberData(nameof(Spellings))]
    public void LocalServiceMarkerFiles_LockTheFileNamedForTheirFoldedSortedFullPaths(string spelling)
    {
        string directory = Spelled(spelling, create: true);
        string marker = Path.Combine(directory, "marker.json");
        string secrets = Path.Combine(directory, "Secrets.json");
        string key = string.Join("\n", new[] { Folded(Path.GetFullPath(marker)), Folded(Path.GetFullPath(secrets)) }
            .Order(StringComparer.Ordinal));
        string lockFile = Path.Combine(UserStorage.TemporaryRoot(), "services", "marker-files", "locks",
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)).AsSpan(0, 16)).ToLowerInvariant() + ".lock");

        new LocalServiceMarkerFiles<State, State>(marker, secrets, TypeInfo(), TypeInfo()).Write(new State(1), new State(1));

        Assert.True(File.Exists(lockFile), $"No lock file {lockFile} for '{directory}'.");
    }

    [Theory]
    [MemberData(nameof(Spellings))]
    [InlineData("drive-root")]
    public void LicenseGate_IdentityHashesTheSourceTheFoldedPathAndTheContent(string spelling)
    {
        string path = spelling == "drive-root" ? Path.GetPathRoot(_temp.Path)! : Path.Combine(Spelled(spelling, create: false), "License.lic");
        if (spelling == "trailing|") { path = Spelled(spelling, create: false); }
        byte[] content = Encoding.UTF8.GetBytes("license-bytes");
        string contentPath = _temp.File("content.lic");
        File.WriteAllBytes(contentPath, content);
        var gate = new LicenseGate(new LicenseResolution(LicenseSourceKind.Flag, path) { ContentPath = contentPath },
            static _ => null, static _ => { });

        byte[] hashed = [.. Encoding.UTF8.GetBytes("flag\0" + Folded(path) + "\0"), .. content];

        Assert.Equal(LicenseState.Licensed, gate.EnsureApplied());
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(hashed)), gate.Identity);
    }

    [Theory]
    [MemberData(nameof(Spellings))]
    [InlineData("drive-root")]
    public void UpdateStatus_NamesTheFileForTheUpperCasedTrimmedFullRootOnEveryPlatform(string spelling)
    {
        string root = spelling == "drive-root" ? Path.GetPathRoot(_temp.Path)! : Spelled(spelling, create: false);
        // Unlike the locks, the status file name folds case on every platform.
        string key = Sha256Hex(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)).ToUpperInvariant()).ToLowerInvariant()[..16];

        Assert.Equal(Path.Combine(UserStorage.TemporaryRoot(), "updates", "status-" + key + ".json"), UpdateStatus.PathFor(root));
    }

    // Pins the lock key rule of both platforms wherever the tests run: it folds only on Windows,
    // while the update status key folds on every platform.
    [Theory]
    [InlineData(@"c:\Users\Ünï\work\", false, @"c:\Users\Ünï\work\", @"C:\USERS\ÜNÏ\WORK\")]
    [InlineData(@"c:\Users\Ünï\work\", true, @"C:\USERS\ÜNÏ\WORK\", @"C:\USERS\ÜNÏ\WORK\")]
    [InlineData("/home/user/ßıσς", false, "/home/user/ßıσς", "/HOME/USER/ßıΣΣ")]
    [InlineData("/home/user/ßıσς", true, "/HOME/USER/ßıΣΣ", "/HOME/USER/ßıΣΣ")]
    public void LockKey_FoldsOnlyOnWindows_AndFoldedKeyFoldsEverywhere(string path, bool windows, string lockKey, string foldedKey)
    {
        Assert.Equal(lockKey, FilePaths.LockKey(path, windows));
        Assert.Equal(foldedKey, FilePaths.FoldedKey(path));
    }

    private static bool IsHeld(string path)
    {
        try
        {
            using var probe = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
    }

    private static JsonTypeInfo<State> TypeInfo() =>
        (JsonTypeInfo<State>)new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        }.GetTypeInfo(typeof(State));

    private sealed record State(int Revision);
}
