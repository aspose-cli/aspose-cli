using System.Net;
using System.Net.Sockets;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.IO.Input;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Sdk.Tests.IO;

/// <summary>
/// The gate blocks the ftp and file requests of an engine as it blocks http and https, whatever
/// the invocation's network access, and lets a file through only when a resource loader
/// permitted it.
/// </summary>
[Collection(ProcessNetworkGateCollection.Name)]
public sealed class NetworkGateTests
{
#pragma warning disable SYSLIB0014 // Document engines request resources through WebRequest; the gate is checked the same way.
    [Fact]
    public void FtpAndFileRequests_AreBlockedUnlessALoaderPermittedTheFile()
    {
        using var workspace = new TempWorkspace();
        string outside = workspace.File("outside.txt");
        File.WriteAllText(outside, "OUTSIDE-SENTINEL");
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var ftp = new Uri($"ftp://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/resource.png");
            var file = new Uri(outside);

            foreach (NetworkAccess access in new[] { NetworkAccess.None, NetworkAccess.Web })
            {
                NetworkGate.Admit(access);

                WebException ftpRefusal = Assert.Throws<WebException>(() => WebRequest.Create(ftp).GetResponse());
                WebException fileRefusal = Assert.Throws<WebException>(() => WebRequest.Create(file).GetResponse());

                Assert.Contains("blocked", ftpRefusal.Message, StringComparison.Ordinal);
                Assert.Contains("blocked", fileRefusal.Message, StringComparison.Ordinal);
                Assert.False(listener.Pending(), $"An ftp request reached the network with access {access}.");
                Warning warning = Assert.IsType<Warning>(NetworkGate.Warning);
                Assert.Equal(WarningCodes.RemoteResourcesBlocked, warning.Code);
                Assert.Contains(ftp.GetLeftPart(UriPartial.Path), warning.Message, StringComparison.Ordinal);
                // A file is named, never its folder.
                Assert.Contains("outside.txt", warning.Message, StringComparison.Ordinal);
                Assert.DoesNotContain(workspace.Path, warning.Message, StringComparison.OrdinalIgnoreCase);
            }

            NetworkGate.Permit(outside);
            using (WebResponse response = WebRequest.Create(file).GetResponse())
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                Assert.Equal("OUTSIDE-SENTINEL", reader.ReadToEnd());
            }
            // A permitted file is no longer reported as omitted; the blocked ftp request still is.
            string listed = Assert.IsType<Warning>(NetworkGate.Warning).Message;
            Assert.DoesNotContain("outside.txt", listed, StringComparison.Ordinal);
            Assert.Contains(ftp.GetLeftPart(UriPartial.Path), listed, StringComparison.Ordinal);
        }
        finally
        {
            listener.Stop();
            // The gate stays installed for the process; leave it as open as an opted-in invocation.
            NetworkGate.Admit(NetworkAccess.Web);
        }
    }

    /// <summary>
    /// A file request names a host when a document references a share, such as
    /// <c>\\localhost\pipe\name</c>: the gate blocks it whatever the host is called, before the
    /// request opens the path, which a pipe of the test's own would see.
    /// </summary>
    [Fact]
    public async Task FileRequestsWithAHost_AreBlockedBeforeTheyOpenAnything()
    {
        string name = "aspose-cli-gate-" + Guid.NewGuid().ToString("N");
        var server = new System.IO.Pipes.NamedPipeServerStream(name, System.IO.Pipes.PipeDirection.InOut, 1,
            System.IO.Pipes.PipeTransmissionMode.Byte, System.IO.Pipes.PipeOptions.Asynchronous);
        Task connected = server.WaitForConnectionAsync();
        Uri[] hosted =
        [
            new(@"\\localhost\pipe\" + name),
            new("file://localhost/pipe/" + name),
            new("file://Server/share/x.png"),
            new("file://127.0.0.1/c$/x.png"),
            new("file://2130706433/c$/x.png"),
            new("file://[::1]/c$/x.png"),
            new("file://xn--bcher-kva.example/share/x.png"),
            new("file://1password.example/share/x.png"),
        ];
        try
        {
            NetworkGate.Admit(NetworkAccess.None);

            foreach (Uri uri in hosted)
            {
                WebException refusal = Assert.Throws<WebException>(() => WebRequest.Create(uri).GetResponse());
                Assert.Contains("blocked", refusal.Message, StringComparison.Ordinal);
            }

            await Task.Delay(250);
            Assert.False(connected.IsCompleted, "A blocked file request opened the pipe it names.");
            string listed = Assert.IsType<Warning>(NetworkGate.Warning).Message;
            Assert.Contains(name, listed, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await server.DisposeAsync();
            NetworkGate.Admit(NetworkAccess.Web);
        }
    }
    /// <summary>
    /// The warning names a blocked file as the input's resource loader names it: relative to the
    /// input's folder below it, by its file name elsewhere, and two files that share a name as two.
    /// </summary>
    [Fact]
    public void BlockedFiles_AreNamedWithoutTheirFolders()
    {
        using var workspace = new TempWorkspace();
        string input = workspace.File("in/input.html");
        Directory.CreateDirectory(Path.GetDirectoryName(input)!);
        File.WriteAllText(input, "<html></html>");
        string below = workspace.File("in/images/below.png");
        string first = workspace.File("one/same.png");
        string second = workspace.File("two/same.png");
        foreach (string file in new[] { below, first, second })
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, "png");
        }
        try
        {
            NetworkGate.Admit(NetworkAccess.None);
            using var loader = new LocalDocumentResourceLoader(input, TestBudgets.Create());

            foreach (string file in new[] { below, first, second })
            {
                Assert.Throws<WebException>(() => WebRequest.Create(new Uri(file)).GetResponse());
            }

            string message = Assert.IsType<Warning>(NetworkGate.Warning).Message;
            Assert.StartsWith("3 resource(s)", message, StringComparison.Ordinal);
            Assert.Contains(Path.Combine("images", "below.png"), message, StringComparison.Ordinal);
            Assert.Equal(2, message.Split("same.png").Length - 1);
            Assert.DoesNotContain(workspace.Path, message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            NetworkGate.Admit(NetworkAccess.Web);
        }
    }
#pragma warning restore SYSLIB0014
}
