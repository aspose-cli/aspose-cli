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

    /// <summary>The problem as a result reports it.</summary>
    public PdfComplianceIssue ToIssue() => new()
    {
        Code = PdfDiagnostics.PdfaNonconformance.Code,
        Message = Message,
        Clause = Clause,
        Severity = Severity,
        Page = Page,
        Convertible = Convertible,
    };

    /// <summary>The problem as <c>details.problems</c> lists it, in the shape of <see cref="ToIssue"/>.</summary>
    public JsonObject ToDetails()
    {
        var details = new JsonObject
        {
            ["code"] = PdfDiagnostics.PdfaNonconformance.Code,
            ["message"] = Message,
        };
        if (Clause is { } clause)
        {
            details["clause"] = clause;
        }

        details["severity"] = Severity;
        if (Page is { } page)
        {
            details["page"] = page;
        }

        details["convertible"] = Convertible;
        return details;
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
        throw Failed(
            profile,
            remaining,
            $"{remaining.Length} problem(s) cannot be fixed automatically");
    }

    /// <summary>
    /// Rejects a converted document that validation of its saved file finds not conforming,
    /// although the conversion reported success (known issue PDF-PDFA-RADIO-APPEARANCE in
    /// KNOWN-ISSUES.md). Annotation appearance problems (the profile's
    /// <paramref name="appearanceClause"/>) name the button fields on their pages that have one
    /// appearance for all their states, which flattening removes.
    /// </summary>
    internal static void EnsureConformant(bool valid, MemoryStream log, string profile, string? appearanceClause, Func<int, IEnumerable<string>> buttonFieldsOnPage)
    {
        if (valid)
        {
            return;
        }

        PdfComplianceProblem[] problems = [.. Parse(log)];
        string[] fields = [.. problems
            .Where(problem => appearanceClause is not null && problem.Clause == appearanceClause && problem.Page is not null)
            .SelectMany(problem => buttonFieldsOnPage(problem.Page!.Value))
            .Distinct(StringComparer.Ordinal)];
        throw Failed(
            profile,
            problems,
            $"the converted file still has {problems.Length} problem(s) the conversion did not fix",
            fields);
    }

    private static CliException Failed(string profile, IReadOnlyList<PdfComplianceProblem> problems, string cause, string[]? fields = null) => new(
        PdfDiagnostics.PdfaConversionFailed,
        $"The document could not be converted to {profile}: {cause}" + (problems.Count > 0 ? $"; first: {problems[0]}" : "."),
        details: new JsonObject
        {
            ["profile"] = profile,
            ["problems"] = new JsonArray(problems.Take(20).Select(static problem => (JsonNode?)problem.ToDetails()).ToArray()),
            ["fields"] = fields is { Length: > 0 } ? new JsonArray([.. fields.Select(static name => (JsonNode)name)]) : null,
        });

    /// <summary>
    /// The problems of the log, each once. The log lists a problem once for every use of the
    /// object it concerns, and marks it convertible on only one of those entries
    /// (PDF-PDFA-LOG-REPEATS): a problem is convertible when any of its entries says so.
    /// </summary>
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
            .GroupBy(static problem => problem with { Convertible = false })
            .Select(static entries => entries.Key with { Convertible = entries.Any(static entry => entry.Convertible) })
            .ToArray();
    }
}
