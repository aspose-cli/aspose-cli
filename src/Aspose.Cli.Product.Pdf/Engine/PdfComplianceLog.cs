using System.Globalization;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>
/// One problem from the SDK's PDF/A compliance log. <paramref name="Section"/> is the log's
/// grouping, such as <c>Fonts</c>, <c>Metadata</c> or <c>EmbeddedFiles</c>.
/// </summary>
internal sealed record PdfComplianceProblem(
    string Severity, string? Clause, int? Page, bool Convertible, string Message, string? Section)
{
    /// <summary>A one-line statement such as <c>6.3.4 (error, page 1): Font 'Helvetica' is not embedded</c>.</summary>
    public override string ToString()
    {
        string where = Page is { } page
            ? string.Create(CultureInfo.InvariantCulture, $"{Severity}, page {page}")
            : Severity;
        return Clause is { } clause ? $"{clause} ({where}): {Message}" : $"({where}): {Message}";
    }
}

/// <summary>
/// Reads the XML log that PDF/A validation and conversion write, so results carry the
/// clause, severity and message of each problem instead of raw XML lines.
/// </summary>
internal static class PdfComplianceLog
{
    /// <summary>
    /// Rejects a PDF/A conversion the SDK reports as unsuccessful: publishing the document
    /// under a PDF/A profile would claim a conformance it does not have.
    /// </summary>
    internal static void EnsureConverted(bool converted, MemoryStream log, string profile)
    {
        if (converted)
        {
            return;
        }

        PdfComplianceProblem[] remaining = Parse(log).Where(static problem => !problem.Convertible).ToArray();
        throw new CliException(
            PdfDiagnostics.PdfaConversionFailed,
            $"The document could not be converted to {profile}: {remaining.Length} problem(s) cannot be fixed automatically"
                + (remaining.Length > 0 ? $"; first: {remaining[0]}" : "."),
            hint: "Fix the reported problems in the source (for example embed its fonts or remove its encryption), or convert to plain PDF.",
            details: new JsonObject
            {
                ["profile"] = profile,
                ["problems"] = new JsonArray(remaining.Take(20).Select(static problem => (JsonNode?)problem.ToString()).ToArray()),
            });
    }

    internal static IReadOnlyList<PdfComplianceProblem> Parse(MemoryStream log)
    {
        if (log.Length == 0)
        {
            return [];
        }

        log.Position = 0;
        return XDocument.Load(log).Descendants("Problem")
            .Select(static problem => new PdfComplianceProblem(
                ((string?)problem.Attribute("Severity") ?? "error").ToLowerInvariant(),
                (string?)problem.Attribute("Clause"),
                int.TryParse((string?)problem.Attribute("Page"), NumberStyles.None, CultureInfo.InvariantCulture, out int page)
                    ? page
                    : null,
                !string.Equals((string?)problem.Attribute("Convertable"), "False", StringComparison.OrdinalIgnoreCase),
                problem.Value.Trim(),
                problem.Parent?.Name.LocalName))
            .ToArray();
    }
}
