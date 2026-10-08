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

    /// <summary>Formats a length in points with at most two decimals, such as <c>595.28</c>.</summary>
    public static string Points(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>Formats a Boolean as stable human-readable text.</summary>
    public static string YesNo(bool value) => value ? "yes" : "no";
}
