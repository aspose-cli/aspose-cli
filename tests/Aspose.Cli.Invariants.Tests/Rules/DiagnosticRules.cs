using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.TestKit.Scenarios;

namespace Aspose.Cli.Invariants.Tests;

// Exit codes, error and warning envelopes and diagnostic codes.
internal static partial class ContractRules
{
    /// <summary>The exit code of each error category capabilities publishes (C-R24).</summary>
    private static readonly Dictionary<string, int> CategoryExitCodes = new(StringComparer.Ordinal)
    {
        ["internal"] = 1,
        ["usage"] = 2,
        ["input"] = 3,
        ["validation"] = 4,
        ["output"] = 5,
        ["format"] = 6,
        ["license"] = 7,
        ["partial"] = 8,
        ["timeout"] = 9,
    };

    /// <summary>
    /// The exit code that C-R24's table gives the errors whose code name places them in one of its
    /// classes, first matching row first: usage and option values 2, the input file 3, licensing 7,
    /// a partial publication 8, the output 5, an unsupported format or content (a render the engine
    /// fails) 6, a timeout 9, ops and targets 4. A code no row names may take any error exit code.
    /// </summary>
    private static readonly (string Subject, int ExitCode, Regex Names)[] NamedExitCodes =
    [
        ("internal errors", 1, new Regex("^INTERNAL_ERROR$")),
        ("usage and option value errors", 2, new Regex("^(USAGE_ERROR|OPTION_[A-Z0-9_]+)$")),
        ("input file errors", 3, new Regex("^(FILE|PASSWORD|INPUT)_[A-Z0-9_]+$")),
        ("license errors", 7, new Regex("^(LICENSE|EVALUATION|EVAL)_[A-Z0-9_]+$")),
        ("partial publications", 8, new Regex("^OUTPUT_PUBLICATION_PARTIAL$")),
        ("output errors", 5, new Regex("^OUTPUT_[A-Z0-9_]+$")),
        ("unsupported format or content errors", 6, new Regex("^(FORMAT_[A-Z0-9_]+|[A-Z0-9_]+_UNSUPPORTED|RENDER_FAILED)$")),
        ("timeouts", 9, new Regex("^[A-Z0-9_]+_TIMEOUT$")),
        ("ops and target errors", 4, new Regex("^(OPS_INVALID|[A-Z0-9_]+_NOT_FOUND)$")),
    ];

    /// <summary>UPPER_SNAKE <c>&lt;SUBJECT&gt;_&lt;PROBLEM/STATE&gt;</c>: two words at least.</summary>
    private static readonly Regex CodeName = new("^[A-Z][A-Z0-9]*(_[A-Z0-9]+)+$");

    /// <summary>The owner of the diagnostics that only the <c>update</c> command emits (C-R28).</summary>
    private const string HostOwner = "host";

    private const string NotFoundDetailsSchema = "v2/common/not-found-details";

    private static IEnumerable<InvariantCase> R24Declared(CliCatalog catalog)
    {
        Diagnostic[] diagnostics = Diagnostics(catalog);
        foreach (string owner in Owners(diagnostics))
        {
            yield return Check("C-R24", $"{owner} exit codes follow their category", () => diagnostics
                .Where(diagnostic => diagnostic.Owner == owner)
                .SelectMany(static diagnostic => CategoryProblems(diagnostic)));
        }
        for (int row = 0; row < NamedExitCodes.Length; row++)
        {
            (string subject, int exitCode, _) = NamedExitCodes[row];
            int index = row;
            yield return Check("C-R24", $"{subject} exit {exitCode}", () => diagnostics
                .Where(diagnostic => diagnostic.IsError && NamedRow(diagnostic.Code) == index && diagnostic.ExitCode != exitCode)
                .Select(diagnostic => $"{diagnostic.Code} exits {Number(diagnostic.ExitCode)}; {subject} exit {exitCode}"));
        }
        yield return Check("C-R24", "one exit code per code", () => diagnostics
            .GroupBy(static diagnostic => diagnostic.Code, StringComparer.Ordinal)
            .Where(static group => group.Select(static diagnostic => diagnostic.ExitCode).Distinct().Count() > 1)
            .Select(static group => $"{group.Key} is declared with exit codes {string.Join(", ", group.Select(static diagnostic => Number(diagnostic.ExitCode)))}"));
    }

    private static IEnumerable<InvariantCase> R27(CliCatalog catalog)
    {
        Diagnostic[] diagnostics = Diagnostics(catalog);
        foreach (string owner in Owners(diagnostics))
        {
            yield return Check("C-R27", $"{owner} warning codes", () => diagnostics
                .Where(diagnostic => diagnostic.Owner == owner && !diagnostic.IsError && diagnostic.Code.EndsWith("_NO_MATCH", StringComparison.Ordinal))
                .Select(static diagnostic => $"{diagnostic.Code}: an operation without effect warns OP_NO_EFFECT, never *_NO_MATCH"));
        }
        // The rule lists no details, yet OP_NO_EFFECT states its id, op and reason in details: the
        // warning may publish details, optional like the error's.
        yield return Check("C-R27", "warning envelope shape", () => EnvelopeShapeProblems(
            catalog, "v2/common/warning", definition: null, "the warning",
            required: ["code", "message"],
            properties: ["code", "message", "hint", "docs", "location", "affectsCompleteness"],
            optional: ["details"]));
    }

    private static IEnumerable<InvariantCase> R28(CliCatalog catalog)
    {
        Diagnostic[] diagnostics = Diagnostics(catalog);
        string[] prefixes = [.. catalog.Products.Select(static product => product.Id.ToUpperInvariant() + "_")];
        foreach (string owner in Owners(diagnostics))
        {
            yield return Check("C-R28", $"{owner} code names", () => diagnostics
                .Where(diagnostic => diagnostic.Owner == owner)
                .SelectMany(diagnostic => NameProblems(diagnostic, prefixes)));
            yield return Check("C-R28", $"{owner} not-found details", () => diagnostics
                .Where(diagnostic => diagnostic.Owner == owner && diagnostic.IsError
                    && diagnostic.Code.EndsWith("_NOT_FOUND", StringComparison.Ordinal)
                    && diagnostic.DetailsSchemaId != NotFoundDetailsSchema)
                .Select(static diagnostic => $"{diagnostic.Code} has details {diagnostic.DetailsSchemaId ?? "of no schema"}, not {NotFoundDetailsSchema}"));
        }
        yield return Check("C-R28", "evaluation codes", () =>
        {
            string[] evaluation = [.. diagnostics.Select(static diagnostic => diagnostic.Code).Where(static code => code.StartsWith("EVALUATION_", StringComparison.Ordinal))];
            return diagnostics
                .Where(static diagnostic => diagnostic.Code.StartsWith("EVAL_", StringComparison.Ordinal))
                .Select(diagnostic => $"{diagnostic.Code}: evaluation codes are EVALUATION_*"
                    + (evaluation.Length == 0 ? string.Empty : $", as {string.Join(", ", evaluation)}"));
        });
        yield return Check("C-R28", "release and update codes belong to host", () => diagnostics
            .Where(static diagnostic => (diagnostic.Code.StartsWith("RELEASE_", StringComparison.Ordinal) || diagnostic.Code.StartsWith("UPDATE_", StringComparison.Ordinal))
                && diagnostic.Owner != HostOwner)
            .Select(static diagnostic => $"{diagnostic.Code} is owned by {diagnostic.Owner}, not {HostOwner}"));
        foreach (CliProduct product in catalog.Products)
        {
            string prefix = product.Id.ToUpperInvariant() + "_";
            yield return Check("C-R28", $"{product.Id} review check codes", () => ReviewCodes(catalog, product.Id)
                .SelectMany(code => ReviewNameProblems(code, prefix)));
        }
        yield return Check("C-R28", "codes declared once", () =>
        {
            var problems = new List<string>();
            problems.AddRange(diagnostics
                .GroupBy(static diagnostic => diagnostic.Code, StringComparer.Ordinal)
                .Where(static group => group.Count() > 1)
                .Select(static group => $"{group.Key} is declared {group.Count()} times, by {string.Join(", ", group.Select(static diagnostic => diagnostic.Owner))}"));
            HashSet<string> codes = [.. diagnostics.Select(static diagnostic => diagnostic.Code)];
            problems.AddRange(catalog.Products
                .SelectMany(product => ReviewCodes(catalog, product.Id))
                .Distinct(StringComparer.Ordinal)
                .Where(codes.Contains)
                .Order(StringComparer.Ordinal)
                .Select(static code => $"review check {code} is also a diagnostic code"));
            return problems;
        });
    }

    // ----- helpers -------------------------------------------------------------------------

    /// <summary>One entry of the capabilities diagnostics catalog.</summary>
    private sealed record Diagnostic(string Code, string Owner, string Severity, string Category, int? ExitCode, string? DetailsSchemaId)
    {
        public bool IsError => Severity == "error";
    }

    private static Diagnostic[] Diagnostics(CliCatalog catalog) =>
    [
        .. catalog.Document["diagnostics"]!.AsArray().Select(static node => new Diagnostic(
            node!["code"]!.GetValue<string>(),
            node["owner"]!.GetValue<string>(),
            node["severity"]!.GetValue<string>(),
            node["category"]!.GetValue<string>(),
            node["exitCode"]?.GetValue<int>(),
            node["detailsSchemaId"]?.GetValue<string>())),
    ];

    private static string[] Owners(IEnumerable<Diagnostic> diagnostics) =>
        [.. diagnostics.Select(static diagnostic => diagnostic.Owner).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

    /// <summary>The index of the first row of <see cref="NamedExitCodes"/> that names a code, or -1.</summary>
    private static int NamedRow(string code) => Array.FindIndex(NamedExitCodes, row => row.Names.IsMatch(code));

    private static string Number(int? exitCode) => exitCode?.ToString(CultureInfo.InvariantCulture) ?? "none";

    private static IEnumerable<string> CategoryProblems(Diagnostic diagnostic)
    {
        if (!diagnostic.IsError)
        {
            if (diagnostic.ExitCode is { } exit)
            {
                yield return $"{diagnostic.Code} is a {diagnostic.Severity} yet declares exit code {exit}";
            }
            yield break;
        }
        if (!CategoryExitCodes.TryGetValue(diagnostic.Category, out int expected))
        {
            yield return $"{diagnostic.Code} has category '{diagnostic.Category}', which the exit code table does not name";
        }
        else if (diagnostic.ExitCode != expected)
        {
            yield return $"{diagnostic.Code} exits {Number(diagnostic.ExitCode)}; its category {diagnostic.Category} exits {expected}";
        }
    }

    private static IEnumerable<string> NameProblems(Diagnostic diagnostic, string[] productPrefixes)
    {
        if (!CodeName.IsMatch(diagnostic.Code))
        {
            yield return $"{diagnostic.Code} is not UPPER_SNAKE <SUBJECT>_<PROBLEM>";
        }
        if (productPrefixes.FirstOrDefault(prefix => diagnostic.Code.StartsWith(prefix, StringComparison.Ordinal)) is { } prefix)
        {
            string kind = diagnostic.IsError ? "an error" : diagnostic.Category == "verification" ? "a verification" : "a warning";
            yield return $"{diagnostic.Code}: {kind} code carries no product prefix ({prefix})";
        }
    }

    private static IEnumerable<string> ReviewNameProblems(string code, string prefix)
    {
        if (!CodeName.IsMatch(code))
        {
            yield return $"{code} is not UPPER_SNAKE <PRODUCT>_<SUBJECT>_<PROBLEM>";
        }
        if (!code.StartsWith(prefix, StringComparison.Ordinal))
        {
            yield return $"review check {code} does not begin with {prefix}";
        }
    }

    /// <summary>The codes of the review checks capabilities publishes for a product.</summary>
    private static string[] ReviewCodes(CliCatalog catalog, string product) =>
    [
        .. (catalog.Document["products"]!.AsArray()
                .Single(node => node!["id"]!.GetValue<string>() == product)!["review"]?["checks"] as JsonArray ?? [])
            .Select(static check => check!["code"]!.GetValue<string>()),
    ];

    /// <summary>
    /// The differences between an envelope schema's object, the schema itself or one of its
    /// definitions, and the rule's shape: exactly its required members and its members, and no other;
    /// an <paramref name="optional"/> member may be published or not, but never required.
    /// </summary>
    private static IEnumerable<string> EnvelopeShapeProblems(
        CliCatalog catalog, string schemaId, string? definition, string what, string[] required, string[] properties, string[]? optional = null)
    {
        optional ??= [];
        JsonNode schema = JsonNode.Parse(catalog.SchemaText(schemaId))!;
        JsonNode node = definition is null ? schema : schema["$defs"]?[definition] ?? new JsonObject();
        string[] declaredRequired = [.. (node["required"] as JsonArray ?? []).Select(static item => item!.GetValue<string>()).Order(StringComparer.Ordinal)];
        string[] declaredProperties = [.. (node["properties"] as JsonObject ?? []).Select(static property => property.Key).Where(key => !optional.Contains(key)).Order(StringComparer.Ordinal)];
        string[] expectedRequired = [.. required.Order(StringComparer.Ordinal)];
        string[] expectedProperties = [.. properties.Order(StringComparer.Ordinal)];
        if (!declaredRequired.SequenceEqual(expectedRequired))
        {
            yield return $"{what} requires [{string.Join(", ", declaredRequired)}], not [{string.Join(", ", expectedRequired)}]";
        }
        if (!declaredProperties.SequenceEqual(expectedProperties))
        {
            yield return $"{what} has members [{string.Join(", ", declaredProperties)}], not [{string.Join(", ", expectedProperties)}]";
        }
        if (node["additionalProperties"]?.GetValue<bool>() != false)
        {
            yield return $"{what} allows other members";
        }
    }
}
