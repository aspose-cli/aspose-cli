using Aspose.Cli.Sdk;

namespace Aspose.Cli.Host.LocalServices;

/// <summary>
/// Hard resource ceilings shared by the App and Preview local HTTP services. Only the
/// snapshot and upload limits that <c>docs preview</c> and <c>docs app</c> name take an
/// environment override, and only within the compiled safety maximum; the others are fixed.
/// </summary>
internal sealed record LocalServiceResourceLimits(
    int MaximumConcurrentRequests,
    int MaximumSseClients,
    int SseQueueCapacity,
    TimeSpan SseWriteTimeout,
    int MaximumSnapshotFiles,
    long MaximumSnapshotFileBytes,
    long MaximumSnapshotBytes,
    int MaximumUploadFiles,
    long MaximumUploadSessionBytes,
    TimeSpan RenderTimeout)
{
    public static LocalServiceResourceLimits Resolve(
        Func<string, string?>? environment = null)
    {
        environment ??= Environment.GetEnvironmentVariable;
        return new LocalServiceResourceLimits(
            MaximumConcurrentRequests: 32,
            MaximumSseClients: 16,
            SseQueueCapacity: 16,
            SseWriteTimeout: TimeSpan.FromSeconds(5),
            Integer(
                environment,
                DistributionInfo.EnvironmentVariablePrefix + "PREVIEW_MAX_FILES",
                defaultValue: 2_048,
                maximum: 8_192),
            Bytes(
                environment,
                DistributionInfo.EnvironmentVariablePrefix + "PREVIEW_MAX_FILE_BYTES",
                defaultValue: 64L * 1024 * 1024,
                maximum: 256L * 1024 * 1024),
            Bytes(
                environment,
                DistributionInfo.EnvironmentVariablePrefix + "PREVIEW_MAX_SNAPSHOT_BYTES",
                defaultValue: 256L * 1024 * 1024,
                maximum: 1024L * 1024 * 1024),
            Integer(
                environment,
                DistributionInfo.EnvironmentVariablePrefix + "APP_MAX_UPLOAD_FILES",
                defaultValue: 16,
                maximum: 128),
            Bytes(
                environment,
                DistributionInfo.EnvironmentVariablePrefix + "APP_MAX_UPLOAD_SESSION_BYTES",
                defaultValue: 512L * 1024 * 1024,
                maximum: 2L * 1024 * 1024 * 1024),
            RenderTimeout: TimeSpan.FromMinutes(1));
    }

    private static int Integer(
        Func<string, string?> environment,
        string name,
        int defaultValue,
        int maximum)
    {
        string? raw = environment(name);
        if (raw is null)
        {
            return defaultValue;
        }

        if (!int.TryParse(
                raw,
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out int value)
            || value < 1
            || value > maximum)
        {
            throw new InvalidOperationException(
                $"{name} must be between 1 and {maximum}.");
        }

        return value;
    }

    private static long Bytes(
        Func<string, string?> environment,
        string name,
        long defaultValue,
        long maximum)
    {
        string? raw = environment(name);
        if (raw is null)
        {
            return defaultValue;
        }

        if (!long.TryParse(
                raw,
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out long value)
            || value <= 0
            || value > maximum)
        {
            throw new InvalidOperationException(
                $"{name} must be between 1 and {maximum}.");
        }

        return value;
    }
}
