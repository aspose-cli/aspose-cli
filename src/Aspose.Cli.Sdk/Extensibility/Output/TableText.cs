using System.Globalization;

namespace Aspose.Cli.Sdk.Extensibility.Output;

/// <summary>
/// Shared value-to-text primitives for table renderers. Invariant culture keeps
/// human output as deterministic as the JSON contract.
/// </summary>
public static class TableText
{
    /// <summary>Formats a byte count using invariant grouping.</summary>
    public static string Bytes(long bytes) => string.Create(CultureInfo.InvariantCulture, $"{bytes:N0} bytes");

    /// <summary>Formats an integer using invariant culture.</summary>
    public static string Int(int value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>Formats a Boolean as stable human-readable text.</summary>
    public static string YesNo(bool value) => value ? "yes" : "no";
}
