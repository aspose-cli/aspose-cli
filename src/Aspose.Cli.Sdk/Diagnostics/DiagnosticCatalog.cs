using System.Text.Json;
using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Resources;

namespace Aspose.Cli.Sdk.Diagnostics;

/// <summary>Validated immutable aggregate of common and compiled-product diagnostics.</summary>
public sealed class DiagnosticCatalog
{
    private static readonly Regex CodePattern = new(
        "^[A-Z][A-Z0-9_]*$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    private DiagnosticCatalog(IReadOnlyList<DiagnosticDescriptor> descriptors)
    {
        All = descriptors;
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

        return new DiagnosticCatalog(Array.AsReadOnly(
            byCode.Values
                .OrderBy(static descriptor => descriptor.Code, StringComparer.Ordinal)
                .ToArray()));
    }

    private static void Register(
        DiagnosticDescriptor descriptor,
        string expectedOwner,
        ProductResourceCatalog resources,
        IDictionary<string, DiagnosticDescriptor> byCode)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (!CodePattern.IsMatch(descriptor.Code))
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
        if (string.IsNullOrWhiteSpace(descriptor.Category)
            || string.IsNullOrWhiteSpace(descriptor.MessageTemplateId)
            || string.IsNullOrWhiteSpace(descriptor.HintTemplateId))
        {
            throw new InvalidOperationException(
                $"Diagnostic '{descriptor.Code}' has incomplete category/template metadata.");
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

    private static bool SchemaExists(
        string id,
        ProductResourceCatalog resources)
    {
        string? document;
        if (!SdkSchemaCatalog.TryRead(id, out document)
            && !resources.TryRead(id, out document))
        {
            return false;
        }
        try
        {
            using JsonDocument _ = JsonDocument.Parse(document);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
