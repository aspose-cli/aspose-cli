using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace Aspose.Cli.Sdk.IO;

internal sealed record OfficePackageOpcMetadata(
    IReadOnlyDictionary<string, string> ContentTypes,
    IReadOnlyList<OfficePackageContentTypeDeclaration> ContentTypeDeclarations,
    IReadOnlyList<OfficePackageRelationship> Relationships);

/// <summary>Reads only product-neutral OPC content types and relationships.</summary>
internal static class OfficePackageOpcReader
{
    private static readonly XNamespace ContentTypesNamespace =
        "http://schemas.openxmlformats.org/package/2006/content-types";
    private static readonly XNamespace RelationshipsNamespace =
        "http://schemas.openxmlformats.org/package/2006/relationships";

    internal static OfficePackageOpcMetadata Read(
        IReadOnlyDictionary<string, ZipArchiveEntry> entries)
    {
        (IReadOnlyDictionary<string, string> contentTypes,
            IReadOnlyList<OfficePackageContentTypeDeclaration> declarations) =
            ReadContentTypes(entries);
        var relationships = new List<OfficePackageRelationship>();
        foreach ((string path, ZipArchiveEntry entry) in entries.OrderBy(static item => item.Key, StringComparer.Ordinal))
        {
            if (!TryGetRelationshipSource(path, out string? sourcePart))
            {
                continue;
            }

            XDocument document = ReadXml(entry);
            XElement root = document.Root
                ?? throw new InvalidDataException($"OPC relationship part '{path}' is empty.");
            if (root.Name != RelationshipsNamespace + "Relationships")
            {
                throw new InvalidDataException($"OPC relationship part '{path}' has an unexpected root element.");
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (XElement element in root.Elements(RelationshipsNamespace + "Relationship"))
            {
                string id = RequiredAttribute(element, "Id", path);
                string type = RequiredAttribute(element, "Type", path);
                string target = RequiredAttribute(element, "Target", path);
                if (!ids.Add(id))
                {
                    throw new InvalidDataException(
                        $"OPC relationship part '{path}' duplicates relationship id '{id}'.");
                }

                string? targetMode = element.Attribute("TargetMode")?.Value;
                if (targetMode is not null
                    && !string.Equals(targetMode, "Internal", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(targetMode, "External", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        $"OPC relationship part '{path}' has unsupported TargetMode '{targetMode}'.");
                }
                bool external = string.Equals(
                    targetMode,
                    "External",
                    StringComparison.OrdinalIgnoreCase);
                relationships.Add(new OfficePackageRelationship(
                    sourcePart,
                    id,
                    type,
                    target,
                    external ? null : ResolveTarget(sourcePart, target, path),
                    external));
            }
        }

        return new OfficePackageOpcMetadata(
            contentTypes,
            declarations,
            relationships
                .OrderBy(static item => item.SourcePart, StringComparer.Ordinal)
                .ThenBy(static item => item.Id, StringComparer.Ordinal)
                .ToArray());
    }

    private static (
        IReadOnlyDictionary<string, string> ContentTypes,
        IReadOnlyList<OfficePackageContentTypeDeclaration> Declarations) ReadContentTypes(
        IReadOnlyDictionary<string, ZipArchiveEntry> entries)
    {
        if (!entries.TryGetValue("[Content_Types].xml", out ZipArchiveEntry? entry))
        {
            return (new Dictionary<string, string>(StringComparer.Ordinal), []);
        }

        XDocument document = ReadXml(entry);
        XElement root = document.Root
            ?? throw new InvalidDataException("OPC content types are empty.");
        if (root.Name != ContentTypesNamespace + "Types")
        {
            throw new InvalidDataException("OPC content types have an unexpected root element.");
        }

        var defaults = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (XElement element in root.Elements(ContentTypesNamespace + "Default"))
        {
            string extension = RequiredAttribute(element, "Extension", "[Content_Types].xml");
            string contentType = RequiredAttribute(element, "ContentType", "[Content_Types].xml");
            if (extension.Contains('.')
                || extension.Contains('/')
                || !defaults.TryAdd(extension, contentType))
            {
                throw new InvalidDataException($"OPC default content type extension '{extension}' is ambiguous.");
            }
        }

        var overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (XElement element in root.Elements(ContentTypesNamespace + "Override"))
        {
            string partName = RequiredAttribute(element, "PartName", "[Content_Types].xml");
            if (!partName.StartsWith("/", StringComparison.Ordinal))
            {
                throw new InvalidDataException($"OPC override part name '{partName}' is not package-absolute.");
            }
            string partPath = OfficePackageInventory.ValidatePath(partName[1..]);
            string contentType = RequiredAttribute(element, "ContentType", "[Content_Types].xml");
            if (!overrides.TryAdd(partPath, contentType))
            {
                throw new InvalidDataException($"OPC override part name '{partName}' is duplicated.");
            }
        }

        IReadOnlyDictionary<string, string> contentTypes = entries.Keys.ToDictionary(
            static path => path,
            path => overrides.TryGetValue(path, out string? overridden)
                ? overridden
                : defaults.GetValueOrDefault(Path.GetExtension(path).TrimStart('.')),
            StringComparer.Ordinal)
            .Where(static item => item.Value is not null)
            .ToDictionary(static item => item.Key, static item => item.Value!, StringComparer.Ordinal);
        IReadOnlyList<OfficePackageContentTypeDeclaration> declarations = defaults
            .Select(static item => new OfficePackageContentTypeDeclaration(
                "default",
                item.Key.ToLowerInvariant(),
                item.Value))
            .Concat(overrides.Select(static item => new OfficePackageContentTypeDeclaration(
                "override",
                item.Key,
                item.Value)))
            .OrderBy(static item => item.Scope, StringComparer.Ordinal)
            .ThenBy(static item => item.Name, StringComparer.Ordinal)
            .ToArray();
        return (contentTypes, declarations);
    }

    private static string ResolveTarget(string sourcePart, string target, string relationshipPart)
    {
        string path = target.Split('#', 2)[0];
        if (path.Length == 0 || path.Contains('?'))
        {
            throw new InvalidDataException(
                $"OPC relationship '{relationshipPart}' has an invalid internal target '{target}'.");
        }

        try
        {
            path = Uri.UnescapeDataString(path);
        }
        catch (UriFormatException exception)
        {
            throw new InvalidDataException(
                $"OPC relationship '{relationshipPart}' has an invalid escaped target '{target}'.",
                exception);
        }

        if (path.StartsWith("/", StringComparison.Ordinal))
        {
            path = path[1..];
        }
        else if (sourcePart.Length > 0)
        {
            string? directory = Path.GetDirectoryName(sourcePart)?.Replace('\\', '/');
            path = string.IsNullOrEmpty(directory) ? path : directory + "/" + path;
        }

        var segments = new List<string>();
        foreach (string segment in path.Split('/'))
        {
            if (segment == "..")
            {
                if (segments.Count == 0)
                {
                    throw new InvalidDataException(
                        $"OPC relationship '{relationshipPart}' escapes the package root.");
                }
                segments.RemoveAt(segments.Count - 1);
            }
            else if (segment.Length == 0 || segment == ".")
            {
                throw new InvalidDataException(
                    $"OPC relationship '{relationshipPart}' has an ambiguous target '{target}'.");
            }
            else
            {
                segments.Add(segment);
            }
        }

        return OfficePackageInventory.ValidatePath(string.Join('/', segments));
    }

    private static bool TryGetRelationshipSource(string path, out string sourcePart)
    {
        if (string.Equals(path, "_rels/.rels", StringComparison.Ordinal))
        {
            sourcePart = string.Empty;
            return true;
        }
        if (path.StartsWith("_rels/", StringComparison.Ordinal)
            && path.EndsWith(".rels", StringComparison.Ordinal))
        {
            sourcePart = OfficePackageInventory.ValidatePath(path[6..^5]);
            return true;
        }

        const string marker = "/_rels/";
        int markerIndex = path.LastIndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0 || !path.EndsWith(".rels", StringComparison.Ordinal))
        {
            sourcePart = string.Empty;
            return false;
        }

        string directory = path[..markerIndex];
        string file = path[(markerIndex + marker.Length)..^5];
        sourcePart = OfficePackageInventory.ValidatePath(directory + "/" + file);
        return true;
    }

    private static XDocument ReadXml(ZipArchiveEntry entry)
    {
        using Stream stream = entry.Open();
        using XmlReader reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = Math.Max(1, entry.Length),
        });
        return XDocument.Load(reader, LoadOptions.None);
    }

    private static string RequiredAttribute(XElement element, string name, string part)
    {
        string? value = element.Attribute(name)?.Value;
        if (string.IsNullOrWhiteSpace(value)
            || value.Any(static character => char.IsControl(character)))
        {
            throw new InvalidDataException(
                $"OPC part '{part}' has a relationship or content type without a valid {name}.");
        }
        return value;
    }
}
