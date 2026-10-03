using System.Diagnostics;
using System.IO.Pipes;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Host.Invocation;

/// <summary>Supplies inherited options and only the environment values requested by its own child.</summary>
internal sealed class InvocationInputServer : IDisposable
{
    private readonly AnonymousPipeServerStream _replies = new(
        PipeDirection.Out, HandleInheritability.Inheritable);
    private readonly AnonymousPipeServerStream _requests;
    private readonly InvocationInputs? _upstream = InvocationInputs.Current;

    internal InvocationInputServer()
    {
        try { _requests = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable); }
        catch { _replies.Dispose(); throw; }
    }

    internal void Configure(ProcessStartInfo start)
    {
        start.Environment[InvocationInputs.HandleVariable] = _replies.GetClientHandleAsString();
        start.Environment[InvocationInputs.RequestHandleVariable] = _requests.GetClientHandleAsString();
    }

    internal async Task ServeAsync(GlobalValues? inherited, CancellationToken cancellationToken)
    {
        _replies.DisposeLocalCopyOfClientHandle();
        _requests.DisposeLocalCopyOfClientHandle();
        await ProcessPipeMessages.WriteAsync(_replies, new InvocationStartMessage(
            1, inherited?.WorkDir, inherited?.LicensePath, inherited?.MaxInputBytes,
            inherited?.EvaluationRequested == true),
            cancellationToken, InvocationInputs.MaximumMessageBytes).ConfigureAwait(false);
        while (true)
        {
            EnvironmentValueRequest? request = await ProcessPipeMessages.ReadOrEndAsync<EnvironmentValueRequest>(
                _requests, cancellationToken, InvocationInputs.MaximumMessageBytes).ConfigureAwait(false);
            if (request is null) { return; }
            if (string.IsNullOrEmpty(request.Name) || request.Name.IndexOfAny(['\0', '=']) >= 0
                || request.MaximumCharacters is < 0 or > ResourceBudgetDefaults.MaximumSecretCharacters)
            {
                throw new InvalidDataException("The environment value request is invalid.");
            }
            EnvironmentValueReply reply;
            if (_upstream is not null)
            {
                reply = _upstream.ReadEnvironment(request.Name, request.MaximumCharacters, cancellationToken);
            }
            else
            {
                string? value = Environment.GetEnvironmentVariable(request.Name);
                reply = value?.Length > request.MaximumCharacters
                    ? new EnvironmentValueReply(null, value.Length)
                    : new EnvironmentValueReply(value);
            }
            await ProcessPipeMessages.WriteAsync(_replies, reply,
                cancellationToken, InvocationInputs.MaximumMessageBytes).ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        _requests.Dispose();
        _replies.Dispose();
    }
}
