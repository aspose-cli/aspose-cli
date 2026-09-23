[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $CliPath,
    [Parameter(Mandatory)][string] $SdkDir,
    [Parameter(Mandatory)][string] $OutputDirectory,
    [string] $LicensePath = $(if ($env:ASPOSE_PDF_LICENSE_PATH) { $env:ASPOSE_PDF_LICENSE_PATH } else { $env:ASPOSE_LICENSE_PATH })
)

$ErrorActionPreference = 'Stop'
try {
    if ([string]::IsNullOrWhiteSpace($LicensePath)) { throw 'Supply a PDF license through -LicensePath or ASPOSE_PDF_LICENSE_PATH.' }
    $cli = (Resolve-Path -LiteralPath $CliPath).Path
    $sdk = (Resolve-Path -LiteralPath $SdkDir).Path
    $assembly = (Resolve-Path -LiteralPath (Join-Path $sdk 'Aspose.PDF.dll')).Path
    $licenseFile = (Resolve-Path -LiteralPath $LicensePath).Path
    $output = [IO.Path]::GetFullPath($OutputDirectory)
    if (Test-Path -LiteralPath $output) { throw 'Choose a fresh output directory; this reproduction does not overwrite files.' }
    [void][IO.Directory]::CreateDirectory($output)
    $inputPdf = Join-Path $PSScriptRoot 'input.pdf'
    $inputHash = (Get-FileHash -LiteralPath $inputPdf -Algorithm SHA256).Hash
    $ops = Join-Path $output 'move.json'
    [IO.File]::WriteAllText($ops, '{"ops":[{"op":"move_pages","pages":"2","to":1}]}', [Text.UTF8Encoding]::new($false))
    $cliOutput = Join-Path $output 'cli.pdf'
    $arguments = @('pdf','edit',$inputPdf,'--ops',$ops,'--out',$cliOutput,'--output','json','--timeout','30')
    $start = [Diagnostics.ProcessStartInfo]::new($cli)
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
    $start.Environment['ASPOSE_PDF_LICENSE_PATH'] = $licenseFile
    foreach ($argument in $arguments) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($start)
    try {
        $stdout = $process.StandardOutput.ReadToEndAsync(); $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(45000)) { $process.Kill($true); $process.WaitForExit(); throw 'The owned CLI reproduction exceeded 45 seconds.' }
        [pscustomobject]@{Command=@($cli)+$arguments;ExitCode=$process.ExitCode;Stdout=$stdout.GetAwaiter().GetResult();Stderr=$stderr.GetAwaiter().GetResult()} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $output 'cli-command.json') -Encoding utf8
        if ($process.ExitCode -ne 0) { throw "CLI command failed with exit $($process.ExitCode); see cli-command.json." }
    } finally { $process.Dispose() }

    [void][Reflection.Assembly]::LoadFrom($assembly)
    $license = [Aspose.Pdf.License]::new(); $license.SetLicense($licenseFile)
    $pdf = [Aspose.Pdf.Document]::new($inputPdf)
    try {
        # Public-API control only: no raw objects, destination rewriting or SDK default changes.
        $coordinates = @($pdf.Outlines | Where-Object Title -In @('Appendix-null','Appendix-zero') | ForEach-Object {
            [pscustomobject]@{Title=$_.Title;Left=$_.Destination.Left;Top=$_.Destination.Top;Zoom=$_.Destination.Zoom}
        })
        $page = $pdf.Pages[2]
        [void]$pdf.Pages.Insert(1, $page)
        $pdf.Pages.Delete(3)
        $nativeOutput = Join-Path $output 'native.pdf'
        $pdf.Save($nativeOutput)
    } finally { $pdf.Dispose() }
    [pscustomobject]@{CliSha256=(Get-FileHash -LiteralPath $cli -Algorithm SHA256).Hash;SdkVersion=[Aspose.Pdf.Document].Assembly.GetName().Version.ToString();SdkSha256=(Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash;InputSha256=$inputHash;InputUnchanged=$inputHash -ceq (Get-FileHash -LiteralPath $inputPdf -Algorithm SHA256).Hash;PublicCoordinateObservations=$coordinates;ObservationIsPreservationPass=$false} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $output 'provenance.json') -Encoding utf8
} catch {
    [Console]::Error.WriteLine($_.Exception.ToString())
    exit 2
}
& (Join-Path $PSScriptRoot 'verify.ps1') -Document $cliOutput -SdkDir $sdk -LicensePath $licenseFile -ResultPath (Join-Path $output 'cli-verification.json')
$cliGate = $LASTEXITCODE
& (Join-Path $PSScriptRoot 'verify.ps1') -Document $nativeOutput -SdkDir $sdk -LicensePath $licenseFile -ResultPath (Join-Path $output 'native-verification.json')
$nativeGate = $LASTEXITCODE
if ($cliGate -eq 2 -or $nativeGate -eq 2) { exit 2 }
if ($cliGate -ne 0 -or $nativeGate -ne 0) { exit 1 }
exit 0
