namespace Aspose.Cli.Host.LocalServices;

/// <summary>
/// Hard resource ceilings shared by the App and Preview local HTTP services.
/// Environment overrides may lower or raise defaults only within the
/// compiled safety maximum.
/// </summary>
internal sealed record LocalServiceResourceLimits(
    int MaximumConcurrentRequests,
    int MaximumSseClients,
    int SseQueueCapacity,
    TimeSpan SseWriteTimeout,
    int MaximumSnapshotFiles,
    long MaximumSnapshotFileBytes,
    long MaximumSnapshotBytes,
    long MaximumInlineHtmlBytes,
    int MaximumUploadFiles,
    long MaximumUploadSessionBytes)
{
    public static LocalServiceResourceLimits Resolve(
        Func<string, string?>? environment = null)
    {
        environment ??= Environment.GetEnvironmentVariable;
        return new LocalServiceResourceLimits(
            Integer(
                environment,
                "ASPOSE_CLI_LOCAL_HTTP_MAX_REQUESTS",
                defaultValue: 32,
                maximum: 128),
            Integer(
                environment,
                "ASPOSE_CLI_LOCAL_SSE_MAX_CLIENTS",
                defaultValue: 16,
                maximum: 64),
            Integer(
                environment,
                "ASPOSE_CLI_LOCAL_SSE_QUEUE_CAPACITY",
                defaultValue: 16,
                maximum: 128),
            TimeSpan.FromMilliseconds(Integer(
                environment,
                "ASPOSE_CLI_LOCAL_SSE_WRITE_TIMEOUT_MS",
                defaultValue: 5_000,
                maximum: 30_000,
                minimum: 100)),
            Integer(
                environment,
                "ASPOSE_CLI_PREVIEW_MAX_FILES",
                defaultValue: 2_048,
                maximum: 8_192),
            Bytes(
                environment,
                "ASPOSE_CLI_PREVIEW_MAX_FILE_BYTES",
                defaultValue: 64L * 1024 * 1024,
                maximum: 256L * 1024 * 1024),
            Bytes(
                environment,
                "ASPOSE_CLI_PREVIEW_MAX_SNAPSHOT_BYTES",
                defaultValue: 256L * 1024 * 1024,
                maximum: 1024L * 1024 * 1024),
            Bytes(
                environment,
                "ASPOSE_CLI_PREVIEW_MAX_INLINE_HTML_BYTES",
                defaultValue: 8L * 1024 * 1024,
                maximum: 32L * 1024 * 1024),
            Integer(
                environment,
                "ASPOSE_CLI_APP_MAX_UPLOAD_FILES",
                defaultValue: 16,
                maximum: 128),
            Bytes(
                environment,
                "ASPOSE_CLI_APP_MAX_UPLOAD_SESSION_BYTES",
                defaultValue: 512L * 1024 * 1024,
                maximum: 2L * 1024 * 1024 * 1024));
    }

    private static int Integer(
        Func<string, string?> environment,
        string name,
        int defaultValue,
        int maximum,
        int minimum = 1)
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
            || value < minimum
            || value > maximum)
        {
            throw new InvalidOperationException(
                $"{name} must be between {minimum} and {maximum}.");
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
