namespace Aspose.Cli.Host.LocalServices;

/// <summary>Reads and writes local-service control frames, in the Host's one process frame format.</summary>
internal static class LocalServiceControlCodec
{
    internal const int ProtocolVersion = 1;
    internal const int MaximumPayloadBytes = ProcessFrames.MaximumBytes;
    internal static readonly TimeSpan DefaultStageTimeout =
        TimeSpan.FromSeconds(5);

    public static Task WriteAsync(
        Stream stream,
        LocalServiceControlRequest request,
        CancellationToken cancellationToken) =>
        ProcessFrames.WriteAsync(
            stream,
            request,
            LocalServiceJsonContext.Default.LocalServiceControlRequest,
            cancellationToken,
            MaximumPayloadBytes);

    public static Task WriteAsync(
        Stream stream,
        LocalServiceControlResponse response,
        CancellationToken cancellationToken) =>
        ProcessFrames.WriteAsync(
            stream,
            response,
            LocalServiceJsonContext.Default.LocalServiceControlResponse,
            cancellationToken,
            MaximumPayloadBytes);

    public static Task<LocalServiceControlRequest> ReadRequestAsync(
        Stream stream,
        CancellationToken cancellationToken) =>
        ProcessFrames.ReadAsync(
            stream,
            LocalServiceJsonContext.Default.LocalServiceControlRequest,
            cancellationToken,
            MaximumPayloadBytes);

    public static Task<LocalServiceControlResponse> ReadResponseAsync(
        Stream stream,
        CancellationToken cancellationToken) =>
        ProcessFrames.ReadAsync(
            stream,
            LocalServiceJsonContext.Default.LocalServiceControlResponse,
            cancellationToken,
            MaximumPayloadBytes);
}
