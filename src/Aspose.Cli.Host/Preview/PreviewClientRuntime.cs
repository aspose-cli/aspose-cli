using System.Reflection;

using Aspose.Cli.Sdk.Preview;

namespace Aspose.Cli.Host.Preview;

internal static class PreviewClientRuntime
{
    private const string ResourceName = "preview/live-runtime.js";
    private static readonly Lazy<string> _script = new(ReadScript);

    public static string Script => _script.Value;

    private static string ReadScript()
    {
        using Stream stream = typeof(PreviewClientRuntime).Assembly
            .GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"Missing embedded preview runtime '{ResourceName}'.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
