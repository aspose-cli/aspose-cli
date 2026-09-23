[CmdletBinding()]
param([Parameter(Mandatory)][string] $Presentation)

$ErrorActionPreference = 'Stop'
$expected = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'expected.json') -Raw | ConvertFrom-Json
$fixture = Join-Path $PSScriptRoot 'input.pptx'
if ((Get-FileHash -LiteralPath $fixture -Algorithm SHA256).Hash -ne $expected.inputSha256) {
    throw 'The fidelity fixture changed; review its independent Office expectations before updating them.'
}

$package = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $Presentation).Path)
try {
    $entry = $package.GetEntry($expected.chartPart)
    if ($null -eq $entry) { throw "Missing chart part: $($expected.chartPart)" }
    $settings = [Xml.XmlReaderSettings]::new()
    $settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $settings.MaxCharactersInDocument = 1048576
    $stream = $entry.Open()
    try {
        $reader = [Xml.XmlReader]::Create($stream, $settings)
        try {
            $document = [Xml.XmlDocument]::new()
            $document.Load($reader)
        } finally { $reader.Dispose() }
    } finally { $stream.Dispose() }
} finally { $package.Dispose() }

$namespaces = [Xml.XmlNamespaceManager]::new($document.NameTable)
$namespaces.AddNamespace('c', 'http://schemas.openxmlformats.org/drawingml/2006/chart')
function Node([string] $path) { $document.SelectSingleNode($path, $namespaces) }
function BooleanValue($element) {
    if ($null -eq $element) { return $null }
    if (-not $element.HasAttribute('val')) { return $true }
    return [Xml.XmlConvert]::ToBoolean($element.GetAttribute('val'))
}

$title = Node '/c:chartSpace/c:chart/c:title'
$automaticTitleDeleted = BooleanValue (Node '/c:chartSpace/c:chart/c:autoTitleDeleted')
$titleOverlay = if ($null -eq $title) {
    # This exact fixture's implicit automatic title is independently known to occupy layout space.
    $false
} else {
    $overlay = Node '/c:chartSpace/c:chart/c:title/c:overlay'
    if ($null -eq $overlay) { $null } else { BooleanValue $overlay }
}
$colors = BooleanValue (Node '//c:barChart/c:varyColors')
$categories = @($document.SelectNodes('//c:barChart/c:ser/c:cat/c:strRef/c:strCache/c:pt/c:v', $namespaces) | ForEach-Object InnerText)
$values = @($document.SelectNodes('//c:barChart/c:ser/c:val/c:numRef/c:numCache/c:pt/c:v', $namespaces) | ForEach-Object { [Xml.XmlConvert]::ToDouble($_.InnerText) })
$name = (Node '//c:barChart/c:ser/c:tx/c:strRef/c:strCache/c:pt/c:v').InnerText
$minimum = (Node '//c:valAx/c:scaling/c:min').GetAttribute('val')
$checks = @(
    [pscustomobject]@{ Name = 'Automatic title retains plot-free layout'; Passed = $titleOverlay -eq $expected.automaticTitleOverlaysPlot -and ($null -ne $title -or $automaticTitleDeleted -ne $true); Expected = $expected.automaticTitleOverlaysPlot; Actual = $titleOverlay }
    [pscustomobject]@{ Name = 'Authored category color behavior'; Passed = $colors -eq $expected.categoryColors; Expected = $expected.categoryColors; Actual = $colors }
    [pscustomobject]@{ Name = 'Native categories'; Passed = ($categories -join '|') -ceq ($expected.categories -join '|'); Expected = $expected.categories; Actual = $categories }
    [pscustomobject]@{ Name = 'Native series name'; Passed = $name -ceq $expected.seriesName; Expected = $expected.seriesName; Actual = $name }
    [pscustomobject]@{ Name = 'Native series values'; Passed = ($values -join '|') -ceq ($expected.values -join '|'); Expected = $expected.values; Actual = $values }
    [pscustomobject]@{ Name = 'Explicit value-axis minimum'; Passed = [Xml.XmlConvert]::ToDouble($minimum) -eq $expected.valueAxisMinimum; Expected = $expected.valueAxisMinimum; Actual = $minimum }
)
$failed = @($checks | Where-Object { -not $_.Passed })
[pscustomobject]@{
    Input = (Resolve-Path -LiteralPath $Presentation).Path
    FidelityPassed = $failed.Count -eq 0
    Checks = $checks
} | ConvertTo-Json -Depth 6
if ($failed.Count -gt 0) { exit 1 }
exit 0
