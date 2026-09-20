using System.Net.Sockets;
using Aspose.Cli.Host.LocalServices;
using System.Text;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using Aspose.Cli.Host.App;
using Aspose.Cli.Host.Commands;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Host.Output;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Rendering;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Host.Tests;

[CollectionDefinition("App mutation isolation", DisableParallelization = true)]
public sealed class AppMutationIsolationCollection;

[Collection("App mutation isolation")]
public sealed class AppMutationTests
{
    [Fact]
    public async Task SlowRequestBodyDoesNotBlockStatusOrClearAndKeepsItsUploadAlive()
    {
        using var app = new RunningApp();
        using (var upload = new MemoryStream("Heading,Value\nUPLOADED,42\n"u8.ToArray()))
        { await app.Host.UploadFileAsync("upload.csv", upload, upload.Length, CancellationToken.None); }
        string uploaded = Assert.Single(Directory.GetFiles(Path.Combine(app.SessionRoot, "uploads", "files")));
        using TcpClient client = await BeginSlowRequestBody(app.Host);
        var status = Task.Run(app.Host.Status);
        try
        {
            Assert.Equal("upload.csv", (await status.WaitAsync(TimeSpan.FromSeconds(2))).File);
            await app.Host.ClearLocalDataAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Null(app.Host.Status().File);
        }
        finally { client.Dispose(); await status.WaitAsync(TimeSpan.FromSeconds(7)); }
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (File.Exists(uploaded)) { await Task.Delay(20, deadline.Token); }
        Assert.True(File.Exists(app.Original));
    }

    private static async Task<TcpClient> BeginSlowRequestBody(AppHost host)
    {
        var client = new TcpClient();
        try
        {
            await client.ConnectAsync("127.0.0.1", host.Port);
            string origin = $"http://127.0.0.1:{host.Port}";
            using var http = new HttpClient();
            string shell = await http.GetStringAsync(host.Url);
            string csrf = System.Text.RegularExpressions.Regex.Match(shell,
                @"name=""aspose-csrf"" content=""([^""]+)""").Groups[1].Value;
            Assert.NotEmpty(csrf);
            string request = $"POST /api/preferences HTTP/1.1\r\nHost: 127.0.0.1:{host.Port}\r\nOrigin: {origin}\r\n{LocalHttpRequestSecurity.CsrfHeader}: {csrf}\r\nContent-Type: application/json\r\nContent-Length: 1\r\nExpect: 100-continue\r\nConnection: close\r\n\r\n";
            await client.GetStream().WriteAsync(Encoding.ASCII.GetBytes(request));
            using var reader = new StreamReader(client.GetStream(), Encoding.ASCII, false, 1024, leaveOpen: true);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            string? line = await reader.ReadLineAsync(timeout.Token);
            Assert.Contains("100 Continue", line, StringComparison.OrdinalIgnoreCase);
            while (!string.IsNullOrEmpty(await reader.ReadLineAsync(timeout.Token))) { }
            return client;
        }
        catch { client.Dispose(); throw; }
    }

    [Fact]
    public async Task UploadSerializesCleanupWhileStatusAndCancellationRemainAvailable()
    {
        using var app = new RunningApp();
        using var input = new PausedInput("Heading,Value\nUPLOADED,42\n");
        Task upload = app.Host.UploadFileAsync("upload.csv", input, input.Length, CancellationToken.None);
        await input.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task clear = app.Host.ClearLocalDataAsync(CancellationToken.None);
        using var cancelled = new CancellationTokenSource();
        Task abandoned = app.Host.ClearLocalDataAsync(cancelled.Token);
        try
        {
            Assert.False(clear.IsCompleted);
            Assert.Equal("original.csv", (await Task.Run(app.Host.Status).WaitAsync(TimeSpan.FromSeconds(5))).File);
            cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => abandoned);
        }
        finally { input.Resume.TrySetResult(); }
        await Task.WhenAll(upload, clear).WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Null(app.Host.Status().File);
        Assert.True(File.Exists(app.Original));
    }

    [Fact]
    public async Task CleanupPreservesUnknownFilesAndAnExternalReplacementOfAnOwnedUpload()
    {
        using var app = new RunningApp();
        using var input = new MemoryStream("Heading,Value\nUPLOADED,42\n"u8.ToArray());
        await app.Host.UploadFileAsync("upload.csv", input, input.Length, CancellationToken.None);
        string directory = Path.Combine(app.SessionRoot, "uploads", "files");
        string uploaded = Assert.Single(Directory.GetFiles(directory));
        string replacement = Path.Combine(directory, "external.csv");
        File.WriteAllText(replacement, "EXTERNAL");
        File.Replace(replacement, uploaded, null);
        string unknown = Path.Combine(directory, "unowned.txt");
        File.WriteAllText(unknown, "UNOWNED");
        await app.Host.ClearLocalDataAsync(CancellationToken.None);
        app.Host.Dispose();
        Assert.Equal("EXTERNAL", File.ReadAllText(uploaded));
        Assert.Equal("UNOWNED", File.ReadAllText(unknown));
        Assert.True(File.Exists(app.Original));
    }

    [Fact]
    public async Task APreparedStopRejectsNewMutationsWithoutBlockingStatus()
    {
        using var app = new RunningApp();
        app.Host.PrepareStop();
        CliException error = Assert.Throws<CliException>(() => app.Host.OpenPath(app.Original));
        Assert.Equal(ErrorCodes.AppBusy, error.Code);
        await Assert.ThrowsAsync<CliException>(() => app.Host.ClearLocalDataAsync(CancellationToken.None));
        Assert.Equal("original.csv", app.Host.Status().File);
    }

    private sealed class PausedInput(string text) : MemoryStream(Encoding.UTF8.GetBytes(text))
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Entered.TrySetResult();
            await Resume.Task.WaitAsync(cancellationToken);
            return await base.ReadAsync(buffer, cancellationToken);
        }
    }

    private sealed class RunningApp : IDisposable
    {
        private readonly TempDirectory _temp = new();
        private readonly Dictionary<string, string?> _environment = [];
        private readonly ViewerServiceProcess _viewer;
        internal AppHost Host { get; }
        internal string Original { get; }
        internal string SessionRoot { get; }
        static RunningApp()
        {
            var dependencies = new AssemblyDependencyResolver(Path.ChangeExtension(CliRunner.ExecutablePath, ".dll"));
            AssemblyLoadContext.Default.ResolvingUnmanagedDll += (_, name) =>
                dependencies.ResolveUnmanagedDllToPath(name) is { } path ? NativeLibrary.Load(path) : IntPtr.Zero;
        }

        internal RunningApp()
        {
            string[] variables = Environment.GetEnvironmentVariables().Keys.Cast<string>()
                .Where(name => name.StartsWith("ASPOSE_", StringComparison.OrdinalIgnoreCase)
                    && (name.EndsWith("_LICENSE_PATH", StringComparison.OrdinalIgnoreCase)
                        || name.EndsWith("_LICENSE_B64", StringComparison.OrdinalIgnoreCase)))
                .Append("ASPOSE_CLI_CONFIG_DIR").Distinct().ToArray();
            foreach (string name in variables)
            {
                _environment[name] = Environment.GetEnvironmentVariable(name);
                Environment.SetEnvironmentVariable(name, null);
            }
            Environment.SetEnvironmentVariable("ASPOSE_CLI_CONFIG_DIR", _temp.File("config"));
            _viewer = new ViewerServiceProcess(_temp.File("config"));
            Original = _temp.File("original.csv");
            File.WriteAllText(Original, "Heading,Value\nORIGINAL,1\n");
            HostContext host = ActualCommandTree.Host;
            var root = RootCommandFactory.Create(host, out _);
            string sessions = PrivateUserStorage.EnsureDirectory(Path.Combine(PrivateUserStorage.TemporaryRoot(), "app"));
            string[] before = Directory.GetDirectories(sessions, $"{Environment.ProcessId}-*");
            Host = new AppHost(host.Catalog, () => CliCapabilitySnapshot.Create(root, host.Catalog, host.Schemas).Result,
                new GlobalValues(OutputMode.Json, true, false, null, _temp.Path, null, InputSizeGuard.DefaultMaxBytes),
                FontSearchProfile.Ambient);
            SessionRoot = Assert.Single(Directory.GetDirectories(sessions, $"{Environment.ProcessId}-*").Except(before));
            Host.Start(0, AppRoutes.Home, Original);
        }
        public void Dispose()
        {
            try { Host.Dispose(); _viewer.Dispose(); PrivateUserStorage.TryDeleteTree(SessionRoot); }
            finally
            {
                foreach ((string name, string? value) in _environment) { Environment.SetEnvironmentVariable(name, value); }
                _temp.Dispose();
            }
        }
    }
}
