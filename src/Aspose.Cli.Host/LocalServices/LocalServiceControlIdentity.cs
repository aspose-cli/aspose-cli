namespace Aspose.Cli.Host.LocalServices;

/// <summary>Validates and stamps the frozen local-service control identity.</summary>
internal sealed class LocalServiceControlIdentity(
    LocalServiceControlEndpoint endpoint,
    string nonce,
    string token)
{
    public bool IsAuthorized(LocalServiceControlRequest? request) =>
        request is not null
        && request.Version
            == LocalServiceControlCodec.ProtocolVersion
        && string.Equals(
            request.Service,
            endpoint.Service,
            StringComparison.Ordinal)
        && string.Equals(
            request.InstanceId,
            endpoint.InstanceId,
            StringComparison.Ordinal)
        && SecretText.FixedEquals(
            request.Nonce,
            nonce)
        && SecretText.FixedEquals(
            request.Token,
            token)
        && request.RequestId.Length is > 0 and <= 64
        && request.Command.Length is > 0 and <= 64;

    public LocalServiceControlResponse Response(
        LocalServiceControlRequest? request,
        bool ok,
        string? message = null) =>
        new(
            LocalServiceControlCodec.ProtocolVersion,
            request?.RequestId ?? string.Empty,
            endpoint.Service,
            endpoint.InstanceId,
            nonce,
            ok,
            message);

    public LocalServiceControlResponse Normalize(
        LocalServiceControlRequest request,
        LocalServiceControlResponse response) =>
        response with
        {
            Version = LocalServiceControlCodec.ProtocolVersion,
            RequestId = request.RequestId,
            Service = endpoint.Service,
            InstanceId = endpoint.InstanceId,
            Nonce = nonce,
        };

    public static void ValidateResponse(
        LocalServiceControlEndpoint endpoint,
        string expectedNonce,
        string requestId,
        LocalServiceControlResponse response)
    {
        if (response.Version
                != LocalServiceControlCodec.ProtocolVersion
            || !string.Equals(
                response.RequestId,
                requestId,
                StringComparison.Ordinal)
            || !string.Equals(
                response.Service,
                endpoint.Service,
                StringComparison.Ordinal)
            || !string.Equals(
                response.InstanceId,
                endpoint.InstanceId,
                StringComparison.Ordinal)
            || !SecretText.FixedEquals(
                response.Nonce,
                expectedNonce))
        {
            throw new InvalidDataException(
                "The local service returned a mismatched control identity.");
        }
    }
}
