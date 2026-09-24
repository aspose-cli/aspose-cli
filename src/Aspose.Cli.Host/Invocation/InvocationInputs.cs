using System.IO.Pipes;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Host.Output;
using Aspose.Cli.Sdk;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Host.Invocation;

/// <summary>Inherited options and named environment access for one owned child invocation.</summary>
internal sealed class InvocationInputs : IDisposable
{
    internal const string HandleVariable = DistributionInfo.EnvironmentVariablePrefix + "INVOCATION_INPUT_HANDLE";
    internal const string RequestHandleVariable = DistributionInfo.EnvironmentVariablePrefix + "INVOCATION_REQUEST_HANDLE";
    internal const int MaximumMessageBytes = 256 * 1024;
    private static readonly AsyncLocal<InvocationInputs?> Ambient = new();
    private readonly InvocationInputs? _previous;
    private readonly AnonymousPipeClientStream _replies;
    private readonly AnonymousPipeClientStream _requests;
    private readonly SemaphoreSlim _gate = new(1, 1);
    internal static InvocationInputs? Current => Ambient.Value;
    internal GlobalValues? Inherited { get; }

    private InvocationInputs(InvocationStartMessage message,
        AnonymousPipeClientStream replies, AnonymousPipeClientStream requests)
    {
        if (message.Version != 1
            || (message.WorkDirectory is not null && !Path.IsPathFullyQualified(message.WorkDirectory))
            || (message.LicensePath is not null && !Path.IsPathFullyQualified(message.LicensePath))
            || message.MaxInputBytes is <= 0 or > InputSizeGuard.MaximumBytes
            || (message.WorkDirectory is null && (message.LicensePath is not null || message.MaxInputBytes is not null)))
        {
            throw new InvalidDataException("The inherited invocation values are invalid.");
        }
        Inherited = message.WorkDirectory is null ? null : new GlobalValues(
            OutputMode.Json, false, false, message.LicensePath, message.WorkDirectory, null,
            message.MaxInputBytes ?? InputSizeGuard.DefaultMaxBytes);
        _replies = replies;
        _requests = requests;
        _previous = Ambient.Value;
        Ambient.Value = this;
    }

    internal static InvocationInputs? Receive()
    {
        string? handle = Environment.GetEnvironmentVariable(HandleVariable);
        string? requestHandle = Environment.GetEnvironmentVariable(RequestHandleVariable);
        if (string.IsNullOrEmpty(handle) && string.IsNullOrEmpty(requestHandle)) { return null; }
        Environment.SetEnvironmentVariable(HandleVariable, null);
        Environment.SetEnvironmentVariable(RequestHandleVariable, null);
        if (string.IsNullOrEmpty(handle) || string.IsNullOrEmpty(requestHandle))
        {
            throw new InvalidDataException("The invocation input channel is incomplete.");
        }
        var replies = new AnonymousPipeClientStream(PipeDirection.In, handle);
        AnonymousPipeClientStream? requests = null;
        try
        {
            requests = new AnonymousPipeClientStream(PipeDirection.Out, requestHandle);
            ProcessPipeHandles.PreventInheritance(replies);
            ProcessPipeHandles.PreventInheritance(requests);
            InvocationStartMessage message = ProcessPipeMessages.ReadAsync<InvocationStartMessage>(
                replies, CancellationToken.None, MaximumMessageBytes).GetAwaiter().GetResult();
            return new InvocationInputs(message, replies, requests);
        }
        catch
        {
            replies.Dispose();
            requests?.Dispose();
            throw;
        }
    }

    internal EnvironmentValueReply ReadEnvironment(
        string name, long maximumCharacters, CancellationToken cancellationToken, Action<int>? reserve = null)
    {
        _gate.Wait(cancellationToken);
        try
        {
            ProcessPipeMessages.WriteAsync(_requests,
                new EnvironmentValueRequest(name, maximumCharacters), cancellationToken, MaximumMessageBytes)
                .WaitAsync(cancellationToken).GetAwaiter().GetResult();
            return ProcessPipeMessages.ReadAsync<EnvironmentValueReply>(
                _replies, cancellationToken, MaximumMessageBytes, reserve)
                .WaitAsync(cancellationToken).GetAwaiter().GetResult();
        }
        finally { _gate.Release(); }
    }

    public void Dispose()
    {
        Ambient.Value = _previous;
        _requests.Dispose();
        _replies.Dispose();
        _gate.Dispose();
    }
}

internal sealed record InvocationStartMessage(
    int Version, string? WorkDirectory, string? LicensePath, long? MaxInputBytes);
internal sealed record EnvironmentValueRequest(string Name, long MaximumCharacters);
internal sealed record EnvironmentValueReply(string? Value, long? OversizedCharacters = null);
