[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $SdkAssembly,
    [Parameter(Mandatory)][string] $OutputDirectory,
    [string] $LicensePath = $(if ($env:ASPOSE_SLIDES_LICENSE_PATH) { $env:ASPOSE_SLIDES_LICENSE_PATH } else { $env:ASPOSE_LICENSE_PATH })
)

# Exit 0: rendering with a font fallback rule writes nothing to standard output. Exit 1: the SDK writes to it. Exit 2: the check could not run.
$ErrorActionPreference = 'Stop'
try {
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

    # Document-operation core: a fallback rule, then one export, with standard output captured.
    $captured = [IO.StringWriter]::new()
    $original = [Console]::Out
    $presentation = [Aspose.Slides.Presentation]::new()
    try {
        [Console]::SetOut($captured)
        $presentation.FontsManager.FontFallBackRulesCollection.Add(
            [Aspose.Slides.FontFallBackRule]::new([uint32]0x4E00, [uint32]0x9FFF, 'SimSun'))
        $shape = $presentation.Slides[0].Shapes.AddAutoShape([Aspose.Slides.ShapeType]::Rectangle, 40, 40, 600, 80)
        $shape.TextFrame.Text = '回退规则门禁'
        $presentation.Save((Join-Path $output 'output.pdf'), [Aspose.Slides.Export.SaveFormat]::Pdf)
    } finally {
        [Console]::SetOut($original)
        $presentation.Dispose()
    }

    $written = $captured.ToString()
    $result = [ordered]@{
        SdkVersion = [Aspose.Slides.Presentation].Assembly.GetName().Version.ToString()
        SdkSha256 = (Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash
        Licensed = -not [string]::IsNullOrWhiteSpace($LicensePath)
        StandardOutput = $written
    }
    $result | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $output 'result.json') -Encoding utf8
} catch {
    [Console]::Error.WriteLine($_.Exception.ToString())
    exit 2
}

if ([string]::IsNullOrEmpty($written)) { exit 0 }
[Console]::Error.WriteLine("Rendering with a fallback rule wrote to standard output: $($written.Trim())")
exit 1
