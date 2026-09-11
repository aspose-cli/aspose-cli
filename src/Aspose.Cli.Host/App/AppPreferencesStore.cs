using System.Security.Cryptography;
using System.Collections.Frozen;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aspose.Cli.Host.Preview;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Host.App;

internal sealed record AppRecentFile(
    string Id,
    string Path,
    string Name,
    string? ProductId,
    string? View);

internal sealed record AppPreferences
{
    public bool OnboardingCompleted { get; init; }

    public IReadOnlyDictionary<string, string> PreviewViews { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public bool RememberRecentFiles { get; init; } = true;

    public IReadOnlyList<AppRecentFile> RecentFiles { get; init; } = [];

    public string PreviewView(ProductDefinition product)
    {
        ProductPreviewDefinition preview = product.Preview;
        if (PreviewViews.TryGetValue(product.Manifest.Id, out string? view)
            && preview.Views.Contains(view, StringComparer.Ordinal))
        {
            return view;
        }

        return preview.DefaultView;
    }
}

/// <summary>Thread-safe, atomic storage for non-secret Web App preferences.</summary>
internal sealed class AppPreferencesStore
{
    private const int MaxRecentFiles = 8;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    private readonly object _gate = new();
    private readonly string _path;
    private AppPreferences _current;

    public AppPreferencesStore(ProductCatalog catalog, string path)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _path = Path.GetFullPath(path);
        PrivateUserStorage.EnsureDirectory(Path.GetDirectoryName(_path)!);
        _current = Freeze(Load(_path, catalog));
    }

    public AppPreferences Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public AppPreferences CompleteOnboarding()
    {
        lock (_gate)
        {
            return Commit(_current with { OnboardingCompleted = true });
        }
    }

    public AppPreferences Update(
        string productId,
        string previewView,
        bool rememberRecentFiles)
    {
        lock (_gate)
        {
            var views = new Dictionary<string, string>(_current.PreviewViews, StringComparer.Ordinal)
            {
                [productId] = previewView,
            };
            return Commit(_current with
            {
                PreviewViews = views,
                RememberRecentFiles = rememberRecentFiles,
                RecentFiles = rememberRecentFiles ? _current.RecentFiles : [],
            });
        }
    }

    public void RecordRecent(
        string filePath,
        string productId,
        string view)
    {
        string full = Path.GetFullPath(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(productId);
        ArgumentException.ThrowIfNullOrWhiteSpace(view);
        lock (_gate)
        {
            if (!_current.RememberRecentFiles)
            {
                return;
            }

            string id = IdForPath(full);
            var next = new List<AppRecentFile>
            {
                new(
                    id,
                    full,
                    Path.GetFileName(full),
                    productId,
                    view),
            };
            next.AddRange(_current.RecentFiles.Where(item =>
                !string.Equals(item.Id, id, StringComparison.Ordinal)));
            Commit(_current with { RecentFiles = next.Take(MaxRecentFiles).ToArray() });
        }
    }

    public string? ResolveRecent(string id)
    {
        lock (_gate)
        {
            return _current.RecentFiles.FirstOrDefault(item =>
                string.Equals(item.Id, id, StringComparison.Ordinal))?.Path;
        }
    }

    public void RemoveRecent(string id)
    {
        lock (_gate)
        {
            Commit(_current with
            {
                RecentFiles = _current.RecentFiles
                    .Where(item => !string.Equals(item.Id, id, StringComparison.Ordinal))
                    .ToArray(),
            });
        }
    }

    public void ClearRecent()
    {
        lock (_gate)
        {
            Commit(_current with { RecentFiles = [] });
        }
    }

    private static AppPreferences Load(string path, ProductCatalog catalog)
    {
        try
        {
            if (!File.Exists(path))
            {
                return new AppPreferences();
            }

            AppPreferences loaded = JsonSerializer.Deserialize<AppPreferences>(
                PrivateUserStorage.ReadAllText(path),
                JsonOptions) ?? new AppPreferences();
            return Normalize(loaded, catalog);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new AppPreferences();
        }
    }

    private static AppPreferences Normalize(
        AppPreferences loaded,
        ProductCatalog catalog)
    {
        IReadOnlyDictionary<string, string> storedViews = loaded.PreviewViews
            ?? new Dictionary<string, string>(StringComparer.Ordinal);
        var views = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (ProductDefinition product in catalog.Products)
        {
            if (storedViews.TryGetValue(product.Manifest.Id, out string? view)
                && product.Preview.Views.Contains(view, StringComparer.Ordinal))
            {
                views[product.Manifest.Id] = view;
            }
        }

        return loaded with
        {
            PreviewViews = views,
            RecentFiles = loaded.RecentFiles ?? [],
        };
    }

    private AppPreferences Commit(AppPreferences candidate)
    {
        AppPreferences snapshot = Freeze(candidate);
        PrivateUserStorage.WriteAllText(_path, JsonSerializer.Serialize(snapshot, JsonOptions));
        _current = snapshot;
        return snapshot;
    }

    private static AppPreferences Freeze(AppPreferences value) => value with
    {
        PreviewViews = value.PreviewViews.ToFrozenDictionary(StringComparer.Ordinal),
        RecentFiles = Array.AsReadOnly(value.RecentFiles.ToArray()),
    };

    private static string IdForPath(string path)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(path));
        return Convert.ToHexString(hash.AsSpan(0, 8)).ToLowerInvariant();
    }
}
