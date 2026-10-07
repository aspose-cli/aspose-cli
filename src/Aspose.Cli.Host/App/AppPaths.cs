using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Host.App;

/// <summary>Filesystem locations owned by the local browser application.</summary>
internal static class AppPaths
{
    public static string ConfigDirectory =>
        Aspose.Cli.Sdk.Configuration.ConfigurationPaths.EnsureUserDirectory();

    public static string Preferences => Path.Combine(ConfigDirectory, "app-settings.json");

    public static string Log => Path.Combine(ConfigDirectory, "app.log");

    public static string SessionRoot(int pid) =>
        UserStorage.CreateTemporaryDirectory(
            "app",
            pid.ToString(System.Globalization.CultureInfo.InvariantCulture));
}
