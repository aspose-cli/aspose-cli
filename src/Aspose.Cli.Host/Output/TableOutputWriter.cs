using System.Text.Json.Nodes;
using Aspose.Cli.Host.Output.Rendering;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Serialization;

namespace Aspose.Cli.Host.Output;

/// <summary>
/// Human-readable rendering of results. This class dispatches to a renderer per
/// result family. Product-owned results are dispatched through the frozen
/// build-time catalog before the remaining common Host cases. Anything unmapped
/// falls back to JSON rather than hiding data, which the compiler cannot catch:
/// <c>TableRendererCoverageTests</c> fails when an SDK or Host result has no case
/// here, and <c>ProductRendererCoverageTests</c> when a product registers no
/// renderer for one of its results.
/// </summary>
internal sealed class TableOutputWriter : IOutputWriter
{
    private readonly bool _quiet;
    private readonly TableFormat _tableFormat;
    private readonly TextWriter? _output;
    private readonly TextWriter? _error;
    private readonly ProductCatalog _catalog;
    private readonly ContractJsonSerializer _serializer;

    public TableOutputWriter(
        ProductCatalog catalog,
        ContractJsonSerializer serializer,
        bool quiet,
        bool markdown = false,
        TextWriter? output = null,
        TextWriter? error = null)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _quiet = quiet;
        _tableFormat = markdown ? TableFormat.Markdown : TableFormat.Plain;
        _output = output;
        _error = error;
    }

    public void WriteResult(ResultEnvelope result)
    {
        ArgumentNullException.ThrowIfNull(result);

        // Resolve the effective writer at write time so an uninjected writer keeps
        // reading the current Console.Out (its redirection is honored on every call).
        TextWriter output = _output ?? Console.Out;
        var surface = new TableSurface(output, _tableFormat);
        bool rendered = _catalog.TryRender(result, surface);
        if (!rendered)
        {
            switch (result)
            {
                case AppResult app: CommonRenderers.Render(app, surface); break;
                case ProductPreviewStartResult preview: CommonRenderers.Render(preview, surface); break;
                case ProductPreviewStatusResult preview: CommonRenderers.Render(preview, surface); break;
                case ReviewResult review: CommonRenderers.Render(review, surface); break;
                case LicenseStatusResult status: CommonRenderers.Render(status, surface); break;
                case CapabilitiesResult capabilities: CommonRenderers.Render(capabilities, surface); break;
                case CapabilitiesSummaryResult summary: CommonRenderers.Render(summary, surface); break;
                case DoctorResult doctor: CommonRenderers.Render(doctor, surface); break;
                case VersionResult version: CommonRenderers.Render(version, surface); break;
                case SchemaListResult schemas: CommonRenderers.Render(schemas, surface); break;
                case FontListResult fonts: CommonRenderers.Render(fonts, surface); break;
                case FontCheckResult fonts: CommonRenderers.Render(fonts, surface); break;
                case SkillInstallResult skill: CommonRenderers.Render(skill, surface); break;
                case SkillListResult skills: CommonRenderers.Render(skills, surface); break;
                case UpdateResult update: CommonRenderers.Render(update, surface); break;
                default:
                    // Never hide data: unknown result families render as JSON.
                    output.WriteLine(_serializer.Serialize(result));
                    break;
            }
        }

        WriteWindow(output, result.Window);
        WriteWarnings(result.Warnings);
    }

    // The window line is the same for every windowed result, so product renderers omit it.
    private static void WriteWindow(TextWriter output, ResultWindow? window)
    {
        if (window is null)
        {
            return;
        }

        string total = window.Total is long count ? $" of {count}" : string.Empty;
        string state = window.Truncated ? "more remain" : "complete";
        output.WriteLine($"window: {window.Returned}{total} {window.Unit}(s), {state}");
        if (window.Next is { } next)
        {
            output.WriteLine($"next: {next}");
        }
    }

    public void WriteError(ErrorEnvelope error)
    {
        ArgumentNullException.ThrowIfNull(error);

        TextWriter output = _error ?? Console.Error;
        output.WriteLine($"error {error.Error.Code}: {error.Error.Message}");
        if (error.Error.Hint is { } hint)
        {
            output.WriteLine($"  hint: {hint}");
        }

        WriteNotFoundDetails(output, error.Error.Details);

        if (error.Error.Code is "OUTPUT_PUBLICATION_FAILED" or "OUTPUT_PUBLICATION_PARTIAL"
            && error.Error.Details?["targets"] is JsonArray targets)
        {
            output.WriteLine("  recovery:");
            foreach (JsonNode? node in targets)
            {
                if (node is not JsonObject item)
                {
                    continue;
                }

                string target = item["target"]?.GetValue<string>() ?? "(unknown)";
                string status = item["status"]?.GetValue<string>() ?? "unknown";
                bool content = item["contentVerified"]?.GetValue<bool>() ?? false;
                bool metadata = item["metadataVerified"]?.GetValue<bool>() ?? false;
                output.WriteLine(
                    $"    {status}: {target} (content={content.ToString().ToLowerInvariant()}, metadata={metadata.ToString().ToLowerInvariant()})");
            }
        }
    }

    // Not-found details (common/not-found-details) carry the names a caller can use instead; the
    // hint already asks about the closest ones.
    private static void WriteNotFoundDetails(TextWriter output, JsonObject? details)
    {
        if (details?["availableCount"] is not JsonValue countNode
            || !countNode.TryGetValue(out int count))
        {
            return;
        }

        if (details["available"] is JsonArray { Count: > 0 } available)
        {
            string more = count > available.Count ? $" (+{count - available.Count} more)" : string.Empty;
            output.WriteLine($"  available: {JoinNames(available)}{more}");
        }
    }

    private static string JoinNames(JsonArray names) =>
        string.Join(", ", names.Select(static name => name?.GetValue<string>()));

    private void WriteWarnings(IReadOnlyList<Warning>? warnings)
    {
        if (_quiet || warnings is not { Count: > 0 })
        {
            return;
        }

        foreach (Warning warning in warnings)
        {
            (_error ?? Console.Error).WriteLine(
                $"warning {warning.Code}: {warning.Message}");
        }
    }
}
