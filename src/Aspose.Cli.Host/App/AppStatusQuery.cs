using System.Runtime.InteropServices;
using Aspose.Cli.Host.Catalog;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Host.Licensing;
using Aspose.Cli.Host.ViewerService;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk;

namespace Aspose.Cli.Host.App;

/// <summary>Builds App status and bounded platform diagnostics.</summary>
internal sealed class AppStatusQuery
{
    private readonly AppPreferencesStore _preferences;
    private readonly ProductCatalog _catalog;
    private readonly AppCliGateway _cli;
    private readonly AppDocumentSession _sessions;
    private readonly string _experience;
    private readonly IReadOnlyList<AppProductView> _products;
    private readonly IReadOnlyList<AppSkillView> _skills;
    private readonly IReadOnlyList<string> _supportedExtensions;
    private readonly IReadOnlyDictionary<
        string,
        IReadOnlyList<AppPreviewView>> _previewViews;
    private readonly object _fontDiagnosticGate = new();
    private AppDiagnosticView? _fontDiagnostic;
    private string? _fontDiagnosticProduct;
    private AppDiagnosticView? _storageDiagnostic;

    internal AppStatusQuery(
        ProductCatalog catalog,
        Func<CapabilitiesResult> capabilities,
        AppPreferencesStore preferences,
        AppCliGateway cli,
        AppDocumentSession sessions)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _preferences = preferences;
        _cli = cli;
        _sessions = sessions;
        ArgumentNullException.ThrowIfNull(capabilities);
        CapabilitiesResult snapshot = capabilities();
        _experience = snapshot.Products.All(
            static product => product.Engine?.LicenseApplicable is false)
            ? "license-free"
            : "licensed";
        _skills = catalog.Resources.Products
            .Where(static package =>
                package.SkillName is not null
                && package.SkillDescription is not null)
            .Select(static package => new AppSkillView(
                package.ProductId,
                package.SkillName!,
                package.SkillDescription!))
            .ToArray();
        _supportedExtensions =
            catalog.DefaultOwnerExtensions;
        _previewViews = catalog.Products
            .ToDictionary(
                static product => product.Manifest.Id,
                static product =>
                    (IReadOnlyList<AppPreviewView>)product.View.Views
                        .Select(static view => new AppPreviewView(view.Id, view.Label))
                        .ToArray(),
                StringComparer.Ordinal);
        _products = snapshot.Products
            .Select(BuildProductView)
            .ToArray();
    }

    internal AppStatusView Build(string route)
    {
        AppPreferences settings = _preferences.Current;
        AppDocumentSnapshot? session = _sessions.Snapshot;
        ProductDefinition product =
            _catalog.ResolveById(
                session?.ProductId
                ?? _catalog.DefaultProductId());
        LicenseStatusResult license = _cli.LicenseStatus();
        return new AppStatusView(
            VersionInfo.CliVersion,
            DistributionInfo.Edition,
            DistributionInfo.DisplayName,
            _experience,
            route,
            settings.OnboardingCompleted,
            product.Manifest.Id,
            settings.PreviewView(product),
            product.View.Views.Select(static view => view.Id).ToArray(),
            _previewViews[product.Manifest.Id],
            _supportedExtensions,
            _skills,
            settings.RememberRecentFiles,
            license,
            session?.FileName,
            session?.UploadedCopy ?? false,
            session?.PreviewUrl,
            session?.View,
            settings.RecentFiles.Select(RecentView).ToArray(),
            Diagnostics(product, license),
            _products);
    }

    internal void InvalidateFontDiagnostic()
    {
        lock (_fontDiagnosticGate)
        {
            _fontDiagnostic = null;
            _fontDiagnosticProduct = null;
        }
    }

    private IReadOnlyList<AppDiagnosticView> Diagnostics(
        ProductDefinition product, LicenseStatusResult license)
    {
        string platform =
            $"{RuntimeInformation.OSDescription}; {RuntimeInformation.ProcessArchitecture}";
        var diagnostics = new List<AppDiagnosticView>
        {
            new(
                "CLI",
                "ok",
                $"Aspose CLI {VersionInfo.CliVersion}"),
            new(
                "Runtime",
                "ok",
                RuntimeInformation.FrameworkDescription),
            new("Platform", "ok", platform),
        };
        diagnostics.AddRange(license.Products.Select(product =>
            new AppDiagnosticView(
                $"License ({product.Name})",
                !product.Applicable
                    ? "ok"
                    : product.Problem is not null
                    ? "fail"
                    : product.Mode == LicenseModes.Licensed
                        ? "ok"
                        : "warn",
                !product.Applicable
                    ? "not applicable"
                    : product.Mode == LicenseModes.Licensed
                    ? $"licensed ({product.Source})"
                    : "evaluation mode",
                product.Hint)));
        diagnostics.Add(FontDiagnostic(product));
        diagnostics.Add(
            _storageDiagnostic ??= WritableConfigCheck());
        return diagnostics;
    }

    private AppDiagnosticView FontDiagnostic(ProductDefinition product)
    {
        lock (_fontDiagnosticGate)
        {
            if (!product.Manifest.Engine.SupportsFontDiagnostics)
            {
                return new AppDiagnosticView(
                    "Fonts",
                    "warn",
                    $"{product.Manifest.Id}: font diagnostics are not available",
                    "Verify font availability and substitution on the target system when visual fidelity matters.");
            }
            if (_fontDiagnostic is not null
                && string.Equals(
                    _fontDiagnosticProduct,
                    product.Manifest.Id,
                    StringComparison.Ordinal))
            {
                return _fontDiagnostic;
            }

            try
            {
                FontListResult fonts = _cli.Fonts(product.Manifest.Id);
                string fallback =
                    fonts.DefaultFont ?? "engine default";
                _fontDiagnostic = new AppDiagnosticView(
                    "Fonts",
                    "ok",
                    $"{product.Manifest.Id}: {fonts.Sources.Count} source(s); fallback {fallback}");
            }
            catch (Exception)
            {
                _fontDiagnostic = new AppDiagnosticView(
                    "Fonts",
                    "warn",
                    $"{product.Manifest.Id}: font discovery is unavailable",
                    "Repair the license configuration, then reopen Settings.");
            }

            _fontDiagnosticProduct = product.Manifest.Id;
            return _fontDiagnostic;
        }
    }

    private static AppDiagnosticView WritableConfigCheck()
    {
        string directory = AppPaths.ConfigDirectory;
        string probe = Path.Combine(
            directory,
            $".{Guid.NewGuid():N}.probe");
        try
        {
            using (FileStream stream =
                   Aspose.Cli.Sdk.IO.PrivateUserStorage.CreateFile(probe))
            using (var writer = new StreamWriter(stream, leaveOpen: true))
            {
                writer.Write("ok");
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            File.Delete(probe);
            return new AppDiagnosticView(
                "Local storage",
                "ok",
                "configuration directory is writable");
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return new AppDiagnosticView(
                "Local storage",
                "fail",
                "configuration directory is not writable",
                "Check this user's application-data permissions.");
        }
    }

    private AppProductView BuildProductView(
        ProductCapabilities capabilities)
    {
        ProductDefinition product =
            _catalog.ResolveById(capabilities.Id);
        AppSkillView? skill = _skills.SingleOrDefault(candidate =>
            string.Equals(
                candidate.Product,
                capabilities.Id,
                StringComparison.Ordinal));
        string fidelity = capabilities.RenderFormats.Count > 0
            ? "rendered"
            : "semantic";
        ProductPreviewCapabilities preview = capabilities.Preview
            ?? throw new InvalidOperationException(
                $"Product '{capabilities.Id}' has no preview capabilities.");
        ProductReviewCapabilities review = capabilities.Review
            ?? throw new InvalidOperationException(
                $"Product '{capabilities.Id}' has no review capabilities.");
        return new AppProductView(
            capabilities.Id,
            product.Manifest.DisplayName,
            capabilities.Formats
                .Select(static format => new AppFormatView(
                    format.Id,
                    format.Extensions,
                    format.Uses))
                .ToArray(),
            capabilities.Verbs,
            new AppPreviewCapabilityView(
                fidelity,
                preview.DefaultView,
                Views(product, preview.Views)),
            new AppReviewCapabilityView(
                fidelity,
                review.DefaultView,
                Views(product, review.Views),
                review.VisualInspectionRequired),
            skill is null
                ? null
                : new AppSkillSummary(
                    skill.Name,
                    skill.Description));
    }

    private AppRecentView RecentView(AppRecentFile file)
    {
        if (file.ProductId is null
            || !_catalog.TryGet(
                file.ProductId,
                out ProductDefinition? resolved)
            || resolved is null)
        {
            return new AppRecentView(
                file.Id,
                file.Name,
                null,
                null,
                null);
        }

        ProductDefinition product = resolved;
        string? view = file.View is not null
            && product.View.Views.Any(candidate => candidate.Id == file.View)
            ? file.View
            : null;
        return new AppRecentView(
            file.Id,
            file.Name,
            product.Manifest.Id,
            product.Manifest.DisplayName,
            view);
    }

    private static IReadOnlyList<AppPreviewView> Views(
        ProductDefinition product,
        IReadOnlyList<string> ids)
    {
        IReadOnlyDictionary<string, string> labels = product.View.Views
            .ToDictionary(
                static view => view.Id,
                static view => view.Label,
                StringComparer.Ordinal);
        return ids.Select(id => new AppPreviewView(
                id,
                labels.TryGetValue(id, out string? label)
                    ? label
                    : id))
            .ToArray();
    }
}
