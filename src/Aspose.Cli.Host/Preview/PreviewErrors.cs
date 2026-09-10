using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Host.Preview;

/// <summary>Stable validation errors for Host-owned preview entry points.</summary>
internal static class PreviewErrors
{
    public static void EnsureViewSupported(
        string product,
        string view,
        IReadOnlyList<string> available)
    {
        if (available.Contains(view, StringComparer.Ordinal))
        {
            return;
        }

        var values = new JsonArray();
        foreach (string item in available)
        {
            values.Add(item);
        }

        throw new CliException(
            ErrorCodes.FeatureUnsupported,
            $"Preview view '{view}' is not supported by the {product} product.",
            hint: $"Use one of: {string.Join(", ", available)}.",
            details: new JsonObject { ["available"] = values });
    }
}
