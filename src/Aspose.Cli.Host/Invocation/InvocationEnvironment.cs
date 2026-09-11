using System.Diagnostics;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;

namespace Aspose.Cli.Host.Invocation;

/// <summary>Explicit child-process environment policy shared by MCP and supervised commands.</summary>
internal static class InvocationEnvironment
{
    private static readonly string[] Baseline =
    [
        "SystemRoot", "WINDIR", "TEMP", "TMP", "PATH", "PATHEXT",
        "USERPROFILE", "HOMEDRIVE", "HOMEPATH", "HOME",
        "LOCALAPPDATA", "APPDATA", "PROGRAMDATA", "LANG", "LC_ALL", "LC_CTYPE",
        LicenseResolver.ConfigDirectoryEnvName, LicenseResolver.EnvPathName, LicenseResolver.EnvBase64Name,
    ];

    internal static IReadOnlyList<string> ProductVariables(ProductCatalog catalog) =>
        catalog.Products.SelectMany(static product => new[]
            {
                LicenseResolver.ProductEnvPathName(product.Manifest.Id),
                LicenseResolver.ProductEnvBase64Name(product.Manifest.Id),
            }.Concat(product.Manifest.ResourceBudgets
                .Select(static budget => budget.EnvironmentVariable)
                .OfType<string>()))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    internal static IReadOnlyList<string> ReferencedVariables(ParsedInvocation invocation) =>
        invocation.ParseResult.DeclaredParameters()
            .Where(static parameter => parameter.Metadata.ValueSource == ParameterValueSource.EnvironmentVariableName)
            .SelectMany(static parameter => parameter.TextValues())
            .Where(IsSafeName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal).ToArray();

    internal static void Configure(
        ProcessStartInfo start, ParsedInvocation invocation, IReadOnlyList<string> productVariables)
    {
        start.Environment.Clear();
        foreach (string name in Baseline.Concat(productVariables).Concat(ReferencedVariables(invocation)))
        {
            if (Environment.GetEnvironmentVariable(name) is { } value)
            {
                start.Environment[name] = value;
            }
        }
        if (invocation.GlobalValues is { } globals)
        {
            if (globals.LicensePath is { } licensePath)
            {
                start.Environment[LicenseResolver.EnvPathName] = licensePath;
            }
            start.Environment[InputSizeGuard.BudgetVariable] =
                globals.MaxInputBytes.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    private static bool IsSafeName(string name) =>
        name.Length is > 0 and <= 128
        && (char.IsAsciiLetter(name[0]) || name[0] == '_')
        && name.AsSpan(1).IndexOfAnyExcept(
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_") < 0;
}
