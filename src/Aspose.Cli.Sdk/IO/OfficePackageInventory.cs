using System.Buffers;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Sdk.IO;

/// <summary>Bounded fingerprints of the decompressed parts in one Office package.</summary>
public sealed record OfficePackageInventory(IReadOnlyList<OfficePackagePart> Parts)
{
    /// <summary>Normalized OPC Default and Override declarations.</summary>
    public IReadOnlyList<OfficePackageContentTypeDeclaration> ContentTypeDeclarations { get; init; } = [];

    /// <summary>Bounded OPC relationships with normalized internal targets.</summary>
    public IReadOnlyList<OfficePackageRelationship> Relationships { get; init; } = [];

    /// <summary>Reads and hashes every file part without extracting it.</summary>
    public static OfficePackageInventory Capture(
        string path,
        int maximumParts,
        long maximumDecompressedBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumParts, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumDecompressedBytes, 1);

        var parts = new List<OfficePackagePart>();
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.Ordinal);
        long totalBytes = 0;
        using var archive = new ZipArchive(File.OpenRead(path), ZipArchiveMode.Read);
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            string partPath = ValidatePath(entry.FullName);
            if (names.TryGetValue(partPath, out string? existing))
            {
                string conflict = string.Equals(existing, partPath, StringComparison.Ordinal)
                    ? "duplicated"
                    : $"case-conflicts with '{existing}'";
                throw new InvalidDataException(
                    $"Package part '{partPath}' is {conflict}.");
            }
            names.Add(partPath, partPath);

            if (entry.FullName.EndsWith("/", StringComparison.Ordinal))
            {
                continue;
            }
            entries.Add(partPath, entry);
            if (parts.Count == maximumParts)
            {
                throw new InvalidDataException(
                    $"Package exceeds the {maximumParts} part budget.");
            }
            if (entry.Length < 0
                || entry.Length > maximumDecompressedBytes - totalBytes)
            {
                throw new InvalidDataException(
                    $"Package exceeds the {maximumDecompressedBytes} decompressed-byte budget.");
            }

            (long size, string sha256) = Hash(entry, maximumDecompressedBytes - totalBytes);
            if (size != entry.Length)
            {
                throw new InvalidDataException(
                    $"Package part '{partPath}' length does not match its ZIP metadata.");
            }
            totalBytes += size;
            parts.Add(new OfficePackagePart(partPath, size, sha256));
        }

        OfficePackageOpcMetadata metadata = OfficePackageOpcReader.Read(entries);
        return new OfficePackageInventory(
            parts
                .Select(part => part with
                {
                    ContentType = metadata.ContentTypes.GetValueOrDefault(part.Path),
                })
                .OrderBy(static part => part.Path, StringComparer.Ordinal)
                .ToArray())
        {
            ContentTypeDeclarations = metadata.ContentTypeDeclarations,
            Relationships = metadata.Relationships,
        };
    }

    /// <summary>Compares decompressed part bytes, ignoring ZIP container metadata.</summary>
    public PackageMutationReceipt CompareTo(OfficePackageInventory updated)
    {
        ArgumentNullException.ThrowIfNull(updated);
        IReadOnlyDictionary<string, OfficePackagePart> before = Parts.ToDictionary(
            static part => part.Path,
            StringComparer.Ordinal);
        IReadOnlyDictionary<string, OfficePackagePart> after = updated.Parts.ToDictionary(
            static part => part.Path,
            StringComparer.Ordinal);
        var changes = new List<PackagePartChange>();
        int preserved = 0;

        foreach (string path in before.Keys.Union(after.Keys, StringComparer.Ordinal)
                     .Order(StringComparer.Ordinal))
        {
            bool hadPart = before.TryGetValue(path, out OfficePackagePart? oldPart);
            bool hasPart = after.TryGetValue(path, out OfficePackagePart? newPart);
            if (!hadPart)
            {
                changes.Add(new(path, "added"));
            }
            else if (!hasPart)
            {
                changes.Add(new(path, "removed"));
            }
            else if (oldPart!.SizeBytes != newPart!.SizeBytes
                     || !string.Equals(oldPart.Sha256, newPart.Sha256, StringComparison.Ordinal))
            {
                changes.Add(new(path, "modified"));
            }
            else
            {
                preserved++;
            }
        }

        return new PackageMutationReceipt(changes, preserved);
    }

    /// <summary>Rejects staged package changes outside a product-proven closure.</summary>
    public PackageMutationReceipt VerifyChanges(
        OfficePackageInventory updated,
        Func<PackagePartChange, bool> isAllowed)
    {
        ArgumentNullException.ThrowIfNull(isAllowed);
        PackageMutationReceipt receipt = CompareTo(updated);
        VerifyContentTypeChanges(updated, receipt);
        string[] remappedContentTypes = Parts
            .Join(
                updated.Parts,
                static part => part.Path,
                static part => part.Path,
                static (before, after) => (before, after),
                StringComparer.Ordinal)
            .Where(static pair => !string.Equals(
                pair.before.ContentType,
                pair.after.ContentType,
                StringComparison.Ordinal))
            .Select(static pair =>
                $"content-type:{pair.before.Path}:"
                + $"{pair.before.ContentType ?? "<none>"}->{pair.after.ContentType ?? "<none>"}")
            .ToArray();
        if (remappedContentTypes.Length > 0)
        {
            throw UnexpectedMutation(remappedContentTypes);
        }
        PackagePartChange[] unexpected = receipt.ChangedParts
            .Where(change => !isAllowed(change))
            .ToArray();
        if (unexpected.Length == 0)
        {
            return receipt;
        }

        throw UnexpectedMutation(unexpected.Select(static change => $"{change.Change}:{change.Path}"));
    }

    private void VerifyContentTypeChanges(
        OfficePackageInventory updated,
        PackageMutationReceipt receipt)
    {
        IReadOnlyDictionary<(string Scope, string Name), string> before = ContentTypeDeclarations
            .ToDictionary(static item => (item.Scope, item.Name), static item => item.ContentType);
        IReadOnlyDictionary<(string Scope, string Name), string> after = updated.ContentTypeDeclarations
            .ToDictionary(static item => (item.Scope, item.Name), static item => item.ContentType);
        var unexpected = new List<string>();
        foreach ((string scope, string name) in before.Keys.Union(after.Keys))
        {
            bool had = before.TryGetValue((scope, name), out string? oldType);
            bool has = after.TryGetValue((scope, name), out string? newType);
            if (had && has && string.Equals(oldType, newType, StringComparison.Ordinal))
            {
                continue;
            }

            string change = !had ? "added" : !has ? "removed" : "modified";
            bool allowed = change != "modified" && scope switch
            {
                "override" => receipt.ChangedParts.Any(part =>
                    part.Path == name && part.Change == change),
                "default" => receipt.ChangedParts.Any(part =>
                    part.Change == change
                    && string.Equals(
                        Path.GetExtension(part.Path).TrimStart('.'),
                        name,
                        StringComparison.OrdinalIgnoreCase)),
                _ => false,
            };
            if (!allowed)
            {
                unexpected.Add(
                    $"content-type-{change}:{scope}:{name}:{oldType ?? "<none>"}->{newType ?? "<none>"}");
            }
        }
        if (unexpected.Count > 0)
        {
            throw UnexpectedMutation(unexpected);
        }
    }

    /// <summary>Rejects semantic OPC relationship changes outside a product closure.</summary>
    public void VerifyRelationshipChanges(
        OfficePackageInventory updated,
        Func<OfficePackageRelationship, string, bool> isAllowed)
    {
        ArgumentNullException.ThrowIfNull(updated);
        ArgumentNullException.ThrowIfNull(isAllowed);
        IReadOnlyDictionary<RelationshipIdentity, int> before = RelationshipCounts(this);
        IReadOnlyDictionary<RelationshipIdentity, int> after = RelationshipCounts(updated);
        var unexpected = new List<string>();
        foreach (RelationshipIdentity identity in before.Keys.Union(after.Keys))
        {
            int beforeCount = before.GetValueOrDefault(identity);
            int afterCount = after.GetValueOrDefault(identity);
            if (beforeCount == afterCount)
            {
                continue;
            }

            string change = afterCount > beforeCount ? "added" : "removed";
            var relationship = new OfficePackageRelationship(
                identity.Source,
                string.Empty,
                identity.Type,
                identity.Target,
                identity.External ? null : identity.Target,
                identity.External);
            if (!isAllowed(relationship, change))
            {
                unexpected.Add(
                    $"relationship-{change}:{identity.Source}->{identity.Target} ({identity.Type})");
            }
        }

        if (unexpected.Count > 0)
        {
            throw UnexpectedMutation(unexpected);
        }
    }

    /// <summary>Returns the concrete owned OPC closure reachable through selected relationship types.</summary>
    public IReadOnlySet<string> RelationshipClosure(
        IEnumerable<string> roots,
        Func<OfficePackageRelationship, bool> include)
    {
        ArgumentNullException.ThrowIfNull(roots);
        ArgumentNullException.ThrowIfNull(include);
        var closure = new HashSet<string>(roots, StringComparer.Ordinal);
        var pending = new Queue<string>(closure);
        while (pending.TryDequeue(out string? source))
        {
            OfficePackageRelationship[] outgoing = Relationships
                .Where(relationship => relationship.SourcePart == source && include(relationship))
                .ToArray();
            if (outgoing.Length == 0)
            {
                continue;
            }

            closure.Add(RelationshipPart(source));
            foreach (string target in outgoing
                         .Where(static relationship => !relationship.External && relationship.TargetPart is not null)
                         .Select(static relationship => relationship.TargetPart!))
            {
                bool sharedOutsideClosure = Relationships.Any(relationship =>
                    !relationship.External
                    && relationship.TargetPart == target
                    && relationship.SourcePart != source
                    && !closure.Contains(relationship.SourcePart));
                if (!sharedOutsideClosure && closure.Add(target))
                {
                    pending.Enqueue(target);
                }
            }
        }
        return closure;
    }

    /// <summary>Returns the OPC relationship part owned by one source part.</summary>
    public static string RelationshipPart(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        int slash = source.LastIndexOf('/');
        return slash < 0
            ? "_rels/" + source + ".rels"
            : source[..slash] + "/_rels/" + source[(slash + 1)..] + ".rels";
    }

    private static CliException UnexpectedMutation(IEnumerable<string> changes)
    {
        string[] unexpected = changes.ToArray();
        return new CliException(
            ErrorCodes.UnexpectedPackageMutation,
            $"The document engine changed {unexpected.Length} package item(s) outside the requested operation closure: "
                + string.Join(", ", unexpected.Take(5))
                + (unexpected.Length > 5 ? ", ..." : "."),
            hint: "The staged output was not published. Narrow the edit batch or report the unexpected package changes.",
            details: new JsonObject
            {
                ["available"] = new JsonArray(unexpected
                    .Take(100)
                    .Select(static change => JsonValue.Create(change))
                    .ToArray()),
            });
    }

    private static IReadOnlyDictionary<RelationshipIdentity, int> RelationshipCounts(
        OfficePackageInventory inventory) => inventory.Relationships
        .GroupBy(static relationship => new RelationshipIdentity(
            relationship.SourcePart,
            relationship.Type,
            relationship.External ? relationship.Target : relationship.TargetPart!,
            relationship.External))
        .ToDictionary(static group => group.Key, static group => group.Count());

    internal static string ValidatePath(string value)
    {
        if (string.IsNullOrEmpty(value)
            || value[0] == '/'
            || value.Contains('\\', StringComparison.Ordinal)
            || value.IndexOfAny([':', '?', '#']) >= 0
            || value.Any(static character => char.IsControl(character)))
        {
            throw new InvalidDataException($"Package part path '{value}' is unsafe.");
        }

        string path = value.EndsWith("/", StringComparison.Ordinal)
            ? value[..^1]
            : value;
        if (string.IsNullOrEmpty(path)
            || path.Split('/').Any(static segment =>
                segment.Length == 0 || segment is "." or ".."))
        {
            throw new InvalidDataException($"Package part path '{value}' is unsafe.");
        }
        return path;
    }

    private static (long Size, string Sha256) Hash(
        ZipArchiveEntry entry,
        long maximumBytes)
    {
        using Stream input = entry.Open();
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        long size = 0;
        try
        {
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) != 0)
            {
                size = checked(size + read);
                if (size > maximumBytes)
                {
                    throw new InvalidDataException(
                        $"Package exceeds the decompressed-byte budget while reading '{entry.FullName}'.");
                }
                hash.AppendData(buffer, 0, read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return (size, Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());
    }
}

internal sealed record RelationshipIdentity(
    string Source,
    string Type,
    string Target,
    bool External);

/// <summary>Fingerprint of one decompressed Office package part.</summary>
public sealed record OfficePackagePart(string Path, long SizeBytes, string Sha256)
{
    /// <summary>Resolved OPC content type, when the package declares one.</summary>
    public string? ContentType { get; init; }
}

/// <summary>One normalized OPC content type declaration.</summary>
public sealed record OfficePackageContentTypeDeclaration(
    string Scope,
    string Name,
    string ContentType);

/// <summary>One OPC relationship with a package-normalized internal target.</summary>
public sealed record OfficePackageRelationship(
    string SourcePart,
    string Id,
    string Type,
    string Target,
    string? TargetPart,
    bool External);
