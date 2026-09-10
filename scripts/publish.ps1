<#
.SYNOPSIS
Publishes one isolated Aspose CLI edition and scans its artifacts.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',

    [string] $RuntimeIdentifier,

    [string] $OutputRoot,

    [switch] $RequireClean
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'release-common.ps1')

function Get-PublishRelativePath {
    param(
        [Parameter(Mandatory)][string] $Root,
        [Parameter(Mandatory)][string] $Path
    )

    $fullRoot = [IO.Path]::GetFullPath($Root)
    $rootPrefix = $fullRoot.TrimEnd(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    $fullPath = [IO.Path]::GetFullPath($Path)
    if (-not $fullPath.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Publish file escaped its output directory: $fullPath"
    }

    return $fullPath.Substring($rootPrefix.Length).Replace('\', '/')
}

$layoutResolver = Join-Path $PSScriptRoot 'resolve-project-layout.ps1'
$generator = Join-Path $PSScriptRoot 'generate-product-catalog.ps1'
$layout = & $layoutResolver  -RepositoryRoot $repoRoot
$provenance = Get-RepositoryProvenance `
    -RepositoryRoot $repoRoot
if ($RequireClean -and $provenance.BuildDirty) {
    $details = @($provenance.DirtyDetails)
    if ($provenance.GitlinkOffset) { $details += 'one or more external engine HEADs differ from their recorded gitlinks' }
    throw "Release packaging requires a clean tracked tree and exact engine gitlinks: $($details -join '; ')"
}
$buildArguments = @($layout.BuildArguments)
$productCatalog = Get-Content -LiteralPath $layout.CatalogPath -Raw | ConvertFrom-Json
$productCount = @($productCatalog.products).Count
$customerPublish = -not [string]::IsNullOrWhiteSpace($RuntimeIdentifier)
if ($customerPublish) {
    [xml] $launcherProject = Get-Content -LiteralPath $layout.LauncherProject -Raw
    $supportedRuntimeIdentifiers = @(
        $launcherProject.Project.PropertyGroup |
            ForEach-Object { [string] $_.RuntimeIdentifiers } |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
            ForEach-Object { $_ -split ';' } |
            ForEach-Object { $_.Trim() } |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    )
    if ($RuntimeIdentifier -notin $supportedRuntimeIdentifiers) {
        throw "Runtime '$RuntimeIdentifier' is not verified for every $($layout.Edition) product. Supported customer runtimes: $($supportedRuntimeIdentifiers -join ', ')."
    }
}
$publishFlavor = if ([string]::IsNullOrWhiteSpace($RuntimeIdentifier)) {
    'portable'
}
else {
    $RuntimeIdentifier
}
$defaultOutput = Join-Path $repoRoot "artifacts/publish/$publishFlavor"
$publishRoot = [IO.Path]::GetFullPath(
    $(if ([string]::IsNullOrWhiteSpace($OutputRoot)) { $defaultOutput } else { $OutputRoot }))
$allowedRoot = [IO.Path]::GetFullPath(
    (Join-Path $repoRoot "artifacts/publish"))
$allowedPrefix = $allowedRoot.TrimEnd(
    [IO.Path]::DirectorySeparatorChar,
    [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if (-not $publishRoot.StartsWith($allowedPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Publish output must be inside the edition artifact root: $allowedRoot"
}

& $generator `
    -Check `
    -RepositoryRoot $repoRoot `
    -OutputRoot $repoRoot

$restoreArguments = @(
    'restore'
    $layout.LauncherProject
    $buildArguments
    '--locked-mode'
    '--nologo'
)
& dotnet @restoreArguments
if ($LASTEXITCODE -ne 0) {
    throw "Locked restore failed with exit code $LASTEXITCODE. Run scripts/sync.ps1  after dependency changes."
}

$freePackageNotices = @()
$null

# Existing output is deleted only after its sibling ownership marker proves this build owns it.
Initialize-OwnedArtifactDirectory $publishRoot $allowedRoot 'publish'

$publishArguments = @(
    'publish'
    $layout.LauncherProject
    $buildArguments
    '--configuration'
    $Configuration
    '--no-restore'
    '--nologo'
    '--output'
    $publishRoot
    '--self-contained'
    $(if ($customerPublish) { 'true' } else { 'false' })
    "-p:SourceRevision=$($provenance.SourceRevision)"
    "-p:BuildDirty=$($provenance.BuildDirty.ToString().ToLowerInvariant())"
    "-p:SourceRevisionId=$($provenance.SourceRevision)"
    "-p:RepositoryCommit=$($provenance.SourceRevision)"
)
if ($customerPublish) {
    $publishArguments += @(
        '--runtime'
        $RuntimeIdentifier
        '-p:PublishSingleFile=true'
        '-p:IncludeNativeLibrariesForSelfExtract=true'
        '-p:EnableCompressionInSingleFile=true'
        '-p:PublishTrimmed=false'
        '-p:DebugType=None'
        '-p:DebugSymbols=false'
    )
}
& dotnet @publishArguments
if ($LASTEXITCODE -ne 0) {
    throw "Publish failed with exit code $LASTEXITCODE."
}

if ($customerPublish -and $RuntimeIdentifier.StartsWith('win-', [StringComparison]::Ordinal)) {
    # Aspose.Slides.NET6.CrossPlatform copies every OS native drawing library as
    # content. The Windows libraries are bundled into the single file; remove
    # the unrelated Unix assets so a win-x64 customer does not download 160 MB
    # of libraries that cannot execute on their machine.
    $foreignSlidesAssets = @(
        'libaspose.slides.drawing.capi_aarch64_libstdcpp_libc2.39.so'
        'libaspose.slides.drawing.capi_appleclang_arm64.dylib'
        'libaspose.slides.drawing.capi_appleclang_x86_64.dylib'
        'libaspose.slides.drawing.capi_x86_64_libstdcpp_libc2.23.so'
    )
    foreach ($name in $foreignSlidesAssets) {
        $path = Join-Path $publishRoot $name
        if (Test-Path -LiteralPath $path -PathType Leaf) {
            Remove-Item -LiteralPath $path -Force
        }
    }
}

if ($customerPublish) {
    # Project and package references can copy API documentation and debug
    # symbols even when the entry project disables them. They are not runtime
    # dependencies and would defeat the single-file customer contract.
    Get-ChildItem -LiteralPath $publishRoot -File |
        Where-Object { $_.Extension -in @('.pdb', '.xml') } |
        Remove-Item -Force
}

$buildManifest = [ordered]@{
    schemaVersion = 1
    productId = 'aspose-cli'
    edition = $layout.Slug
    runtimeIdentifier = $publishFlavor
    sourceRevision = $provenance.SourceRevision
    buildDirty = [bool]$provenance.BuildDirty
    enginePackages = @($provenance.EnginePackages)
}
Write-StableJson (Join-Path $publishRoot $script:BuildManifestName) $buildManifest

$publishedFiles = @(Get-ChildItem -LiteralPath $publishRoot -File -Recurse)
$fileNames = @($publishedFiles | ForEach-Object Name)
$relativeFileNames = @(
    $publishedFiles |
        ForEach-Object {
            Get-PublishRelativePath -Root $publishRoot -Path $_.FullName
        }
)
$commercialAssemblies = @(
    'Aspose.Cells.dll'
    'Aspose.PDF.dll'
    'Aspose.PDF.Drawing.dll'
    'Aspose.Slides.dll'
    'Aspose.Words.dll'
)
$fossAssemblies = @(
    'Aspose.Cells.FOSS.dll'
    'Aspose.Pdf.Foss.dll'
    'Aspose.Slides.Foss.dll'
    'Aspose.Words.FOSS.dll'
    'Aspose.Foundation.dll'
)
$forbidden = $(
    $fossAssemblies
)
$contamination = @(
    $fileNames |
        Where-Object { $_ -in $forbidden } |
        Sort-Object -Unique
)
if ($contamination.Count -ne 0) {
    throw "$($layout.Edition) publish contains assemblies from the other edition: $($contamination -join ', ')."
}

$(
    $fossFiles = @(
        Get-ChildItem -LiteralPath $publishRoot -File -Recurse |
            Where-Object {
                $_.Name.IndexOf(
                    '.foss',
                    [StringComparison]::OrdinalIgnoreCase) -ge 0
            } |
            ForEach-Object FullName
    )
    if ($fossFiles.Count -ne 0) {
        throw "Commercial publish contains FOSS artifacts: $($fossFiles -join ', ')."
    }
)

if ($customerPublish) {
    $executableName = if ($RuntimeIdentifier.StartsWith('win-', [StringComparison]::Ordinal)) {
        'aspose-cli.exe'
    }
    else {
        'aspose-cli'
    }
    $requiredCustomerFiles = @($executableName, $script:BuildManifestName)
    $null
    $missingCustomerFiles = @(
        $requiredCustomerFiles |
            Where-Object { $_ -cnotin $relativeFileNames }
    )
    if ($missingCustomerFiles.Count -ne 0) {
        throw "Customer publish is missing required files: $($missingCustomerFiles -join ', ')."
    }
    $unexpectedCustomerFiles = @(
        $relativeFileNames |
            Where-Object { $_ -cnotin $requiredCustomerFiles }
    )
    if ($unexpectedCustomerFiles.Count -ne 0) {
        throw "Customer publish contains unexpected loose files: $($unexpectedCustomerFiles -join ', ')."
    }

    $null

    $executable = Join-Path $publishRoot $executableName
    & $executable --version | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Published executable failed --version with exit code $LASTEXITCODE."
    }
    $capabilitiesText = & $executable capabilities --output json
    if ($LASTEXITCODE -ne 0) {
        throw "Published executable failed capabilities with exit code $LASTEXITCODE."
    }
    $capabilities = ($capabilitiesText -join [Environment]::NewLine) | ConvertFrom-Json
    if ($capabilities.edition -cne $layout.Slug) {
        throw "Published executable reports edition '$($capabilities.edition)', expected '$($layout.Slug)'."
    }
    if ($capabilities.sourceRevision -cne $provenance.SourceRevision -or
        [bool]$capabilities.buildDirty -ne [bool]$provenance.BuildDirty) {
        throw 'Published executable provenance does not match the captured repository provenance.'
    }
    $skillsText = & $executable skill list --output json
    if ($LASTEXITCODE -ne 0) {
        throw "Published executable failed skill discovery with exit code $LASTEXITCODE."
    }
    $skills = ($skillsText -join [Environment]::NewLine) | ConvertFrom-Json
    if (@($skills.skills).Count -ne $productCount) {
        throw "Published executable reports $(@($skills.skills).Count) Skills, expected $productCount."
    }
}

$kind = if ($customerPublish) { "self-contained $RuntimeIdentifier customer" } else { 'framework-dependent portable' }
Write-Host "$($layout.Edition) $kind publish at $($provenance.SourceRevision) passed artifact isolation checks: $publishRoot"
