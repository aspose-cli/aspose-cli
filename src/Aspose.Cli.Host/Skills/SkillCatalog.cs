using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.Sdk;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Host.Skills;

/// <summary>
/// Catalog of all Agent Skills embedded in the executable: the platform Skill, which covers
/// what every product shares, followed by one Skill per product that ships one.
/// </summary>
internal sealed class SkillCatalog
{
    /// <summary>Name of the platform Skill the Host embeds.</summary>
    public const string PlatformSkillName = DistributionInfo.SkillPrefix + "platform";

    private readonly IReadOnlyList<BundledSkill> _packages;

    public SkillCatalog(Aspose.Cli.Sdk.Extensibility.ProductCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _packages = Create(catalog);
    }

    private static IReadOnlyList<BundledSkill> Create(
        Aspose.Cli.Sdk.Extensibility.ProductCatalog catalog)
    {
        Assembly host = typeof(SkillCatalog).Assembly;
        Aspose.Cli.Sdk.Extensibility.SkillFrontMatter platform =
            Aspose.Cli.Sdk.Extensibility.SkillFrontMatter.Read(host, PlatformSkillName)
            ?? throw new InvalidOperationException($"The Host ships no '{PlatformSkillName}' Skill.");
        var packages = new List<BundledSkill>
        {
            new(
                platform.Name,
                platform.Description,
                host,
                Array.AsReadOnly(host.GetManifestResourceNames()
                    .Where(static name => name.StartsWith($"skill/{PlatformSkillName}/", StringComparison.Ordinal))
                    .ToArray())),
        };
        foreach (Aspose.Cli.Sdk.Extensibility.ProductPackageResources resources
            in catalog.Resources.Products
                .Where(static resources =>
                    resources.SkillName is not null
                    && resources.SkillDescription is not null))
        {
            packages.Add(new(
                resources.SkillName!,
                resources.SkillDescription!,
                resources.ResourceAssembly,
                resources.ResourceNames));
        }
        return packages;
    }

    public IReadOnlyList<BundledSkill> All => _packages;

    public BundledSkill Get(string name) =>
        _packages.FirstOrDefault(package => package.Name == name)
        ?? throw new ArgumentException($"Unknown bundled skill '{name}'.", nameof(name));
}

/// <summary>One embedded Agent Skill package.</summary>
internal sealed record BundledSkill(
    string Name,
    string Description,
    Assembly ResourceAssembly,
    IReadOnlyList<string> ResourceNames)
{
    private const string ManifestName = ".aspose-skill-manifest.json";
    private const string ManifestProductId = DistributionInfo.SkillManifestProductId;
    private static readonly TimeSpan InstallLockTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Materialises the package into a staging tree beside the target, so the
    /// publishing rename stays on one volume. The tree is scratch, not a published
    /// output: <see cref="InstallInto"/> publishes it with a single directory rename. Writing it through the output publication
    /// transaction would hand it to a supervising parent instead of to disk, leaving
    /// the rename nothing to publish.
    /// </summary>
    private int ExtractTo(
        ResourceBudgetLedger resourceBudgets,
        string directory)
    {
        ArgumentNullException.ThrowIfNull(resourceBudgets);
        ArgumentException.ThrowIfNullOrEmpty(directory);
        string prefix = $"skill/{Name}/";
        string root = Path.GetFullPath(directory);
        Directory.CreateDirectory(root);
        var files = new List<SkillFileEntry>();
        using IncrementalHash contentHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (string resource in ResourceNames.Order(StringComparer.Ordinal))
        {
            string normalized = resource.Replace('\\', '/');
            if (!normalized.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            string relative = NormalizeManifestPath(normalized[prefix.Length..]);
            using Stream source = ResourceAssembly.GetManifestResourceStream(resource)!;
            using var content = new MemoryStream();
            source.CopyTo(content);
            ReadOnlySpan<byte> bytes = content.GetBuffer().AsSpan(0, checked((int)content.Length));
            contentHash.AppendData(Encoding.UTF8.GetBytes(relative));
            contentHash.AppendData([0]);
            contentHash.AppendData(bytes);
            files.Add(new(
                relative,
                content.Length,
                Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()));
            Stage(resourceBudgets, root, relative, bytes);
        }

        byte[] manifest = InstallManifest(
            files,
            Convert.ToHexString(contentHash.GetHashAndReset()).ToLowerInvariant());
        Stage(resourceBudgets, root, ManifestName, manifest);
        return files.Count + 1;
    }

    /// <summary>
    /// Writes one staged file, consuming the invocation's output budget so the
    /// extraction stays bounded, and refusing to follow a link out of the tree.
    /// </summary>
    private static void Stage(
        ResourceBudgetLedger resourceBudgets,
        string root,
        string relative,
        ReadOnlySpan<byte> content)
    {
        resourceBudgets.Consume(
            ResourceBudgetKinds.OutputBytes, content.Length, "bytes", "skill-stage");
        string path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        EnsureNoReparsePoint(path);
        using var file = new FileStream(
            path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        file.Write(content);
    }

    private byte[] InstallManifest(
        IReadOnlyList<SkillFileEntry> files,
        string contentSha256)
    {
        string executable = Environment.ProcessPath
            ?? throw new InvalidOperationException(
                "The current executable path is unavailable for Skill provenance.");
        string executableSha256;
        using (FileStream stream = File.OpenRead(executable))
        {
            executableSha256 = Convert.ToHexString(SHA256.HashData(stream))
                .ToLowerInvariant();
        }
        string version = Assembly.GetEntryAssembly()?
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion
            ?? Assembly.GetEntryAssembly()?.GetName().Version?.ToString()
            ?? "unknown";
        using var output = new MemoryStream();
        using (var json = new Utf8JsonWriter(output, new JsonWriterOptions
        {
            Indented = true,
        }))
        {
            json.WriteStartObject();
            json.WriteNumber("schemaVersion", 2);
            json.WriteString("productId", ManifestProductId);
            json.WriteString("skill", Name);
            json.WriteString("cliVersion", version);
            json.WriteString("executable", Path.GetFileName(executable));
            json.WriteString("executableSha256", executableSha256);
            json.WriteString("contentSha256", contentSha256);
            json.WriteStartArray("files");
            foreach (SkillFileEntry file in files.OrderBy(static file => file.Path, StringComparer.Ordinal))
            {
                json.WriteStartObject();
                json.WriteString("path", file.Path);
                json.WriteNumber("size", file.Size);
                json.WriteString("sha256", file.Sha256);
                json.WriteEndObject();
            }
            json.WriteEndArray();
            json.WriteEndObject();
        }
        output.WriteByte((byte)'\n');
        return output.ToArray();
    }

    public SkillInstallResult InstallInto(
        ResourceBudgetLedger resourceBudgets,
        string directory)
    {
        ArgumentNullException.ThrowIfNull(resourceBudgets);
        ArgumentException.ThrowIfNullOrEmpty(directory);
        string target = Path.GetFullPath(directory);
        string parent = Path.GetDirectoryName(target)
            ?? throw ManagedSkillConflict(target, "the target has no parent directory");
        Directory.CreateDirectory(parent);
        EnsureNoReparsePoint(parent);

        // One resource-keyed interprocess lock per target. Its file lives in the
        // private service lock directory and is never deleted, so two installers
        // can never hold locks on different files for the same target.
        LocalServiceOperationLock installLock;
        try
        {
            installLock = LocalServiceOperationLock.Acquire("skill-install", LockKey(target), InstallLockTimeout);
        }
        catch (TimeoutException exception)
        {
            throw ManagedSkillConflict(
                target,
                "another Skill installation is already using this target",
                exception);
        }

        string transaction = Guid.NewGuid().ToString("N");
        string stage = Path.Combine(parent, $".aspose-skill-stage-{transaction}");
        string backup = Path.Combine(parent, $".aspose-skill-backup-{transaction}");
        int files;
        bool oldMoved = false;
        bool newPublished = false;
        try
        {
            ValidateExistingTarget(target);
            files = ExtractTo(resourceBudgets, stage);
            ValidateManagedTarget(stage);

            if (Directory.Exists(target))
            {
                Directory.Move(target, backup);
                oldMoved = true;
            }
            else if (File.Exists(target))
            {
                throw ManagedSkillConflict(target, "the target is an existing file");
            }

            Directory.Move(stage, target);
            newPublished = true;
            ValidateManagedTarget(target);

            if (oldMoved)
            {
                ValidateExistingTarget(backup);
                Directory.Delete(backup, recursive: true);
                oldMoved = false;
            }
        }
        catch
        {
            if (newPublished && Directory.Exists(target))
            {
                try
                {
                    ValidateManagedTarget(target);
                    Directory.Move(target, stage);
                    newPublished = false;
                }
                catch
                {
                    // Preserve both trees for manual recovery if the new tree changed externally.
                }
            }

            if (oldMoved && !Directory.Exists(target) && Directory.Exists(backup))
            {
                ValidateExistingTarget(backup);
                Directory.Move(backup, target);
                oldMoved = false;
            }
            throw;
        }
        finally
        {
            installLock.Dispose();
            if (Directory.Exists(stage))
            {
                try
                {
                    ValidateManagedTarget(stage);
                    Directory.Delete(stage, recursive: true);
                }
                catch
                {
                    // A changed recovery tree is intentionally retained.
                }
            }
        }

        return new SkillInstallResult
        {
            Skill = Name,
            Target = target,
            Files = files,
        };
    }

    private void ValidateExistingTarget(string target)
    {
        if (!Directory.Exists(target))
        {
            if (File.Exists(target))
            {
                throw ManagedSkillConflict(target, "the target is an existing file");
            }
            return;
        }

        EnsureNoReparsePoint(target);
        if (!Directory.EnumerateFileSystemEntries(target).Any())
        {
            return;
        }

        string manifestPath = Path.Combine(target, ManifestName);
        if (!File.Exists(manifestPath))
        {
            throw ManagedSkillConflict(
                target,
                $"the non-empty directory has no {ManifestName} ownership manifest");
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(
                File.ReadAllBytes(manifestPath),
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 32,
                });
            EnsureUniqueProperties(document.RootElement);
            int schemaVersion = document.RootElement.GetProperty("schemaVersion").GetInt32();

            if (schemaVersion == 2)
            {
                RequireProperties(
                    document.RootElement,
                    "schemaVersion",
                    "productId",
                    "skill",
                    "cliVersion",
                    "executable",
                    "executableSha256",
                    "contentSha256",
                    "files");
                ValidateManagedTarget(target, document.RootElement);
                return;
            }
            throw new InvalidDataException($"unsupported manifest schemaVersion {schemaVersion}");
        }
        catch (CliException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException
            or InvalidDataException
            or InvalidOperationException
            or KeyNotFoundException)
        {
            throw ManagedSkillConflict(target, $"the ownership manifest is invalid: {exception.Message}", exception);
        }
    }



    private void ValidateManagedTarget(string target)
    {
        try
        {
            string manifestPath = Path.Combine(target, ManifestName);
            using JsonDocument document = JsonDocument.Parse(
                File.ReadAllBytes(manifestPath),
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 32,
                });
            EnsureUniqueProperties(document.RootElement);
            RequireProperties(
                document.RootElement,
                "schemaVersion",
                "productId",
                "skill",
                "cliVersion",
                "executable",
                "executableSha256",
                "contentSha256",
                "files");
            ValidateManagedTarget(target, document.RootElement);
        }
        catch (CliException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or JsonException
            or InvalidDataException
            or InvalidOperationException
            or KeyNotFoundException)
        {
            throw ManagedSkillConflict(
                target, $"the ownership manifest is invalid: {exception.Message}", exception);
        }
    }

    private void ValidateManagedTarget(string target, JsonElement manifest)
    {
        if (manifest.GetProperty("schemaVersion").GetInt32() != 2)
        {
            throw new InvalidDataException("the Skill manifest is not schemaVersion 2");
        }
        RequireString(manifest, "productId", ManifestProductId);
        RequireString(manifest, "skill", Name);
        string expectedContentSha256 = RequireSha256(manifest, "contentSha256");
        SkillTree tree = ReadTree(target);
        if (!tree.Files.Remove(ManifestName))
        {
            throw new InvalidDataException("the ownership manifest is missing from the inventory");
        }

        var expected = new Dictionary<string, SkillFileEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (JsonElement item in manifest.GetProperty("files").EnumerateArray())
        {
            RequireProperties(item, "path", "size", "sha256");
            string path = NormalizeManifestPath(item.GetProperty("path").GetString());
            long size = item.GetProperty("size").GetInt64();
            string sha256 = RequireSha256(item, "sha256");
            if (size < 0 || !expected.TryAdd(path, new(path, size, sha256)))
            {
                throw new InvalidDataException($"invalid or duplicate Skill file entry '{path}'");
            }
        }

        if (!expected.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase)
            .SetEquals(tree.Files.Keys))
        {
            throw ManagedSkillConflict(
                target,
                "the managed Skill contains an unknown, missing, or case-conflicting file");
        }

        HashSet<string> expectedDirectories = ExpectedDirectories(expected.Keys);
        if (!expectedDirectories.SetEquals(tree.Directories))
        {
            throw ManagedSkillConflict(
                target,
                "the managed Skill contains an unknown or missing directory");
        }

        foreach (SkillFileEntry file in expected.Values)
        {
            FileInfo actual = tree.Files[file.Path];
            if (actual.Length != file.Size
                || !string.Equals(FileSha256(actual.FullName), file.Sha256, StringComparison.Ordinal))
            {
                throw ManagedSkillConflict(target, $"managed file '{file.Path}' was modified");
            }
        }
        if (!string.Equals(
                expectedContentSha256,
                AggregateHash(target, expected.Keys),
                StringComparison.Ordinal))
        {
            throw ManagedSkillConflict(target, "the aggregate managed content hash does not match");
        }
    }

    private static SkillTree ReadTree(string target)
    {
        EnsureNoReparsePoint(target);
        var files = new Dictionary<string, FileInfo>(StringComparer.OrdinalIgnoreCase);
        var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Queue<DirectoryInfo>();
        pending.Enqueue(new DirectoryInfo(target));
        while (pending.Count != 0)
        {
            DirectoryInfo current = pending.Dequeue();
            foreach (FileSystemInfo item in current.EnumerateFileSystemInfos())
            {
                if ((item.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw ManagedSkillConflict(target, $"reparse point '{item.FullName}' is not allowed");
                }
                string relative = NormalizeManifestPath(Path.GetRelativePath(target, item.FullName));
                if (item is DirectoryInfo directory)
                {
                    if (!directories.Add(relative))
                    {
                        throw new InvalidDataException($"duplicate directory '{relative}'");
                    }
                    pending.Enqueue(directory);
                }
                else if (item is FileInfo file)
                {
                    if (!files.TryAdd(relative, file))
                    {
                        throw new InvalidDataException($"duplicate file '{relative}'");
                    }
                }
            }
        }
        return new(files, directories);
    }

    private static HashSet<string> ExpectedDirectories(IEnumerable<string> files)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string file in files)
        {
            string? directory = Path.GetDirectoryName(file.Replace('/', Path.DirectorySeparatorChar));
            while (!string.IsNullOrEmpty(directory))
            {
                result.Add(directory.Replace(Path.DirectorySeparatorChar, '/'));
                directory = Path.GetDirectoryName(directory);
            }
        }
        return result;
    }

    private static string AggregateHash(string target, IEnumerable<string> files)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (string relative in files.Order(StringComparer.Ordinal))
        {
            byte[] relativeBytes = Encoding.UTF8.GetBytes(relative);
            hash.AppendData(relativeBytes);
            hash.AppendData([0]);
            using FileStream input = File.OpenRead(Path.Combine(
                target,
                relative.Replace('/', Path.DirectorySeparatorChar)));
            byte[] buffer = new byte[64 * 1024];
            int read;
            while ((read = input.Read(buffer)) != 0)
            {
                hash.AppendData(buffer.AsSpan(0, read));
            }
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static string NormalizeManifestPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)
            || Path.IsPathRooted(path)
            || path.StartsWith("\\\\", StringComparison.Ordinal)
            || path.Contains(':', StringComparison.Ordinal))
        {
            throw new InvalidDataException($"unsafe Skill path '{path}'");
        }
        string normalized = path.Replace('\\', '/');
        string[] segments = normalized.Split('/');
        if (segments.Any(static segment => segment.Length == 0
            || segment is "." or ".."
            || segment.EndsWith(' ')
            || segment.EndsWith('.')))
        {
            throw new InvalidDataException($"unsafe Skill path '{path}'");
        }
        return string.Join('/', segments);
    }

    private static void EnsureUniqueProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new InvalidDataException($"duplicate JSON property '{property.Name}'");
                }
                EnsureUniqueProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                EnsureUniqueProperties(item);
            }
        }
    }

    private static void RequireString(JsonElement element, string property, string expected)
    {
        string? actual = element.GetProperty(property).GetString();
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"'{property}' must be '{expected}'");
        }
    }

    private static void RequireProperties(JsonElement element, params string[] expected)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("a JSON object was required");
        }
        HashSet<string> actual = element.EnumerateObject()
            .Select(static property => property.Name)
            .ToHashSet(StringComparer.Ordinal);
        if (!actual.SetEquals(expected))
        {
            string unknown = string.Join(", ", actual.Except(expected, StringComparer.Ordinal));
            string missing = string.Join(", ", expected.Except(actual, StringComparer.Ordinal));
            throw new InvalidDataException(
                $"manifest properties do not match (unknown: [{unknown}], missing: [{missing}])");
        }
    }

    private static string RequireSha256(JsonElement element, string property)
    {
        string? value = element.GetProperty(property).GetString();
        if (value is null || value.Length != 64 || value.Any(static character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidDataException($"'{property}' is not a SHA-256 value");
        }
        return value.ToLowerInvariant();
    }

    private static string FileSha256(string path)
    {
        using FileStream input = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant();
    }

    private static string LockKey(string target) =>
        OperatingSystem.IsWindows() ? target.ToUpperInvariant() : target;

    private static void EnsureNoReparsePoint(string path)
    {
        string fullPath = Path.GetFullPath(path);
        if (OperatingSystem.IsWindows()
            && (fullPath.StartsWith("\\\\", StringComparison.Ordinal)
                || fullPath.StartsWith("\\\\?\\", StringComparison.Ordinal)
                || fullPath.StartsWith("\\\\.\\", StringComparison.Ordinal)))
        {
            throw ManagedSkillConflict(fullPath, "remote and device paths are not allowed");
        }

        for (string? cursor = fullPath;
             !string.IsNullOrEmpty(cursor);
             cursor = Path.GetDirectoryName(cursor))
        {
            if ((File.Exists(cursor) || Directory.Exists(cursor))
                && (File.GetAttributes(cursor) & FileAttributes.ReparsePoint) != 0)
            {
                throw ManagedSkillConflict(
                    fullPath,
                    $"reparse-point ancestor '{cursor}' is not allowed");
            }

            string? parent = Path.GetDirectoryName(cursor);
            if (string.Equals(parent, cursor, StringComparison.Ordinal))
            {
                break;
            }
        }
    }

    private static CliException ManagedSkillConflict(
        string target,
        string reason,
        Exception? inner = null) => new(
            ErrorCodes.OutputExists,
            $"The Skill target cannot be updated safely: {target} ({reason}).",
            hint: $"Preserve custom files and choose a different --target, or restore the managed contents before retrying.",
            details: new JsonObject
            {
                ["path"] = target,
                ["reason"] = reason,
            },
            innerException: inner);

    private sealed record SkillFileEntry(string Path, long Size, string Sha256);

    private sealed record SkillTree(
        Dictionary<string, FileInfo> Files,
        HashSet<string> Directories);
}
