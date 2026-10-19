using System.Net;
using System.Text;
using System.Text.Json;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Host.Viewer;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.Sdk.Serialization;
using Aspose.Cli.Sdk.Views;

namespace Aspose.Cli.Host.Review;

/// <summary>
/// Publishes one immutable static review bundle as one directory, new or, with
/// <c>--overwrite</c>, replacing an existing one: the rendered view parts, their
/// <c>view.json</c> manifest, <c>review.json</c> and an <c>index.html</c> that
/// presents both in the shared viewer.
/// </summary>
internal static class ReviewEvidenceWriter
{
    public const int DefaultMaxItems = 256;
    public const int MaximumMaxItems = 8_192;
    internal const string ViewManifestFile = "view.json";
    private const string ArtifactsBase = "artifacts/";

    public static ReviewResult Write(
        string sourcePath,
        string productId,
        string outputDirectory,
        int maxItems,
        bool visualInspectionRequired,
        ViewPresentation presentation,
        Func<IViewArtifactSink, ViewManifest> render,
        Func<ViewManifest, ProductReviewAssessment> assess,
        LicenseState license,
        ContractJsonSerializer serializer,
        ResourceBudgetLedger budgets,
        IReadOnlyList<string>? codes = null,
        Func<ReviewResult, ReviewResult>? disclose = null,
        bool overwrite = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(productId);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(presentation);
        ArgumentNullException.ThrowIfNull(render);
        ArgumentNullException.ThrowIfNull(assess);
        ArgumentNullException.ThrowIfNull(serializer);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxItems, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxItems, MaximumMaxItems);

        string source = Path.GetFullPath(sourcePath);
        string target = Path.GetFullPath(outputDirectory);
        EnsureTarget(target, overwrite, source);
        using var publication = new AtomicNewDirectoryWriter(budgets, target, "review", overwrite);
        string staging = publication.StagingDirectory;
        try
        {
            string evidenceDirectory = Directory.CreateDirectory(Path.Combine(staging, "artifacts")).FullName;
            LocalServiceResourceLimits limits = LocalServiceResourceLimits.Resolve();
            ViewManifest manifest = ViewRendering.Render(render, evidenceDirectory, maxItems, limits);
            ProductReviewAssessment assessment = assess(manifest);
            string viewJson = JsonSerializer.Serialize(manifest, SdkJsonContext.Default.ViewManifest);
            File.WriteAllText(
                Path.Combine(evidenceDirectory, ViewManifestFile),
                viewJson + Environment.NewLine,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            ViewBundleManifest files = ViewBundleManifest.Validate(
                evidenceDirectory,
                ViewManifestFile,
                limits);
            // The evaluation disclosure is part of the result the evidence files carry.
            disclose ??= static result => result;
            ReviewResult result = disclose(BuildResult(
                source,
                productId,
                target,
                maxItems,
                visualInspectionRequired,
                evidenceDirectory,
                files,
                manifest,
                assessment,
                license,
                codes));

            string reviewJson = serializer.Serialize(result);
            File.WriteAllText(
                Path.Combine(staging, "index.html"),
                ViewerPage.Static(
                    "Review: " + Path.GetFileName(source),
                    presentation,
                    $"{{\"view\":{viewJson},\"review\":{reviewJson}}}",
                    ArtifactsBase,
                    Fallback(manifest)),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.WriteAllText(
                Path.Combine(staging, "review.json"),
                reviewJson + Environment.NewLine,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            publication.Commit();
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
    }

    private static ReviewResult BuildResult(
        string source,
        string productId,
        string target,
        int maxItems,
        bool visualInspectionRequired,
        string evidenceDirectory,
        ViewBundleManifest files,
        ViewManifest manifest,
        ProductReviewAssessment assessment,
        LicenseState license,
        IReadOnlyList<string>? codes)
    {
        int expected = manifest.TotalPartCount;
        int rendered = manifest.Parts.Count;
        int omitted = expected - rendered;
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
            new ReviewArtifact
            {
                Sequence = 2,
                Path = ArtifactsBase + ViewManifestFile,
                Role = "entry",
                MediaType = "application/json",
                Scope = manifest.View,
                Label = ViewManifestFile,
            },
            .. manifest.Parts.Select((part, index) => CreateArtifact(
                evidenceDirectory,
                files,
                manifest.View,
                part,
                index + 3)),
        ];
        IReadOnlyList<Warning>? warnings = EnvelopeParts.CombineWarnings(
            manifest.Warnings, assessment.Warnings);
        IReadOnlyList<ReviewFinding> findings = AssociateEvidence(assessment.Findings ?? [], artifacts, manifest.Parts);
        IReadOnlyList<ReviewFinding> reported = codes is null
            ? findings
            : [.. findings.Where(finding => codes.Contains(finding.Code, StringComparer.Ordinal))];
        return new ReviewResult
        {
            Product = productId,
            Input = FileObjects.Input(source, manifest.SourceFormat, manifest.SourceEncrypted),
            OutputDirectory = target,
            IndexPath = Path.Combine(target, "index.html"),
            Manifest = Path.Combine(target, "review.json"),
            View = manifest.View,
            VisualInspectionRequired = visualInspectionRequired,
            Coverage = new ReviewCoverage
            {
                MaxItemCount = maxItems,
                DiscoveredItemCount = expected,
                ReportedItemCount = rendered,
                Truncated = omitted > 0,
                ExpectedItemCount = expected,
                RenderedItemCount = rendered,
                OmittedItemCount = omitted,
                Complete = omitted == 0 && assessment.Complete
                    && !(warnings?.Any(static warning => warning.AffectsCompleteness) ?? false),
                Metrics = assessment.Coverage ?? [],
            },
            Artifacts = artifacts,
            Findings = reported,
            Filter = codes is null ? null : new ReviewFilter
            {
                Codes = codes,
                OmittedFindingCount = findings.Count - reported.Count,
            },
            License = EnvelopeParts.License(license),
            Warnings = warnings,
        };
    }

    // A finding about parts points at each part's rendering, or at the view entry for a part the
    // coverage limit left unrendered; one about the whole document points at every rendering
    // (or, with none, at the view entry).
    private static IReadOnlyList<ReviewFinding> AssociateEvidence(
        IReadOnlyList<ReviewFinding> findings,
        IReadOnlyList<ReviewArtifact> artifacts,
        IReadOnlyList<ViewPart> parts)
    {
        Dictionary<string, string> partEvidence = new(StringComparer.Ordinal);
        foreach (ViewPart part in parts)
        {
            string path = ArtifactsBase + part.File;
            if (MediaTypes.IsVisual(path))
            {
                partEvidence.TryAdd(part.Id, path);
            }
        }

        string[] entry = artifacts
            .Where(static artifact => artifact.Role == "entry")
            .Select(static artifact => artifact.Path)
            .ToArray();
        string[] evidence = artifacts
            .Where(static artifact => artifact.Role == "evidence"
                && MediaTypes.IsVisual(artifact.Path))
            .Select(static artifact => artifact.Path)
            .ToArray();
        if (evidence.Length == 0)
        {
            evidence = entry;
        }
        return findings.Select(finding => finding.Evidence is { Count: > 0 }
                ? finding
                : finding with
                {
                    Evidence = finding.Parts is not { } ids ? evidence
                        : ids.SelectMany(id => partEvidence.TryGetValue(id, out string? path) ? [path] : entry)
                            .Distinct(StringComparer.Ordinal)
                            .ToArray(),
                })
            .ToArray();
    }

    private static ReviewArtifact CreateArtifact(
        string evidenceDirectory,
        ViewBundleManifest files,
        string view,
        ViewPart part,
        int sequence)
    {
        if (!files.Contains(part.File))
        {
            throw new InvalidOperationException(
                $"The product view part '{part.Id}' names a missing file '{part.File}'.");
        }
        (int? width, int? height) = ReadDimensions(Path.Combine(
            evidenceDirectory,
            part.File.Replace('/', Path.DirectorySeparatorChar)));
        return new ReviewArtifact
        {
            Sequence = sequence,
            Path = ArtifactsBase + part.File,
            Role = "evidence",
            MediaType = MediaTypes.Of(part.File),
            Scope = view,
            Label = part.Label.Length == 0 ? Path.GetFileName(part.File) : part.Label,
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

    /// <summary>What the review page shows without scripts: every part in document order.</summary>
    private static string Fallback(ViewManifest manifest) =>
        "<div class=\"av-fallback\"><p>The viewer needs JavaScript. The rendered parts follow; "
        + "the findings are in <a href=\"review.json\">review.json</a>.</p><div class=\"av-fallback-parts\">"
        + string.Concat(manifest.Parts.Select(static part =>
        {
            string path = WebUtility.HtmlEncode(ArtifactsBase + part.File);
            string label = WebUtility.HtmlEncode(part.Label);
            return part.Kind == ViewPartKinds.Image
                ? $"<figure><img loading=\"lazy\" src=\"{path}\" alt=\"{label}\"><figcaption>{label}</figcaption></figure>"
                : $"<p><a href=\"{path}\">{label}</a></p>";
        }))
        + "</div></div>";

    /// <summary>
    /// Refuses an evidence directory the review may not publish at <paramref name="target"/>:
    /// the input itself or a directory that holds it (<c>OPTION_INVALID</c>, with or without
    /// <paramref name="overwrite"/>, as an output file never names an input); a file; and an
    /// existing directory, unless <paramref name="overwrite"/> lets the review replace one that
    /// is empty or holds earlier evidence, recognised by its <c>review.json</c>. Like
    /// <c>skill install</c>, which replaces only a Skill it installed, the review never replaces
    /// a directory that is not its own (<c>OUTPUT_EXISTS</c>).
    /// </summary>
    internal static void EnsureTarget(string target, bool overwrite, string input)
    {
        string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(target));
        string source = Path.GetFullPath(input);
        if (string.Equals(full, source, StringComparison.OrdinalIgnoreCase)
            || source.StartsWith(full + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw CliErrors.OptionInvalid(
                "--out",
                $"the evidence directory would hold or replace the input file: {full}",
                "Choose an --out outside the input's folder, or omit --out to write <filename>.review beside the input.");
        }

        if (File.Exists(full) && overwrite)
        {
            throw CliErrors.OutputUnwritable(full, "a file occupies the path, and --overwrite replaces only a directory");
        }

        if (File.Exists(full) || Directory.Exists(full) && !overwrite)
        {
            throw CliErrors.OutputDirectoryExists(full);
        }

        if (Directory.Exists(full) && Directory.EnumerateFileSystemEntries(full).Any()
            && !File.Exists(Path.Combine(full, "review.json")))
        {
            throw CliErrors.OutputDirectoryNotOwned(full, "review evidence (review.json)");
        }
    }


}
