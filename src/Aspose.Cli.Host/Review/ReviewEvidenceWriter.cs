using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Host.Preview;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.Sdk.Serialization;

namespace Aspose.Cli.Host.Review;

/// <summary>Publishes one immutable static review bundle into a new directory.</summary>
internal static class ReviewEvidenceWriter
{
    public const int DefaultMaxItems = 256;
    public const int MaximumMaxItems = 8_192;

    public static ReviewResult Write(
        string sourcePath,
        string productId,
        string view,
        string outputDirectory,
        int maxItems,
        ProductReviewRenderer renderer,
        LicenseState license,
        ContractJsonSerializer serializer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(productId);
        ArgumentException.ThrowIfNullOrWhiteSpace(view);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(serializer);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxItems, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxItems, MaximumMaxItems);

        string source = Path.GetFullPath(sourcePath);
        string target = Path.GetFullPath(outputDirectory);
        EnsureNewTarget(target);
        string parent = Path.GetDirectoryName(target)
            ?? throw CliErrors.OutputUnwritable(target, "the path has no parent directory");
        string staging = Path.Combine(
            parent,
            $".{Path.GetFileName(target)}.{Guid.NewGuid():N}.review.tmp");
        try
        {
            Directory.CreateDirectory(parent);
            EnsureNewTarget(target);
            Directory.CreateDirectory(staging);
            string evidenceDirectory = Path.Combine(staging, "artifacts");
            Directory.CreateDirectory(evidenceDirectory);

            ProductReviewRenderOutcome outcome = renderer(evidenceDirectory);
            PreviewArtifactManifest manifest = PreviewArtifactManifest.Validate(
                evidenceDirectory,
                outcome.EntryFileName,
                LocalServiceResourceLimits.Resolve());
            RewriteStaticHtml(evidenceDirectory, manifest.Files);
            ReviewResult result = BuildResult(
                source,
                productId,
                view,
                target,
                maxItems,
                evidenceDirectory,
                manifest,
                outcome,
                license);

            File.WriteAllText(
                Path.Combine(staging, "index.html"),
                IndexHtmlV2(result, manifest.EntryFileName),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.WriteAllText(
                Path.Combine(staging, "review.json"),
                serializer.Serialize(result) + Environment.NewLine,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            try
            {
                Directory.Move(staging, target);
            }
            catch (IOException) when (Directory.Exists(target) || File.Exists(target))
            {
                throw ReviewOutputExists(target);
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                throw CliErrors.OutputUnwritable(
                    target,
                    "the review evidence directory could not be published",
                    exception);
            }
            return result;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            throw CliErrors.OutputUnwritable(
                target,
                "the review evidence could not be written",
                exception);
        }
        finally
        {
            TryDeleteStaging(staging, parent);
        }
    }

    private static ReviewResult BuildResult(
        string source,
        string productId,
        string view,
        string target,
        int maxItems,
        string evidenceDirectory,
        PreviewArtifactManifest manifest,
        ProductReviewRenderOutcome outcome,
        LicenseState license)
    {
        string[] evidenceFiles = manifest.Files
            .Where(path => !string.Equals(
                path,
                manifest.EntryFileName,
                StringComparison.OrdinalIgnoreCase))
            .Order(ReviewArtifactPathComparer.Instance)
            .ToArray();
        (int expectedItems, int renderedItems) = ValidateCoverage(
            outcome,
            evidenceFiles,
            maxItems);
        IReadOnlyList<string> selected =
            [manifest.EntryFileName, .. evidenceFiles];
        int omittedItems = Math.Max(0, expectedItems - renderedItems);
        ReviewArtifact[] artifacts =
        [
            new ReviewArtifact
            {
                Sequence = 0,
                Path = "index.html",
                Role = "index",
                MediaType = "text/html",
            },
            new ReviewArtifact
            {
                Sequence = 1,
                Path = "review.json",
                Role = "manifest",
                MediaType = "application/json",
            },
            .. selected.Select((path, index) => CreateArtifact(
                evidenceDirectory,
                manifest.EntryFileName,
                view,
                path,
                index + 2)),
        ];
        IReadOnlyList<ReviewFinding> findings = AssociateEvidence(
            outcome.Findings ?? [],
            artifacts);
        return new ReviewResult
        {
            Product = productId,
            Input = source,
            OutputDirectory = target,
            Index = Path.Combine(target, "index.html"),
            Manifest = Path.Combine(target, "review.json"),
            View = view,
            SourceFormat = outcome.SourceFormatId,
            SourceSizeBytes = outcome.SourceSizeBytes,
            VisualInspectionRequired = outcome.VisualInspectionRequired,
            Coverage = new ReviewCoverage
            {
                MaxItems = maxItems,
                DiscoveredItems = expectedItems,
                ReportedItems = renderedItems,
                Truncated = omittedItems > 0,
                ExpectedItems = expectedItems,
                RenderedItems = renderedItems,
                OmittedItems = omittedItems,
                Complete = omittedItems == 0 && outcome.Complete
                    && !(outcome.Warnings?.Any(static warning => warning.AffectsCompleteness) ?? false),
                Metrics = outcome.Coverage ?? [],
            },
            Artifacts = artifacts,
            Findings = findings,
            License = EnvelopeParts.License(license),
            Warnings = EnvelopeParts.CombineWarnings(EnvelopeParts.OutputWarnings(license), outcome.Warnings),
        };
    }

    private static (int ExpectedItems, int RenderedItems) ValidateCoverage(
        ProductReviewRenderOutcome outcome,
        IReadOnlyList<string> evidenceFiles,
        int maxItems)
    {
        int expected = outcome.ExpectedItems;
        int rendered = outcome.RenderedItems;
        if (expected < 0
            || rendered < 0
            || rendered > expected
            || rendered > maxItems)
        {
            throw new InvalidOperationException(
                $"Product review coverage is invalid: expected={expected}, rendered={rendered}, maxItems={maxItems}.");
        }

        int visualEvidence = evidenceFiles.Count(IsVisualEvidence);
        if (visualEvidence != rendered)
        {
            throw new InvalidOperationException(
                $"Product review coverage reports {rendered} rendered item(s), but the artifact set contains {visualEvidence} visual item(s).");
        }
        return (expected, rendered);
    }

    private static IReadOnlyList<ReviewFinding> AssociateEvidence(
        IReadOnlyList<ReviewFinding> findings,
        IReadOnlyList<ReviewArtifact> artifacts)
    {
        string[] evidence = artifacts
            .Where(static artifact => artifact.Role is "entry" or "evidence")
            .Where(static artifact => IsVisualEvidence(artifact.Path))
            .Select(static artifact => artifact.Path)
            .ToArray();
        if (evidence.Length == 0)
        {
            evidence = artifacts
                .Where(static artifact => artifact.Role == "entry")
                .Select(static artifact => artifact.Path)
                .ToArray();
        }
        return findings.Select(finding => finding.Evidence is { Count: > 0 }
                ? finding
                : finding with { Evidence = evidence })
            .ToArray();
    }

    private static bool IsVisualEvidence(string path) =>
        Path.GetExtension(path).ToLowerInvariant() is
            ".svg" or ".png" or ".jpg" or ".jpeg"
            or ".gif" or ".webp" or ".pdf";

    private static ReviewArtifact CreateArtifact(
        string evidenceDirectory,
        string entry,
        string view,
        string relativePath,
        int sequence)
    {
        (int? width, int? height) = ReadDimensions(Path.Combine(
            evidenceDirectory,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        return new ReviewArtifact
        {
            Sequence = sequence,
            Path = "artifacts/" + relativePath,
            Role = string.Equals(relativePath, entry, StringComparison.OrdinalIgnoreCase)
                ? "entry"
                : "evidence",
            MediaType = MediaType(relativePath),
            Scope = view,
            Label = Path.GetFileName(relativePath),
            Width = width,
            Height = height,
        };
    }

    private static (int? Width, int? Height) ReadDimensions(string path)
    {
        if (!Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase))
        {
            return (null, null);
        }
        using FileStream stream = File.OpenRead(path);
        Span<byte> header = stackalloc byte[24];
        if (stream.Read(header) != header.Length
            || !header[..8].SequenceEqual(
                new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
        {
            return (null, null);
        }
        int width = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(header[16..20]);
        int height = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(header[20..24]);
        return width > 0 && height > 0 ? (width, height) : (null, null);
    }

    private static void RewriteStaticHtml(
        string evidenceDirectory,
        IReadOnlyList<string> files)
    {
        foreach (string relative in files.Where(path =>
            Path.GetExtension(path).Equals(".html", StringComparison.OrdinalIgnoreCase)
            || Path.GetExtension(path).Equals(".htm", StringComparison.OrdinalIgnoreCase)))
        {
            string path = Path.Combine(
                evidenceDirectory,
                relative.Replace('/', Path.DirectorySeparatorChar));
            string html = File.ReadAllText(path);
            string rewritten = html
                .Replace("/asset/", string.Empty, StringComparison.Ordinal)
                .Replace("/live/shell.css", "data:text/css,", StringComparison.Ordinal)
                .Replace("/live/client.js", "data:text/javascript,", StringComparison.Ordinal)
                .Replace("/live/events", "#static-review-events-disabled", StringComparison.Ordinal)
                .Replace("/live/refresh", "#static-review-refresh-disabled", StringComparison.Ordinal);
            if (!string.Equals(html, rewritten, StringComparison.Ordinal))
            {
                File.WriteAllText(
                    path,
                    rewritten,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
        }
    }

    private static string IndexHtmlV2(ReviewResult result, string entry)
    {
        string title = WebUtility.HtmlEncode(Path.GetFileName(result.Input));
        string source = WebUtility.HtmlEncode("artifacts/" + entry);
        string product = WebUtility.HtmlEncode(result.Product);
        string coverage = $"{result.Coverage.RenderedItems}/{result.Coverage.ExpectedItems}";
        string findings = result.Findings.Count == 0
            ? "<p class=\"quiet\">No deterministic findings. AI visual inspection is still required.</p>"
            : "<ul>" + string.Concat(result.Findings.Select(finding =>
                $"<li class=\"{WebUtility.HtmlEncode(finding.Severity)}\"><code>{WebUtility.HtmlEncode(finding.Code)}</code> "
                + WebUtility.HtmlEncode(finding.Message)
                + (finding.Location is null
                    ? string.Empty
                    : $" <span class=\"quiet\">({WebUtility.HtmlEncode(finding.Location)})</span>")
                + (finding.Hint is null
                    ? string.Empty
                    : $"<br><span class=\"quiet\">Fix: {WebUtility.HtmlEncode(finding.Hint)}</span>")
                + "</li>")) + "</ul>";
        string gallery = string.Concat(result.Artifacts
            .Where(static artifact => artifact.Role == "evidence"
                && artifact.MediaType.StartsWith("image/", StringComparison.Ordinal))
            .Select(artifact =>
                $"<figure><a href=\"{WebUtility.HtmlEncode(artifact.Path)}\"><img loading=\"lazy\" src=\"{WebUtility.HtmlEncode(artifact.Path)}\" alt=\"{WebUtility.HtmlEncode(artifact.Label)}\"></a>"
                + $"<figcaption>{WebUtility.HtmlEncode(artifact.Label)}</figcaption></figure>"));
        return "<!doctype html>\n"
            + "<html lang=\"en\"><head><meta charset=\"utf-8\">"
            + "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">"
            + $"<title>Review: {title}</title>"
            + "<style>body{margin:0;font:14px system-ui;background:#f4f5f7;color:#17202a}"
            + "header,main{padding:16px;max-width:1440px;margin:auto}header{background:#fff;border-bottom:1px solid #ccd1d1}"
            + "iframe{display:block;width:100%;height:70vh;border:1px solid #ccd1d1;background:#fff}"
            + ".required,.error{color:#9c2f12;font-weight:600}.warning{color:#7a5200}.quiet{color:#607080}"
            + ".gallery{display:grid;grid-template-columns:repeat(auto-fit,minmax(260px,1fr));gap:16px}figure{margin:0;padding:10px;background:#fff;border:1px solid #d7dce1}img{display:block;max-width:100%;height:auto;margin:auto}figcaption{text-align:center;margin-top:8px}</style></head><body>"
            + $"<header><strong>{title}</strong> &middot; {product} &middot; coverage {coverage} &middot; "
            + "<span class=\"required\">Visual inspection required</span> &middot; "
            + "<a href=\"review.json\">review.json</a></header><main>"
            + "<h2>Deterministic findings</h2>" + findings
            + "<h2>Product review</h2>"
            + $"<iframe title=\"Review evidence\" src=\"{source}\"></iframe>"
            + "<h2>Visual artifacts</h2><div class=\"gallery\">" + gallery + "</div></main>"
            + "</body></html>\n";
    }

    private static string MediaType(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".html" or ".htm" => "text/html",
            ".css" => "text/css",
            ".js" => "text/javascript",
            ".json" => "application/json",
            ".svg" => "image/svg+xml",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".pdf" => "application/pdf",
            ".txt" => "text/plain",
            _ => "application/octet-stream",
        };

    private static void EnsureNewTarget(string target)
    {
        if (Directory.Exists(target) || File.Exists(target))
        {
            throw ReviewOutputExists(target);
        }
    }

    private static CliException ReviewOutputExists(string target) =>
        new(
            ErrorCodes.OutputExists,
            $"Review output directory already exists: {target}",
            hint: "Choose a new directory with --out; review evidence is never overwritten.",
            details: new JsonObject { ["path"] = target });

    private static void TryDeleteStaging(string staging, string parent)
    {
        string full = Path.GetFullPath(staging);
        string root = Path.GetFullPath(parent)
            .TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!full.StartsWith(
                root,
                OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal)
            || !Path.GetFileName(full).EndsWith(
                ".review.tmp",
                StringComparison.Ordinal))
        {
            return;
        }
        try
        {
            if (Directory.Exists(full))
            {
                Directory.Delete(full, recursive: true);
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            // The target was not published; a later user cleanup can reclaim it.
        }
    }
}
