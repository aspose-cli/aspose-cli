[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $SdkAssembly,
    [Parameter(Mandatory)][string] $OutputDirectory,
    [string] $LicensePath = $(if ($env:ASPOSE_SLIDES_LICENSE_PATH) { $env:ASPOSE_SLIDES_LICENSE_PATH } else { $env:ASPOSE_LICENSE_PATH })
)

# Exit 0: one Chinese run is drawn in one font. Exit 1: the run alternates between fonts glyph by glyph. Exit 2: the check could not run.
$ErrorActionPreference = 'Stop'
try {
    if (-not $IsWindows) { throw 'This gate needs the Windows Chinese and Japanese fonts; run it on Windows.' }
    $fonts = [Environment]::GetFolderPath([Environment+SpecialFolder]::Fonts)
    foreach ($required in @('simsun.ttc', 'msgothic.ttc')) {
        if (-not (Test-Path -LiteralPath (Join-Path $fonts $required))) {
            throw "The defect shows only when both SimSun and MS Gothic are installed; $required is missing."
        }
    }
    $assembly = (Resolve-Path -LiteralPath $SdkAssembly).Path
    $output = [IO.Path]::GetFullPath($OutputDirectory)
    if (Test-Path -LiteralPath $output) { throw 'Choose a fresh output directory; this reproduction does not overwrite files.' }
    [void][IO.Directory]::CreateDirectory($output)

    # The native drawing library shipped with the SDK must be discoverable.
    $env:PATH = [IO.Path]::GetDirectoryName($assembly) + [IO.Path]::PathSeparator + $env:PATH
    Add-Type -Path $assembly
    if (-not [string]::IsNullOrWhiteSpace($LicensePath)) {
        $license = [Aspose.Slides.License]::new()
        $license.SetLicense((Resolve-Path -LiteralPath $LicensePath).Path)
    }

    # Document-operation core: one Chinese run in a new presentation, whose theme fonts have no
    # Chinese glyphs, drawn to SVG.
    $text = '问题与对策应收账款余额'
    $svgPath = Join-Path $output 'slide.svg'
    $presentation = [Aspose.Slides.Presentation]::new()
    try {
        $shape = $presentation.Slides[0].Shapes.AddAutoShape([Aspose.Slides.ShapeType]::Rectangle, 40, 40, 600, 80)
        $shape.TextFrame.Text = $text
        $stream = [IO.File]::Create($svgPath)
        try { $presentation.Slides[0].WriteAsSvg($stream) } finally { $stream.Dispose() }
    } finally { $presentation.Dispose() }

    [xml] $svg = Get-Content -LiteralPath $svgPath -Raw -Encoding utf8
    $families = @($svg.SelectNodes('//*[local-name()="text" or local-name()="tspan"]') |
        Where-Object { $_.InnerText -match '[一-鿿]' } |
        ForEach-Object { $_.GetAttribute('font-family') } |
        Where-Object { $_ } |
        Sort-Object -Unique)
    $result = [ordered]@{
        SdkVersion = [Aspose.Slides.Presentation].Assembly.GetName().Version.ToString()
        SdkSha256 = (Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash
        Licensed = -not [string]::IsNullOrWhiteSpace($LicensePath)
        Text = $text
        FontFamilies = $families
    }
    $result | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $output 'result.json') -Encoding utf8
    if ($families.Count -eq 0) { throw 'The SVG names no font for the Chinese text; the reproduction did not take effect.' }
} catch {
    [Console]::Error.WriteLine($_.Exception.ToString())
    exit 2
}

if ($families.Count -eq 1) { exit 0 }
[Console]::Error.WriteLine("One Chinese run is drawn in $($families.Count) fonts: $($families -join ', ').")
exit 1
