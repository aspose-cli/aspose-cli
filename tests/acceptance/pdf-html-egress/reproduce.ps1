[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $SdkDir,
    [Parameter(Mandatory)][string] $OutputDirectory,
    [string] $LicensePath = $(if ($env:ASPOSE_PDF_LICENSE_PATH) { $env:ASPOSE_PDF_LICENSE_PATH } else { $env:ASPOSE_LICENSE_PATH })
)

# Exit 0: the importer made no network request. Exit 1: it did. Exit 2: the check could not run.
$ErrorActionPreference = 'Stop'
try {
    $sdk = (Resolve-Path -LiteralPath $SdkDir).Path
    $assembly = (Resolve-Path -LiteralPath (Join-Path $sdk 'Aspose.PDF.dll')).Path
    $output = [IO.Path]::GetFullPath($OutputDirectory)
    if (Test-Path -LiteralPath $output) { throw 'Choose a fresh output directory; this reproduction does not overwrite files.' }
    [void][IO.Directory]::CreateDirectory($output)
    $pdf = [Reflection.Assembly]::LoadFrom($assembly)
    if (-not [string]::IsNullOrWhiteSpace($LicensePath)) {
        $license = [Aspose.Pdf.License]::new()
        $license.SetLicense((Resolve-Path -LiteralPath $LicensePath).Path)
    }

    # The loopback server and the refusing loader run on SDK threads, so they are compiled
    # rather than PowerShell script blocks.
    Add-Type -ReferencedAssemblies $assembly, 'System.Net.Primitives', 'System.Net.Sockets', 'System.Collections.Concurrent' -TypeDefinition @'
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Aspose.Pdf;

public sealed class PdfHtmlEgressGate : IDisposable
{
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
    private readonly TcpListener _listener = new TcpListener(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new CancellationTokenSource();
    public readonly ConcurrentQueue<string> Requests = new ConcurrentQueue<string>();
    public readonly ConcurrentQueue<string> LoaderCalls = new ConcurrentQueue<string>();

    public PdfHtmlEgressGate()
    {
        _listener.Start();
        Url = "http://127.0.0.1:" + ((IPEndPoint)_listener.LocalEndpoint).Port;
        Task.Run(Serve);
    }

    public string Url { get; private set; }

    /// <summary>Installs the documented custom loader, which refuses every external resource.</summary>
    public void RefuseEveryResource(HtmlLoadOptions options)
    {
        options.CustomLoaderOfExternalResources = Refuse;
    }

    // An empty result that is not cancelled keeps the SDK default loader off.
    private LoadOptions.ResourceLoadingResult Refuse(string uri)
    {
        LoaderCalls.Enqueue(uri);
        return new LoadOptions.ResourceLoadingResult(new byte[0]) { LoadingCancelled = false };
    }

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

    $gate = [PdfHtmlEgressGate]::new()
    try {
        $html = Join-Path $output 'input.html'
        [IO.File]::WriteAllText($html, @"
<html><head><link rel="stylesheet" href="$($gate.Url)/style.css"></head>
<body><p>Local content</p><img src="$($gate.Url)/image.png"></body></html>
"@, [Text.UTF8Encoding]::new($false))

        # Document-operation core: the documented HTML import with a custom resource loader.
        $options = [Aspose.Pdf.HtmlLoadOptions]::new($output + [IO.Path]::DirectorySeparatorChar)
        $gate.RefuseEveryResource($options)
        $document = [Aspose.Pdf.Document]::new($html, $options)
        try { $document.Save((Join-Path $output 'output.pdf')) } finally { $document.Dispose() }
        Start-Sleep -Milliseconds 500
        $result = [ordered]@{
            SdkVersion = $pdf.GetName().Version.ToString()
            SdkSha256 = (Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash
            Licensed = -not [string]::IsNullOrWhiteSpace($LicensePath)
            LoaderCalls = @($gate.LoaderCalls.ToArray())
            NetworkRequests = @($gate.Requests.ToArray())
        }
    } finally { $gate.Dispose() }
    $result | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $output 'result.json') -Encoding utf8
} catch {
    [Console]::Error.WriteLine($_.Exception.ToString())
    exit 2
}

if ($result.LoaderCalls.Count -eq 0) {
    [Console]::Error.WriteLine('The importer never consulted the custom loader; the reproduction did not exercise the defect.')
    exit 2
}
if ($result.NetworkRequests.Count -ne 0) {
    [Console]::Error.WriteLine("The importer made $($result.NetworkRequests.Count) network request(s) although the custom loader refused every resource: $($result.NetworkRequests -join '; ')")
    exit 1
}
exit 0
