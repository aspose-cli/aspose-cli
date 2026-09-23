using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.Architecture.Tests;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

public sealed class ReleaseNoticesTests
{
    [Fact]
    public void DeployedPackagesAndRuntimePreserveLegalBytesAndRejectArchiveTampering()
    {
        using var directory = new TempDirectory();
        string cache = directory.File("packages");
        var dependencies = new JsonObject();
        var locked = new JsonObject();
        var runtimePacks = new JsonArray();
        byte[] original = Encoding.UTF8.GetBytes("Original copyright\r\n  preserve spaces\t\r\n");
        string engine = Package("Fixture.Engine", "1.2.3", runtime: false, original);
        Package("Microsoft.NETCore.App.Runtime.win-x64", "10.0.10", runtime: true, original);
        Package("Fixture.Expression", "1.0.0", runtime: false, license: null);
        locked["Fixture.NotDeployed"] = new JsonObject { ["type"] = "Transitive", ["resolved"] = "1.0.0", ["contentHash"] = "not deployed" };
        string deps = directory.File("app.deps.json");
        string assets = directory.File("project.assets.json");
        string packagesLock = directory.File("packages.lock.json");
        string runtimeLock = directory.File("runtime-packs.lock.json");
        File.WriteAllText(deps, new JsonObject { ["libraries"] = dependencies }.ToJsonString());
        File.WriteAllText(assets, new JsonObject
        {
            ["packageFolders"] = new JsonObject { [cache] = new JsonObject() },
        }.ToJsonString());
        File.WriteAllText(packagesLock, new JsonObject
        {
            ["version"] = 1,
            ["dependencies"] = new JsonObject { ["net10.0"] = locked, ["net10.0/win-x64"] = new JsonObject() },
        }.ToJsonString());
        File.WriteAllText(runtimeLock, new JsonObject { ["schemaVersion"] = 1, ["runtimePacks"] = runtimePacks }.ToJsonString());
        string output = directory.File("output");
        Directory.CreateDirectory(output);
        CliResult result = Collect(output);
        Assert.True(result.ExitCode == 0, result.StdOut + result.StdErr);
        Assert.Equal(File.ReadAllBytes(Path.Combine(RepositoryPaths.Root, "LICENSE")), File.ReadAllBytes(Path.Combine(output, "LICENSE")));
        Assert.Equal(original, File.ReadAllBytes(Path.Combine(output, "notices/fixture.engine/1.2.3/License/LICENSE.txt")));
        Assert.Equal(original, File.ReadAllBytes(Path.Combine(output, "notices/microsoft.netcore.app.runtime.win-x64/10.0.10/THIRD-PARTY-NOTICES.txt")));
        Assert.Equal("Fixture author\n\n" + File.ReadAllText(Path.Combine(RepositoryPaths.Root, "eng/notices/MIT.txt")),
            File.ReadAllText(Path.Combine(output, "notices/fixture.expression/1.0.0/LICENSE.txt")));
        using JsonDocument index = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "notices/index.json")));
        JsonElement packages = index.RootElement.GetProperty("packages");
        Assert.Equal(3, packages.GetArrayLength());
        foreach (JsonElement package in packages.EnumerateArray())
        {
            foreach (JsonElement file in package.GetProperty("files").EnumerateArray())
            {
                string path = Path.Combine(output, file.GetProperty("path").GetString()!);
                Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant(),
                    file.GetProperty("sha256").GetString());
            }
        }
        Assert.False(Directory.Exists(Path.Combine(output, "notices/fixture.notdeployed")));
        byte[] pristine = File.ReadAllBytes(engine);
        File.AppendAllText(engine, "tampered");
        string refused = directory.File("refused");
        Directory.CreateDirectory(refused);
        CliResult failed = Collect(refused);
        Assert.NotEqual(0, failed.ExitCode);
        Assert.Contains("package hash differs from packages.lock.json", Flat(failed), StringComparison.OrdinalIgnoreCase);

        File.WriteAllBytes(engine, pristine);
        // A deployed runtime pack must be pinned by the runtime pack lock, not only restored.
        File.WriteAllText(runtimeLock, new JsonObject { ["schemaVersion"] = 1, ["runtimePacks"] = new JsonArray() }.ToJsonString());
        string unpinned = directory.File("unpinned");
        Directory.CreateDirectory(unpinned);
        CliResult unpinnedResult = Collect(unpinned);
        Assert.NotEqual(0, unpinnedResult.ExitCode);
        Assert.Contains("is not pinned by eng/runtime-packs.lock.json", Flat(unpinnedResult), StringComparison.Ordinal);

        string Package(string id, string version, bool runtime, byte[]? license)
        {
            string identity = id + "/" + version;
            string root = Path.Combine(cache, identity.ToLowerInvariant());
            Directory.CreateDirectory(root);
            string archive = Path.Combine(root, id.ToLowerInvariant() + "." + version + ".nupkg");
            using (ZipArchive zip = ZipFile.Open(archive, ZipArchiveMode.Create))
            {
                Write(id + ".nuspec", Encoding.UTF8.GetBytes($"<package><metadata><id>{id}</id><version>{version}</version><license type=\"expression\">MIT</license><copyright>Fixture author</copyright></metadata></package>"));
                if (license is not null) { Write("License/LICENSE.txt", license); }
                Write("THIRD-PARTY-NOTICES.txt", original);
                void Write(string name, byte[] value)
                {
                    using Stream stream = zip.CreateEntry(name).Open();
                    stream.Write(value);
                }
            }
            string hash = Convert.ToBase64String(SHA512.HashData(File.ReadAllBytes(archive)));
            // No cache sidecar files: only the archive bytes and the lock files count.
            dependencies[(runtime ? "runtimepack." : "") + identity] = new JsonObject { ["type"] = runtime ? "runtimepack" : "package" };
            // An unsigned archive's NuGet content hash is the SHA-512 of the whole file.
            if (runtime) { runtimePacks.Add(new JsonObject { ["id"] = id, ["version"] = version, ["contentHash"] = hash }); }
            else { locked[id] = new JsonObject { ["type"] = "Direct", ["resolved"] = version, ["contentHash"] = hash }; }
            return archive;
        }

        CliResult Collect(string target)
        {
            string common = Path.Combine(RepositoryPaths.Root, "scripts/release-common.ps1");
            string notices = Path.Combine(RepositoryPaths.Root, "scripts/release-notices.ps1");
            // Print a failure plainly: redirected pwsh serializes its error stream as CLIXML.
            string script = $"$ErrorActionPreference='Stop'; try {{ . {Literal(common)}; . {Literal(notices)}; "
                + $"Write-ReleaseNotices -RepositoryRoot {Literal(RepositoryPaths.Root)} -OutputRoot {Literal(target)} -DependenciesPath {Literal(deps)} -AssetsPath {Literal(assets)} "
                + $"-PackagesLockPath {Literal(packagesLock)} -RuntimePacksLockPath {Literal(runtimeLock)} }} "
                + "catch { [Console]::Out.WriteLine($_.Exception.Message); exit 1 }";
            string shell = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator)
                .Where(static path => !string.IsNullOrWhiteSpace(path))
                .Select(static path => Path.GetFullPath(Path.Combine(path.Trim('"'), OperatingSystem.IsWindows() ? "pwsh.exe" : "pwsh")))
                .First(File.Exists);
            return new CliProcess(shell, CliEnvironment.Evaluation(directory.File("config")), TimeSpan.FromSeconds(30))
                .Run(directory.Path, args: ["-NoLogo", "-NoProfile", "-NonInteractive", "-EncodedCommand", Convert.ToBase64String(Encoding.Unicode.GetBytes(script))]);
        }
    }

    private static string Flat(CliResult result) =>
        (result.StdOut + result.StdErr).Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n", string.Empty, StringComparison.Ordinal);

    private static string Literal(string value) => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
}
