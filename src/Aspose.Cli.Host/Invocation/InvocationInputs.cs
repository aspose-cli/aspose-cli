using System.Diagnostics;
using System.IO.Pipes;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Host.Output;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Host.Invocation;

/// <summary>Inherited options for this invocation; command arguments retain their own precedence.</summary>
internal sealed class InvocationInputs : IDisposable
{
    internal const string HandleVariable = "ASPOSE_CLI_INVOCATION_INPUT_HANDLE";
    private static readonly AsyncLocal<InvocationInputs?> Ambient = new();
    private readonly InvocationInputs? _previous;
    internal static InvocationInputs? Current => Ambient.Value;
    internal GlobalValues? Inherited { get; }

    private InvocationInputs(InvocationStartMessage message)
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
        _previous = Ambient.Value;
        Ambient.Value = this;
    }

    internal static InvocationInputs? Receive()
    {
        string? handle = Environment.GetEnvironmentVariable(HandleVariable);
        if (string.IsNullOrEmpty(handle)) { return null; }
        Environment.SetEnvironmentVariable(HandleVariable, null);
        using var pipe = new AnonymousPipeClientStream(PipeDirection.In, handle);
        return new InvocationInputs(ProcessPipeMessages.ReadAsync<InvocationStartMessage>(
            pipe, CancellationToken.None).GetAwaiter().GetResult());
    }

    public void Dispose() => Ambient.Value = _previous;
}

/// <summary>Owns a startup pipe until its child has received the inherited options.</summary>
internal sealed class InvocationInputServer : IDisposable
{
    private readonly AnonymousPipeServerStream _pipe = new(
        PipeDirection.Out, HandleInheritability.Inheritable);

    internal void Configure(ProcessStartInfo start) =>
        start.Environment[InvocationInputs.HandleVariable] = _pipe.GetClientHandleAsString();

    internal Task SendAsync(GlobalValues? inherited, CancellationToken cancellationToken)
    {
        _pipe.DisposeLocalCopyOfClientHandle();
        return ProcessPipeMessages.WriteAsync(_pipe, new InvocationStartMessage(
            1, inherited?.WorkDir, inherited?.LicensePath, inherited?.MaxInputBytes), cancellationToken);
    }

    public void Dispose() => _pipe.Dispose();
}

internal sealed record InvocationStartMessage(
    int Version, string? WorkDirectory, string? LicensePath, long? MaxInputBytes);
