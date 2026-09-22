using System.Buffers.Binary;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Execution;
using Aspose.Cli.Sdk.Serialization;
using Xunit;

namespace Aspose.Cli.Host.Tests;

[Collection("Local service lifecycle")]
public sealed class LocalServiceRuntimeTests
{
    [Fact]
    public void ControlProtocol_StartFailsWhenItsWindowsPipeIsAlreadyOwned()
    {
        if (!OperatingSystem.IsWindows()) { return; }
        LocalServiceControlEndpoint endpoint = Endpoint();
        using var occupied = new NamedPipeServerStream(endpoint.PipeName, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        using var server = new LocalServiceControlServer(endpoint, Guid.NewGuid().ToString("N"),
            Guid.NewGuid().ToString("N"), (_, _) => throw new InvalidOperationException("No request should arrive."),
            stageTimeout: TimeSpan.FromMilliseconds(250));
        Assert.Throws<IOException>(() => server.Start());
    }

    [Fact]
    public void ControlProtocol_ContainsHandlerAndDiagnosticFailures()
    {
        LocalServiceControlEndpoint endpoint = Endpoint();
        string nonce = Guid.NewGuid().ToString("N");
        string token = Guid.NewGuid().ToString("N");
        using var server = new LocalServiceControlServer(endpoint, nonce, token,
            (_, _) => throw new InvalidOperationException("Private operation detail."),
            describeFailure: _ => throw new InvalidOperationException("Private diagnostic detail."));
        server.Start();
        LocalServiceControlResponse failure = LocalServiceControlServer.Send(endpoint, nonce, token, "open");
        Assert.False(failure.Ok);
        Assert.DoesNotContain("Private", failure.Message!, StringComparison.Ordinal);
        Assert.True(LocalServiceControlServer.Send(endpoint, nonce, token, "ping").Ok);
    }

    [Fact]
    public void ChildError_RestoresTheStructuredEnvelopeAndExitCategory()
    {
        string json = JsonSerializer.Serialize(
            new ErrorEnvelope
            {
                Error = new ErrorPayload
                {
                    Code = "FILE_CORRUPT",
                    Message = "The input package is corrupt.",
                    Details = new JsonObject { ["part"] = "xref" },
                    Hint = "Use an intact input.",
                    Docs = "pdf/verification",
                },
            },
            SdkJsonContext.Default.ErrorEnvelope);

        CliException error = Assert.IsType<CliException>(
            LocalServiceChildError.TryRead(json, (int)ExitCode.InputError));

        Assert.Equal("FILE_CORRUPT", error.Code.Name);
        Assert.Equal(ExitCode.InputError, error.ExitCode);
        Assert.Equal("The input package is corrupt.", error.Message);
        Assert.Equal("xref", error.Details!["part"]!.GetValue<string>());
        Assert.Equal("Use an intact input.", error.Hint);
        Assert.Equal("pdf/verification", error.Docs);
    }

    [Theory]
    [InlineData("", 3)]
    [InlineData("diagnostic\n{}", 3)]
    [InlineData("{}", 3)]
    [InlineData("{\"schema\":\"wrong\",\"schemaVersion\":1,\"error\":{\"code\":\"X\",\"message\":\"x\"}}", 3)]
    [InlineData("{\"schema\":\"https://schemas.aspose.dev/aspose-cli/v2/common/error.schema.json\",\"schemaVersion\":1,\"error\":{\"code\":\"X\",\"message\":\"x\"}}", 0)]
    [InlineData("{\"schema\":\"https://schemas.aspose.dev/aspose-cli/v2/common/error.schema.json\",\"schemaVersion\":1,\"error\":{\"code\":\"X\",\"message\":\"x\"}}", 10)]
    public void ChildError_RejectsAnythingOtherThanOneCurrentErrorEnvelope(
        string standardError,
        int exitCode)
    {
        Assert.Null(LocalServiceChildError.TryRead(standardError, exitCode));
    }

    [Fact]
    public void ChildError_RejectsAnEnvelopeAboveTheDiagnosticBudgetBeforeParsing()
    {
        Assert.Null(LocalServiceChildError.TryRead(
            new string('x', LocalServiceChildError.MaximumEnvelopeCharacters + 1),
            (int)ExitCode.InputError));
    }

    [Fact]
    public void ManagedSelfLaunch_UsesTheEntryAssemblyBesideTheHost()
    {
        string path = SelfProcessLauncher.ResolveManagedEntryAssemblyPath(
            typeof(LocalServiceRuntimeTests).Assembly,
            "test",
            "Build the test assembly.");

        Assert.Equal(
            Path.GetFullPath(typeof(LocalServiceRuntimeTests).Assembly.Location),
            path);
        Assert.Equal(Path.GetFullPath(AppContext.BaseDirectory), Path.GetDirectoryName(path) + Path.DirectorySeparatorChar);
    }

    [Fact]
    public void ManagedSelfLaunch_ReportsAnAssemblyThatIsNotBesideTheHost()
    {
        CliException error = Assert.Throws<CliException>(() =>
            SelfProcessLauncher.ResolveManagedEntryAssemblyPath(
                typeof(string).Assembly,
                "test",
                "Build the test assembly."));

        Assert.Equal(ErrorCodes.OptionInvalid, error.Code);
    }

    [Fact]
    public async Task ControlProtocol_SeparatesFrameBudgetFromCancellableOperations()
    {
        LocalServiceControlEndpoint endpoint = Endpoint();
        string nonce = Guid.NewGuid().ToString("N");
        string token = Guid.NewGuid().ToString("N");
        using var entered = new ManualResetEventSlim();
        using var resume = new ManualResetEventSlim();
        using var cancelled = new ManualResetEventSlim();
        using var parent = OperationDeadline.Start(TimeSpan.FromSeconds(10));
        using var server = new LocalServiceControlServer(endpoint, nonce, token, (request, deadline) =>
        {
            if (request.Command == "open")
            {
                Assert.Equal(parent.ExpiresAtTick, request.ExpiresAtTick);
                entered.Set();
                resume.Wait(deadline.Token);
            }
            else if (request.Command == "cancel")
            {
                entered.Set();
                deadline.Token.WaitHandle.WaitOne();
                cancelled.Set();
                deadline.ThrowIfExpired("test");
            }
            return new LocalServiceControlResponse(0, "", "", "", "", true);
        }, stageTimeout: TimeSpan.FromMilliseconds(100), operationTimeout: TimeSpan.FromSeconds(10));
        server.Start();
        await Task.Delay(30);
        Task<LocalServiceControlResponse> opening = Task.Run(() =>
            LocalServiceControlServer.Send(endpoint, nonce, token, "open", deadline: parent));
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            await Task.Delay(250);
            Assert.True(LocalServiceControlServer.Send(endpoint, nonce, token, "ping").Ok);
        }
        finally { resume.Set(); }
        Assert.True((await opening.WaitAsync(TimeSpan.FromSeconds(5))).Ok);
        Assert.False(LocalServiceControlServer.Send(endpoint, nonce, token, "cancel",
            timeout: TimeSpan.FromMilliseconds(100)).Ok);
        Assert.True(cancelled.IsSet);
        cancelled.Reset();
        entered.Reset();
        using (Stream connection = await ConnectRaw(endpoint))
        {
            byte[] request = LocalServiceControlCodec.Serialize(new LocalServiceControlRequest(
                LocalServiceControlServer.ProtocolVersion, Guid.NewGuid().ToString("N"), endpoint.Service,
                endpoint.InstanceId, nonce, token, "cancel"));
            await LocalServiceControlCodec.WriteFrameAsync(connection, request, CancellationToken.None);
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
        }
        Assert.True(cancelled.Wait(TimeSpan.FromSeconds(5)));
        Assert.True(LocalServiceControlServer.Send(endpoint, nonce, token, "ping").Ok);
    }

    [Fact]
    public void ControlProtocol_RoundTripsVersionedIdentityAndPayload()
    {
        LocalServiceControlEndpoint endpoint = Endpoint();
        string nonce = Guid.NewGuid().ToString("N");
        string token = Guid.NewGuid().ToString("N");
        using var server = new LocalServiceControlServer(
            endpoint,
            nonce,
            token,
            (request, _) => new LocalServiceControlResponse(
                0,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                true,
                Result: JsonSerializer.SerializeToElement(
                    new { state = "ready" })));
        server.Start();

        LocalServiceControlResponse response =
            LocalServiceControlServer.Send(
                endpoint,
                nonce,
                token,
                "status");

        Assert.True(response.Ok);
        Assert.Equal(
            LocalServiceControlServer.ProtocolVersion,
            response.Version);
        Assert.Equal(endpoint.Service, response.Service);
        Assert.Equal(endpoint.InstanceId, response.InstanceId);
        Assert.Equal(nonce, response.Nonce);
        Assert.Equal(
            "ready",
            response.Result?.GetProperty("state").GetString());
    }

    [Fact]
    public void ControlProtocol_RejectsWrongTokenAndNonce()
    {
        LocalServiceControlEndpoint endpoint = Endpoint();
        string nonce = Guid.NewGuid().ToString("N");
        string token = Guid.NewGuid().ToString("N");
        using var server = new LocalServiceControlServer(
            endpoint,
            nonce,
            token,
            (request, _) => throw new InvalidOperationException(
                "Unauthorized requests must not reach the adapter."));
        server.Start();

        Assert.False(LocalServiceControlServer.Send(
            endpoint,
            nonce,
            "wrong-token",
            "stop").Ok);
        Assert.Throws<InvalidDataException>(() =>
            LocalServiceControlServer.Send(
                endpoint,
                "wrong-nonce",
                token,
                "stop"));
    }

    [Fact]
    public async Task ControlProtocol_FlushesResponseBeforeRunningStopTransition()
    {
        LocalServiceControlEndpoint endpoint = Endpoint();
        string nonce = Guid.NewGuid().ToString("N");
        string token = Guid.NewGuid().ToString("N");
        using var transitionEntered = new ManualResetEventSlim();
        using var allowTransitionToFinish = new ManualResetEventSlim();
        using var server = new LocalServiceControlServer(
            endpoint,
            nonce,
            token,
            (request, _) => new LocalServiceControlResponse(
                0,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                true),
            afterResponse: request =>
            {
                transitionEntered.Set();
                allowTransitionToFinish.Wait(
                    TimeSpan.FromSeconds(5));
            });
        server.Start();

        Task<LocalServiceControlResponse> send = Task.Run(() =>
            LocalServiceControlServer.Send(
                endpoint,
                nonce,
                token,
                "stop"));
        Assert.True(transitionEntered.Wait(TimeSpan.FromSeconds(5)));
        LocalServiceControlResponse response =
            await send.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(response.Ok);

        allowTransitionToFinish.Set();
    }

    [Fact]
    public async Task ControlProtocol_PartialAndOversizedFramesDoNotBlockTheServer()
    {
        LocalServiceControlEndpoint endpoint = Endpoint();
        string nonce = Guid.NewGuid().ToString("N");
        string token = Guid.NewGuid().ToString("N");
        using var server = new LocalServiceControlServer(
            endpoint,
            nonce,
            token,
            (request, _) => new LocalServiceControlResponse(
                0,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                true),
            TimeSpan.FromMilliseconds(150));
        server.Start();

        using (Stream partial = await ConnectRaw(endpoint))
        {
            byte[] header = new byte[sizeof(int)];
            BinaryPrimitives.WriteInt32LittleEndian(header, 32);
            await partial.WriteAsync(header);
            await partial.FlushAsync();
            await Task.Delay(250);
        }

        Assert.True(LocalServiceControlServer.Send(
            endpoint,
            nonce,
            token,
            "ping").Ok);

        using (Stream oversized = await ConnectRaw(endpoint))
        {
            byte[] header = new byte[sizeof(int)];
            BinaryPrimitives.WriteInt32LittleEndian(
                header,
                LocalServiceControlServer.MaximumPayloadBytes + 1);
            await oversized.WriteAsync(header);
            await oversized.FlushAsync();
        }

        Assert.True(LocalServiceControlServer.Send(
            endpoint,
            nonce,
            token,
            "ping").Ok);
    }

    [Fact]
    public void UnixControlSocket_IsPrivateAndOwnedByTheEffectiveUser()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        LocalServiceControlEndpoint endpoint = Endpoint();
        using var server = new LocalServiceControlServer(
            endpoint,
            Guid.NewGuid().ToString("N"),
            Guid.NewGuid().ToString("N"),
            (request, _) => new LocalServiceControlResponse(
                0,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                true));
        server.Start();

        Assert.Equal(
            UnixFileMode.UserRead | UnixFileMode.UserWrite,
            File.GetUnixFileMode(endpoint.UnixSocketPath));
    }

    [Fact]
    public async Task OperationLockSerializesCompetingProcesses()
    {
        string key = Guid.NewGuid().ToString("N");
        using LocalServiceOperationLock first = LocalServiceOperationLock.Acquire(
            "contract",
            key,
            TimeSpan.FromSeconds(2));
        Task<LocalServiceOperationLock> competing = Task.Run(() =>
            LocalServiceOperationLock.Acquire(
                "contract",
                key,
                TimeSpan.FromSeconds(2)));
        await Task.Delay(150);
        Assert.False(competing.IsCompleted);

        first.Dispose();
        using LocalServiceOperationLock second = await competing;
    }

    [Fact]
    public void ProcessIdentity_RequiresPidStartTimeAndNonce()
    {
        LocalServiceProcessIdentity identity =
            LocalServiceProcessIdentity.Current(
                Guid.NewGuid().ToString("N"));

        Assert.True(identity.MatchesLiveProcess());
        Assert.False((identity with
        {
            StartTicksUtc = identity.StartTicksUtc
                - TimeSpan.FromMinutes(1).Ticks,
        }).MatchesLiveProcess());
        Assert.False((identity with { Nonce = string.Empty })
            .MatchesLiveProcess());
    }

    private static LocalServiceControlEndpoint Endpoint() => new(
        "contract",
        Guid.NewGuid().ToString("N"));

    private static async Task<Stream> ConnectRaw(
        LocalServiceControlEndpoint endpoint)
    {
        if (OperatingSystem.IsWindows())
        {
            var pipe = new NamedPipeClientStream(
                ".",
                endpoint.PipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);
            await pipe.ConnectAsync(
                new CancellationTokenSource(
                    TimeSpan.FromSeconds(5)).Token);
            return pipe;
        }

        var socket = new Socket(
            AddressFamily.Unix,
            SocketType.Stream,
            ProtocolType.Unspecified);
        await socket.ConnectAsync(
            new UnixDomainSocketEndPoint(
                endpoint.UnixSocketPath));
        return new NetworkStream(socket, ownsSocket: true);
    }
}
