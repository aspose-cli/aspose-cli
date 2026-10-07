using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Resources;
using Json.Schema;

namespace Aspose.Cli.TestKit.Scenarios;

/// <summary>
/// The checks every scenario step gets whatever it expects, from the CLI's own catalog:
/// no internal failure, one valid error envelope per failure, and results that conform to the
/// schema they name.
/// </summary>
public static class ScenarioContract
{
    /// <summary>No invocation ends with <c>INTERNAL_ERROR</c> or its exit code.</summary>
    public const string NoInternalError = "no-internal-error";

    /// <summary>
    /// A failure writes nothing to stdout and exactly one JSON error envelope to stderr; its
    /// code is in the diagnostics catalog, its exit code is the catalog's, and its details
    /// conform to the catalog's details schema.
    /// </summary>
    public const string ErrorEnvelope = "error-envelope";

    /// <summary>A result is one JSON document that conforms to the schema it names, and its warning codes are in the catalog.</summary>
    public const string ResultEnvelope = "result-envelope";

    private const string SchemaPrefix = "https://schemas.aspose.com/aspose-cli/";
    private const string SchemaSuffix = ".schema.json";
    private static readonly object SchemaLock = new();
    private static readonly Dictionary<string, JsonSchema> Schemas = new(StringComparer.Ordinal);
    private static readonly BuildOptions SchemaOptions = SchemaTestRegistry.CreateOptions();

    /// <summary>
    /// Checks one invocation and returns the JSON document it wrote, the result or the error
    /// envelope, or null when it wrote none. Commands that print raw documents
    /// (<c>schema</c>, <c>docs</c>) are checked only for their exit code.
    /// </summary>
    public static JsonNode? Check(IReadOnlyList<string> args, CliResult result, Action<string, string> report)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(report);
        CliCatalog catalog = CliCatalog.Current;
        int? internalExit = catalog.Diagnostics.TryGetValue("INTERNAL_ERROR", out CliDiagnostic? internalError) ? internalError.ExitCode : 1;
        if (result.ExitCode == internalExit)
        {
            report(NoInternalError, $"exit {result.ExitCode}, the internal failure exit code: {Excerpt(result.StdErr)}");
        }
        bool raw = args.Count > 0 && args[0] is "schema" or "docs";
        bool succeeded = result.ExitCode == 0 || (result.ExitCode == 8 && !string.IsNullOrWhiteSpace(result.StdOut));
        if (succeeded)
        {
            return raw ? null : CheckResult(result, report);
        }
        return CheckFailure(catalog, result, report);
    }

    /// <summary>Evaluates a JSON value against a schema by id, such as <c>v2/common/not-found-details</c>; null when it conforms.</summary>
    public static string? Validate(string schemaId, JsonNode? value)
    {
        JsonSchema schema = SchemaById(schemaId);
        using JsonDocument instance = JsonDocument.Parse(value?.ToJsonString() ?? "null");
        EvaluationResults evaluation = schema.Evaluate(instance.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });
        if (evaluation.IsValid)
        {
            return null;
        }
        string[] errors =
        [
            .. (evaluation.Details ?? [])
                .Where(static detail => detail.Errors is { Count: > 0 })
                .SelectMany(static detail => detail.Errors!.Select(error => $"{detail.InstanceLocation}: {error.Value}"))
                .Distinct()
                .Take(3),
        ];
        return $"does not conform to {schemaId}" + (errors.Length == 0 ? string.Empty : ": " + string.Join("; ", errors));
    }

    private static JsonNode? CheckResult(CliResult result, Action<string, string> report)
    {
        if (!TryParse(result.StdOut, out JsonNode? document))
        {
            report(ResultEnvelope, $"stdout is not one JSON document: {Excerpt(result.StdOut)}");
            return null;
        }
        if (document?["schema"]?.GetValue<string>() is not { } uri || IdOf(uri) is not { } id)
        {
            report(ResultEnvelope, "the result names no canonical schema");
            return document;
        }
        if (Validate(id, document) is { } invalid)
        {
            report(ResultEnvelope, $"the result {invalid}");
        }
        foreach (string code in Codes(document["warnings"]))
        {
            if (!CliCatalog.Current.Diagnostics.ContainsKey(code))
            {
                report(ResultEnvelope, $"warning {code} is not in the diagnostics catalog");
            }
        }
        return document;
    }

    private static JsonNode? CheckFailure(CliCatalog catalog, CliResult result, Action<string, string> report)
    {
        if (!string.IsNullOrWhiteSpace(result.StdOut))
        {
            report(ErrorEnvelope, $"exit {result.ExitCode} wrote to stdout: {Excerpt(result.StdOut)}");
        }
        if (!TryParse(result.StdErr, out JsonNode? document) || document?["error"] is not JsonObject error)
        {
            report(ErrorEnvelope, $"exit {result.ExitCode} did not write exactly one JSON error envelope to stderr: {Excerpt(result.StdErr)}");
            return null;
        }
        if (document["schema"]?.GetValue<string>() is { } uri && IdOf(uri) is { } id)
        {
            if (Validate(id, document) is { } invalid)
            {
                report(ErrorEnvelope, $"the error envelope {invalid}");
            }
        }
        else
        {
            report(ErrorEnvelope, "the error envelope names no canonical schema");
        }
        string code = error["code"]?.GetValue<string>() ?? string.Empty;
        if (code == "INTERNAL_ERROR")
        {
            report(NoInternalError, $"INTERNAL_ERROR: {error["message"]?.GetValue<string>()}");
        }
        if (!catalog.Diagnostics.TryGetValue(code, out CliDiagnostic? diagnostic))
        {
            report(ErrorEnvelope, $"error code '{code}' is not in the diagnostics catalog");
            return document;
        }
        if (diagnostic.ExitCode != result.ExitCode)
        {
            report(ErrorEnvelope, $"{code} exited {result.ExitCode}; the catalog declares {diagnostic.ExitCode?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "no exit code"}");
        }
        if (diagnostic.DetailsSchemaId is { } detailsSchema && error["details"] is { } details
            && Validate(detailsSchema, details) is { } invalidDetails)
        {
            report(ErrorEnvelope, $"{code} details {invalidDetails}");
        }
        return document;
    }

    /// <summary>The codes of a warnings array.</summary>
    internal static IEnumerable<string> Codes(JsonNode? warnings) =>
        warnings is JsonArray array
            ? array.Select(static warning => warning?["code"]?.GetValue<string>()).OfType<string>()
            : [];

    internal static string Excerpt(string text)
    {
        string flat = text.ReplaceLineEndings(" ").Trim();
        return flat.Length <= 300 ? flat : flat[..300] + "...";
    }

    private static bool TryParse(string text, out JsonNode? document)
    {
        document = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }
        try
        {
            document = JsonNode.Parse(text);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? IdOf(string uri) =>
        uri.StartsWith(SchemaPrefix, StringComparison.Ordinal) && uri.EndsWith(SchemaSuffix, StringComparison.Ordinal)
            ? uri[SchemaPrefix.Length..^SchemaSuffix.Length]
            : null;

    private static JsonSchema SchemaById(string id)
    {
        lock (SchemaLock)
        {
            if (Schemas.TryGetValue(id, out JsonSchema? cached))
            {
                return cached;
            }
            JsonSchema schema = SdkSchemaCatalog.TryRead(id, out _)
                && SchemaOptions.SchemaRegistry.Get(new Uri(SchemaPrefix + id + SchemaSuffix)) is JsonSchema common
                    ? common
                    : JsonSchema.FromText(CliCatalog.Current.SchemaText(id), SchemaOptions);
            Schemas.Add(id, schema);
            return schema;
        }
    }
}
