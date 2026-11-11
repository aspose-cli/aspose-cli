using System.Globalization;

#pragma warning disable CA1050 // The runtime finds a startup hook only as StartupHook in no namespace.

/// <summary>
/// The startup hook (<c>DOTNET_STARTUP_HOOKS</c>) of this test assembly, which starts an
/// <c>aspose-cli</c> process under German regional settings, as on a machine set to them, so a
/// test runs the real CLI under a culture that writes a decimal comma
/// (<see cref="Aspose.Cli.Product.Cells.Tests.CellsTextValuesAsWrittenTests"/>).
/// </summary>
public static class StartupHook
{
    public static void Initialize() =>
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
}

#pragma warning restore CA1050
