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
. (Join-Path $PSScriptRoot 'release-notices.ps1')

$layoutResolver = Join-Path $PSScriptRoot 'resolve-project-layout.ps1'
$generator = Join-Path $PSScriptRoot 'generate-product-catalog.ps1'
$layout = & $layoutResolver -RepositoryRoot $repoRoot
$provenance = Get-RepositoryProvenance `
    -RepositoryRoot $repoRoot
if ($RequireClean -and $provenance.BuildDirty) {
    throw "Release packaging requires a clean project tree: $($provenance.DirtyDetails -join '; ')"
}
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
[void](Get-ArtifactRelativePath -Root $allowedRoot -Path $publishRoot)

& $generator `
    -Check `
    -RepositoryRoot $repoRoot

$restoreArguments = @(
    'restore'
    $layout.LauncherProject
    '--locked-mode'
    '--nologo'
)
& dotnet @restoreArguments
if ($LASTEXITCODE -ne 0) {
    throw "Locked restore failed with exit code $LASTEXITCODE. Run scripts/sync.ps1 after dependency changes."
}

# Existing output is deleted only after its sibling ownership marker proves this build owns it.
Initialize-OwnedArtifactDirectory $publishRoot $allowedRoot 'publish'

$publishArguments = @(
    'publish'
    $layout.LauncherProject
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
        "-p:AsposeCliPublishRuntimeIdentifier=$RuntimeIdentifier"
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
    productId = [string]$layout.Identity.id
    edition = $layout.Edition
    runtimeIdentifier = $publishFlavor
    sourceRevision = $provenance.SourceRevision
    buildDirty = [bool]$provenance.BuildDirty
    enginePackages = @($provenance.EnginePackages)
}
Write-StableJson (Join-Path $publishRoot $script:BuildManifestName) $buildManifest

[xml] $buildDefaults = Get-Content -LiteralPath (Join-Path $repoRoot 'Directory.Build.props') -Raw
$framework = $buildDefaults.SelectSingleNode('/Project/PropertyGroup/TargetFramework').InnerText
$buildOutput = Join-Path $layout.SourceRoot "Aspose.Cli/bin/$Configuration/$framework"
if ($customerPublish) { $buildOutput = Join-Path $buildOutput $RuntimeIdentifier }
$noticeFiles = @(Write-ReleaseNotices -RepositoryRoot $repoRoot -OutputRoot $publishRoot `
    -DependenciesPath (Join-Path $buildOutput $layout.Names.DependencyManifestName) `
    -AssetsPath (Join-Path $layout.SourceRoot 'Aspose.Cli/obj/project.assets.json'))

$publishedFiles = @(Get-ChildItem -LiteralPath $publishRoot -File -Recurse)
$relativeFileNames = @(
    $publishedFiles |
        ForEach-Object {
            Get-ArtifactRelativePath -Root $publishRoot -Path $_.FullName
        }
)
$contamination = @(
    $publishedFiles |
        Where-Object {
            $_.Name.IndexOf('.foss', [StringComparison]::OrdinalIgnoreCase) -ge 0 -or
            $_.Name -ieq 'Aspose.Foundation.dll'
        } |
        ForEach-Object FullName
)
if ($contamination.Count -ne 0) {
    throw "Commercial publish contains FOSS artifacts: $($contamination -join ', ')."
}

if ($customerPublish) {
    $executableName = if ($RuntimeIdentifier.StartsWith('win-', [StringComparison]::Ordinal)) {
        [string]$layout.Names.ExecutableName
    }
    else {
        [string]$layout.Identity.commandName
    }
    $requiredCustomerFiles = @($executableName, $script:BuildManifestName) + $noticeFiles
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
    $declaredVersion = $buildDefaults.SelectSingleNode('/Project/PropertyGroup/Version').InnerText
    if ($capabilities.cliVersion -cne $declaredVersion) {
        throw "Published version '$($capabilities.cliVersion)' does not match declared version '$declaredVersion'."
    }
    if ($capabilities.edition -cne $layout.Edition) {
        throw "Published executable reports edition '$($capabilities.edition)', expected '$($layout.Edition)'."
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
