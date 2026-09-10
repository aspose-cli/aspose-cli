using System.Reflection;

namespace Aspose.Cli.Host.Invocation;

/// <summary>Version of the running CLI, as reported in capabilities output.</summary>
internal static class VersionInfo
{
    private static readonly Assembly Distribution = DistributionAssembly();

    public static string ArtifactVersion { get; } = ComputeArtifactVersion();

    public static string CliVersion { get; } = StripBuildMetadata(ArtifactVersion);

    public static string SourceRevision { get; } =
        Metadata("SourceRevision") ?? "unknown";

    public static bool BuildDirty { get; } =
        bool.TryParse(Metadata("BuildDirty"), out bool dirty) && dirty;

    private static string ComputeArtifactVersion()
    {
        string? informational = Distribution
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrWhiteSpace(informational))
        {
            return "0.0.0";
        }

        return informational;
    }

    private static string StripBuildMetadata(string informational)
    {
        int metadata = informational.IndexOf('+', StringComparison.Ordinal);
        return metadata > 0 ? informational[..metadata] : informational;
    }

    private static string? Metadata(string key) =>
        Distribution.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => string.Equals(
                attribute.Key,
                key,
                StringComparison.Ordinal))
            ?.Value;

    private static Assembly DistributionAssembly()
    {
        Assembly? entry = Assembly.GetEntryAssembly();
        return string.Equals(
                entry?.GetName().Name,
                Aspose.Cli.Sdk.DistributionInfo.CommandName,
                StringComparison.Ordinal)
            ? entry!
            : typeof(CliHost).Assembly;
    }
}
