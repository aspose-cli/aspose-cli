using System.CommandLine;
using System.Runtime.CompilerServices;
using Aspose.Cli.Sdk.IO.Input;

namespace Aspose.Cli.Sdk.Extensibility.Commanding;

/// <summary>
/// Declares the flag by which a caller opts an invocation into <see cref="NetworkAccess.Web"/>
/// for trusted input. The host reads the declaration and admits the invocation to the
/// <see cref="NetworkGate"/> before the command runs; nothing else opens the gate.
/// </summary>
public static class NetworkOptIn
{
    private static readonly ConditionalWeakTable<Option, object> Declared = [];

    /// <summary>Declares that the flag, when set, lets the invocation's engines request http and https addresses.</summary>
    /// <returns>The same option.</returns>
    public static Option<bool> OptsIntoNetwork(this Option<bool> option)
    {
        ArgumentNullException.ThrowIfNull(option);
        Declared.AddOrUpdate(option, Declared);
        return option;
    }

    /// <summary>Whether the symbol is a flag declared with <see cref="OptsIntoNetwork"/>.</summary>
    public static bool IsNetworkOptIn(this Symbol symbol) =>
        symbol is Option option && Declared.TryGetValue(option, out _);
}
