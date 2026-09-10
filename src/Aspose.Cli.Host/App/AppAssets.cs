namespace Aspose.Cli.Host.App;

/// <summary>Reads the browser application assets embedded in the executable.</summary>
internal static class AppAssets
{
    private static readonly Lazy<string> HtmlAsset = new(() => Read("app/app.html"));
    private static readonly Lazy<string> CssAsset = new(() => Read("app/app.css"));
    private static readonly Lazy<string> JavaScriptAsset = new(() => Read("app/app.js"));

    public static string Html => HtmlAsset.Value;

    public static string Css => CssAsset.Value;

    public static string JavaScript => JavaScriptAsset.Value;

    private static string Read(string name)
    {
        using Stream stream = typeof(AppAssets).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded resource '{name}' is missing from the build.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
