using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Sdk.Errors;
using Xunit;

namespace Aspose.Cli.Host.Tests.LocalServices;

/// <summary>
/// A port HTTP.sys refuses with access denied, because another principal holds a path beneath its
/// root, is one a chosen port skips and an explicit port reports. The held paths are the URL
/// reservations Windows itself makes for other accounts (such as <c>http://+:5985/wsman/</c>);
/// creating one would need an administrator.
/// </summary>
public sealed partial class LoopbackHttpListenerBinderTests
{
    [Fact]
    public void AChosenPort_HeldBeneathItsRootByAnotherAccount_IsSkipped()
    {
        int held = HeldPort();
        LoopbackHttpListenerBinding probe = LoopbackHttpListenerBinder.Start(0);
        int free = probe.Port;
        probe.Listener.Close();

        LoopbackHttpListenerBinding binding = LoopbackHttpListenerBinder.StartAny([held, free]);
        using HttpListener listener = binding.Listener;

        Assert.Equal(free, binding.Port);
        Assert.True(listener.IsListening);
    }

    [Fact]
    public void AnExplicitPort_HeldBeneathItsRootByAnotherAccount_IsReported()
    {
        int held = HeldPort();

        CliException error = Assert.Throws<CliException>(() => LoopbackHttpListenerBinder.Start(held));

        Assert.Equal(ErrorCodes.LoopbackListenerUnavailable, error.Code);
        Assert.Equal("permission-denied", error.Details!["reason"]!.GetValue<string>());
    }

    /// <summary>
    /// A port whose root this user may not register because a reservation for another account
    /// covers a path beneath it.
    /// </summary>
    private static int HeldPort()
    {
        var start = new ProcessStartInfo("netsh", "http show urlacl")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        using Process netsh = Process.Start(start)!;
        string reservations = netsh.StandardOutput.ReadToEnd();
        netsh.WaitForExit();
        foreach (Match reserved in Reservation().Matches(reservations))
        {
            int port = int.Parse(reserved.Groups["port"].Value, System.Globalization.CultureInfo.InvariantCulture);
            using var listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            try
            {
                listener.Start();
            }
            catch (HttpListenerException exception) when (exception.ErrorCode == 5)
            {
                return port;
            }
        }
        Assert.Skip("This machine has no URL reservation beneath a port root for another account.");
        return 0;
    }

    [GeneratedRegex(@"http://[+*]:(?<port>[0-9]+)/[^/\s]+/")]
    private static partial Regex Reservation();
}
