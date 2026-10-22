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
                Assert.Contains(Path.GetFullPath(outside), warning.Message, StringComparison.OrdinalIgnoreCase);
            }

            NetworkGate.Permit(outside);
            using (WebResponse response = WebRequest.Create(file).GetResponse())
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                Assert.Equal("OUTSIDE-SENTINEL", reader.ReadToEnd());
            }
            // A permitted file is no longer reported as omitted; the blocked ftp request still is.
            string listed = Assert.IsType<Warning>(NetworkGate.Warning).Message;
            Assert.DoesNotContain(Path.GetFullPath(outside), listed, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(ftp.GetLeftPart(UriPartial.Path), listed, StringComparison.Ordinal);
        }
        finally
        {
            listener.Stop();
            // The gate stays installed for the process; leave it as open as an opted-in invocation.
            NetworkGate.Admit(NetworkAccess.Web);
        }
    }
#pragma warning restore SYSLIB0014
}
