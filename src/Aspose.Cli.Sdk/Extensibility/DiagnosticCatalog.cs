using Aspose.Cli.Sdk.Diagnostics;
using Aspose.Cli.Sdk.Serialization;

namespace Aspose.Cli.Sdk.Extensibility;

/// <summary>Validated immutable aggregate of common and compiled-product diagnostics.</summary>
public sealed class DiagnosticCatalog
{
    private readonly ProductResourceCatalog _resources;

    private DiagnosticCatalog(IReadOnlyList<DiagnosticDescriptor> descriptors, ProductResourceCatalog resources)
    {
        All = descriptors;
        _resources = resources;
        Errors = Array.AsReadOnly(All
            .Where(static item => item.Severity == DiagnosticSeverity.Error)
            .ToArray());
        Warnings = Array.AsReadOnly(All
            .Where(static item => item.Severity == DiagnosticSeverity.Warning)
            .ToArray());
    }

    /// <summary>All diagnostics in deterministic code order.</summary>
    public IReadOnlyList<DiagnosticDescriptor> All { get; }

    /// <summary>Fatal diagnostics in deterministic code order.</summary>
    public IReadOnlyList<DiagnosticDescriptor> Errors { get; }

    /// <summary>Non-fatal diagnostics in deterministic code order.</summary>
    public IReadOnlyList<DiagnosticDescriptor> Warnings { get; }

    /// <summary>
    /// This catalog with the diagnostics of another owner, such as the host, checked as the
    /// catalog checks its own: one owner and one severity per code, and a known details schema.
    /// </summary>
    /// <exception cref="InvalidOperationException">A descriptor breaks one of those rules.</exception>
    public DiagnosticCatalog With(IEnumerable<DiagnosticDescriptor> descriptors, string owner)
    {
        ArgumentNullException.ThrowIfNull(descriptors);
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        var byCode = All.ToDictionary(static descriptor => Key(descriptor.Severity, descriptor.Code), StringComparer.Ordinal);
        foreach (DiagnosticDescriptor descriptor in descriptors)
        {
            Register(descriptor, owner, _resources, byCode);
        }

        return Create(byCode, _resources);
    }

    internal static DiagnosticCatalog Build(
        IReadOnlyList<ProductDefinition> products,
        ProductResourceCatalog resources)
    {
        var byCode = new Dictionary<string, DiagnosticDescriptor>(
            StringComparer.Ordinal);
        foreach (DiagnosticDescriptor descriptor in CommonDiagnostics.All)
        {
            Register(descriptor, CommonDiagnostics.Owner, resources, byCode);
        }
        foreach (ProductDefinition product in products)
        {
            foreach (DiagnosticDescriptor descriptor in product.Diagnostics)
            {
                Register(
                    descriptor,
                    product.Manifest.Id,
                    resources,
                    byCode);
            }
        }

        return Create(byCode, resources);
    }

    private static DiagnosticCatalog Create(
        Dictionary<string, DiagnosticDescriptor> byCode,
        ProductResourceCatalog resources) =>
        new(
            Array.AsReadOnly(byCode.Values
                .OrderBy(static descriptor => descriptor.Code, StringComparer.Ordinal)
                .ToArray()),
            resources);

    private static void Register(
        DiagnosticDescriptor descriptor,
        string expectedOwner,
        ProductResourceCatalog resources,
        IDictionary<string, DiagnosticDescriptor> byCode)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (!IsScreamingSnakeCase(descriptor.Code))
        {
            throw new InvalidOperationException(
                $"Diagnostic code '{descriptor.Code}' is not SCREAMING_SNAKE_CASE.");
        }
        if (!string.Equals(
                descriptor.Owner,
                expectedOwner,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Diagnostic '{descriptor.Code}' owner '{descriptor.Owner}' "
                + $"does not match contribution owner '{expectedOwner}'.");
        }
        if (descriptor.Severity == DiagnosticSeverity.Error)
        {
            if (descriptor.ExitCode is null
                || descriptor.ExitCode
                    == global::Aspose.Cli.Sdk.Errors.ExitCode.Success
                || !Enum.IsDefined(descriptor.ExitCode.Value))
            {
                throw new InvalidOperationException(
                    $"Error diagnostic '{descriptor.Code}' has invalid exit code.");
            }
        }
        else if (descriptor.ExitCode is not null)
        {
            throw new InvalidOperationException(
                $"Warning diagnostic '{descriptor.Code}' must not declare an exit code.");
        }

        if (!SchemaExists(descriptor.DetailsSchemaId, resources))
        {
            throw new InvalidOperationException(
                $"Diagnostic '{descriptor.Code}' references unknown details schema "
                + $"'{descriptor.DetailsSchemaId}'.");
        }
        DiagnosticDescriptor? sameCode = byCode.Values.FirstOrDefault(
            existing => string.Equals(
                existing.Code,
                descriptor.Code,
                StringComparison.Ordinal));
        if (sameCode is not null
            && sameCode.Severity != descriptor.Severity)
        {
            throw new InvalidOperationException(
                $"Diagnostic code '{descriptor.Code}' is reused across severities.");
        }
        string key = Key(descriptor.Severity, descriptor.Code);
        if (!byCode.TryAdd(key, descriptor))
        {
            DiagnosticDescriptor existing = byCode[key];
            throw new InvalidOperationException(
                $"{descriptor.Severity} diagnostic '{descriptor.Code}' has multiple owners: "
                + $"'{existing.Owner}' and '{descriptor.Owner}'.");
        }
    }

    private static string Key(DiagnosticSeverity severity, string code) =>
        $"{severity}:{code}";

    private static bool IsScreamingSnakeCase(string code) =>
        code.Length > 0
        && char.IsAsciiLetterUpper(code[0])
        && code.All(static character =>
            char.IsAsciiLetterUpper(character) || char.IsAsciiDigit(character) || character == '_');

    // Every schema is written from records, so a published id is a well-formed document; the
    // check never writes one, which most invocations would not otherwise read.
    private static bool SchemaExists(
        string id,
        ProductResourceCatalog resources) =>
        SdkSchemaCatalog.Schemas.Contains(id) || resources.Contains(id);
}
