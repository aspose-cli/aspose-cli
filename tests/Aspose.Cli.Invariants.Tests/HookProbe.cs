using System.Globalization;

#pragma warning disable CA1050 // The runtime finds a startup hook only as StartupHook in no namespace.

/// <summary>
/// A startup hook like the culture hook of <c>scripts/test.ps1 -CiLike</c>, for
/// <see cref="Aspose.Cli.Invariants.Tests.CliWorkerTests.AWorker_KeepsTheStartupHooksOfTheRun"/>:
/// in a process started with <see cref="Aspose.Cli.Invariants.Tests.HookProbe.FileVariable"/> it sets
/// <see cref="Aspose.Cli.Invariants.Tests.HookProbe.Culture"/> and records, in that file, the
/// culture, the default thread culture and the startup hooks variable each time a CLI product
/// assembly loads or a command raises a CLI error.
/// </summary>
public static class StartupHook
{
    public static void Initialize()
    {
        if (Environment.GetEnvironmentVariable(Aspose.Cli.Invariants.Tests.HookProbe.FileVariable) is not { } file)
        {
            return;
        }
        CultureInfo culture = CultureInfo.GetCultureInfo(Aspose.Cli.Invariants.Tests.HookProbe.Culture);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        File.WriteAllText(file, string.Empty);
        var gate = new object();
        void Record(string what)
        {
            lock (gate)
            {
                File.AppendAllText(file, $"{what} {CultureInfo.CurrentCulture.Name} {CultureInfo.DefaultThreadCurrentCulture?.Name} "
                    + Environment.GetEnvironmentVariable("DOTNET_STARTUP_HOOKS") + Environment.NewLine);
            }
        }
        AppDomain.CurrentDomain.AssemblyLoad += (_, loaded) =>
        {
            if (loaded.LoadedAssembly.GetName().Name?.StartsWith("Aspose.Cli.Product.", StringComparison.Ordinal) == true)
            {
                Record("load");
            }
        };
        AppDomain.CurrentDomain.FirstChanceException += (_, raised) =>
        {
            if (raised.Exception.GetType().Name == "CliException")
            {
                Record("error");
            }
        };
    }
}

#pragma warning restore CA1050

namespace Aspose.Cli.Invariants.Tests
{
    /// <summary>The settings of the startup hook this test assembly is (<c>StartupHook</c>).</summary>
    public static class HookProbe
    {
        /// <summary>The file the hook records into; a process without it runs no probe.</summary>
        public const string FileVariable = "ASPOSE_CLI_TEST_HOOK_PROBE";

        /// <summary>The culture the hook sets.</summary>
        public const string Culture = "fr-FR";
    }
}
