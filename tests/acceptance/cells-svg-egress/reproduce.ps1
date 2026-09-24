[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $SdkDir,
    [Parameter(Mandatory)][string] $OutputDirectory,
    [string] $LicensePath = $(if ($env:ASPOSE_CELLS_LICENSE_PATH) { $env:ASPOSE_CELLS_LICENSE_PATH } else { $env:ASPOSE_LICENSE_PATH })
)

# Exit 0: adding the SVG picture made no network request. Exit 1: it did. Exit 2: the check could not run.
$ErrorActionPreference = 'Stop'
try {
    $sdk = (Resolve-Path -LiteralPath $SdkDir).Path
    $assembly = (Resolve-Path -LiteralPath (Join-Path $sdk 'Aspose.Cells.dll')).Path
    $output = [IO.Path]::GetFullPath($OutputDirectory)
    if (Test-Path -LiteralPath $output) { throw 'Choose a fresh output directory; this reproduction does not overwrite files.' }
    [void][IO.Directory]::CreateDirectory($output)
    if ($IsWindows) {
        # SVG rasterization needs the SkiaSharp native library shipped beside the SDK.
        $architecture = [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString().ToLowerInvariant()
        [void][Runtime.InteropServices.NativeLibrary]::Load((Join-Path $sdk "runtimes/win-$architecture/native/libSkiaSharp.dll"))
    }
    $cells = [Reflection.Assembly]::LoadFrom($assembly)
    if (-not [string]::IsNullOrWhiteSpace($LicensePath)) {
        $license = [Aspose.Cells.License]::new()
        $license.SetLicense((Resolve-Path -LiteralPath $LicensePath).Path)
    }

    # The loopback server and the refusing provider run on SDK threads, so they are compiled
    # rather than PowerShell script blocks.
    Add-Type -ReferencedAssemblies $assembly, 'System.Net.Primitives', 'System.Net.Sockets', 'System.Collections.Concurrent' -TypeDefinition @'
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Aspose.Cells;

public sealed class CellsSvgEgressGate : IStreamProvider, IDisposable
{
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
    private readonly TcpListener _listener = new TcpListener(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new CancellationTokenSource();
    public readonly ConcurrentQueue<string> Requests = new ConcurrentQueue<string>();
    public readonly ConcurrentQueue<string> ProviderCalls = new ConcurrentQueue<string>();

    public CellsSvgEgressGate()
    {
        _listener.Start();
        Url = "http://127.0.0.1:" + ((IPEndPoint)_listener.LocalEndpoint).Port;
        Task.Run(Serve);
    }

    public string Url { get; private set; }

    // The documented refusal: skip the resource and supply no stream.
    public void InitStream(StreamProviderOptions options)
    {
        ProviderCalls.Enqueue(options.DefaultPath ?? string.Empty);
        options.ResourceLoadingType = ResourceLoadingType.Skip;
        options.Stream = null;
    }

    public void CloseStream(StreamProviderOptions options) { }

    private async Task Serve()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                using (TcpClient client = await _listener.AcceptTcpClientAsync(_stop.Token))
                using (NetworkStream stream = client.GetStream())
                {
                    var reader = new System.IO.StreamReader(stream, Encoding.ASCII);
                    Requests.Enqueue(await reader.ReadLineAsync() ?? "empty request");
                    string line;
                    while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync())) { }
                    byte[] header = Encoding.ASCII.GetBytes(
                        "HTTP/1.1 200 OK\r\nContent-Type: image/png\r\nContent-Length: " + Png.Length + "\r\nConnection: close\r\n\r\n");
                    await stream.WriteAsync(header, 0, header.Length);
                    await stream.WriteAsync(Png, 0, Png.Length);
                }
            }
        }
        catch (Exception) when (_stop.IsCancellationRequested) { }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _listener.Stop();
    }
}
'@

    $gate = [CellsSvgEgressGate]::new()
    try {
        $svg = [Text.Encoding]::UTF8.GetBytes(
            '<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="40" height="40">' +
            "<image xlink:href=`"$($gate.Url)/svg-image.png`" width=`"20`" height=`"20`"/>" +
            '<rect y="25" width="10" height="10"/></svg>')
        [IO.File]::WriteAllBytes((Join-Path $output 'input.svg'), $svg)

        # Document-operation core: the documented resource provider, then a documented picture insertion.
        $workbook = [Aspose.Cells.Workbook]::new()
        $workbook.Settings.ResourceProvider = $gate
        $stream = [IO.MemoryStream]::new($svg, $false)
        try { [void]$workbook.Worksheets[0].Pictures.Add(1, 1, $stream) } finally { $stream.Dispose() }
        $workbook.Save((Join-Path $output 'output.xlsx'))
        Start-Sleep -Milliseconds 500
        $result = [ordered]@{
            SdkVersion = $cells.GetName().Version.ToString()
            SdkSha256 = (Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash
            Licensed = -not [string]::IsNullOrWhiteSpace($LicensePath)
            ProviderCalls = @($gate.ProviderCalls.ToArray())
            NetworkRequests = @($gate.Requests.ToArray())
        }
    } finally { $gate.Dispose() }
    $result | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $output 'result.json') -Encoding utf8
} catch {
    [Console]::Error.WriteLine($_.Exception.ToString())
    exit 2
}

if ($result.NetworkRequests.Count -ne 0) {
    [Console]::Error.WriteLine("Adding the SVG picture made $($result.NetworkRequests.Count) network request(s) although the resource provider refuses every resource: $($result.NetworkRequests -join '; ')")
    exit 1
}
exit 0
