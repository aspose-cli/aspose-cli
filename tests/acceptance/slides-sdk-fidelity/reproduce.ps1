[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $SdkAssembly,
    [Parameter(Mandatory)][string] $Output,
    [string] $LicensePath = $(if ($env:ASPOSE_SLIDES_LICENSE_PATH) { $env:ASPOSE_SLIDES_LICENSE_PATH } else { $env:ASPOSE_LICENSE_PATH }),
    [switch] $KeepThumbnail
)

$ErrorActionPreference = 'Stop'
try {
    if ([string]::IsNullOrWhiteSpace($LicensePath)) { throw 'Supply a commercial Slides license via -LicensePath or ASPOSE_SLIDES_LICENSE_PATH.' }
    $assembly = (Resolve-Path -LiteralPath $SdkAssembly).Path
    $licenseFile = (Resolve-Path -LiteralPath $LicensePath).Path
    $outputPath = [IO.Path]::GetFullPath($Output)
    if (Test-Path -LiteralPath $outputPath) { throw 'Choose a fresh output path; this reproduction does not overwrite files.' }
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($outputPath))
    if ($IsWindows) { $env:PATH = [IO.Path]::GetDirectoryName($assembly) + [IO.Path]::PathSeparator + $env:PATH }
    Add-Type -Path $assembly
    $license = [Aspose.Slides.License]::new()
    $license.SetLicense($licenseFile)
    $presentation = [Aspose.Slides.Presentation]::new((Join-Path $PSScriptRoot 'input.pptx'))
    try {
        # Document operation core: no chart, slide, theme or font access before Save.
        if ($KeepThumbnail) {
            $options = [Aspose.Slides.Export.PptxOptions]::new()
            $options.RefreshThumbnail = $false
            $presentation.Save($outputPath, [Aspose.Slides.Export.SaveFormat]::Pptx, $options)
        } else {
            $presentation.Save($outputPath, [Aspose.Slides.Export.SaveFormat]::Pptx)
        }
    } finally { $presentation.Dispose() }
} catch {
    [Console]::Error.WriteLine($_.Exception.ToString())
    exit 2
}

& (Join-Path $PSScriptRoot 'verify.ps1') -Presentation $outputPath
exit $LASTEXITCODE
