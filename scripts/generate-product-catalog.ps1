<#
.SYNOPSIS
Internal desired-state reconciler for the authored product catalog.

.DESCRIPTION
The public repository entry point is scripts/sync.ps1. This implementation is
kept separate so lifecycle tests can exercise generation without restoring.
#>
[CmdletBinding()]
param(
    [switch] $Check,

    [string] $RepositoryRoot
)

$ErrorActionPreference = 'Stop'
$repoRoot = if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    Split-Path -Parent $PSScriptRoot
}
else {
    [IO.Path]::GetFullPath($RepositoryRoot)
}
$layoutResolver = Join-Path $repoRoot 'scripts/resolve-project-layout.ps1'
if (-not (Test-Path -LiteralPath $layoutResolver -PathType Leaf)) {
    throw "Edition layout resolver does not exist: $layoutResolver"
}
$layout = & $layoutResolver  -RepositoryRoot $repoRoot
$catalogPath = [IO.Path]::GetFullPath($layout.CatalogPath)

function Normalize-RelativePath {
    param([string] $Path)
    return $Path.Replace('\', '/')
}

function Relative-ToRepository {
    param([string] $Path)
    $rootWithSlash = $repoRoot.TrimEnd('/').TrimEnd('\')
    $rootWithSlash += [IO.Path]::DirectorySeparatorChar
    $base = [Uri]::new($rootWithSlash)
    $target = [Uri]::new([IO.Path]::GetFullPath($Path))
    return [Uri]::UnescapeDataString(
        $base.MakeRelativeUri($target).ToString())
}

function Get-OptionalValue {
    param(
        [object] $Object,
        [string] $Name,
        $Default
    )
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property -or $null -eq $property.Value) {
        return $Default
    }
    return $property.Value
}

function Assert-ExactProperties {
    param(
        [object] $Object,
        [string[]] $Allowed,
        [string[]] $Required,
        [string] $Label
    )
    $actual = @($Object.PSObject.Properties.Name)
    $unknown = @($actual | Where-Object { $_ -notin $Allowed })
    $missing = @($Required | Where-Object { $_ -notin $actual })
    if ($unknown.Count -ne 0) {
        throw "$Label contains unknown field(s): $($unknown -join ', ')."
    }
    if ($missing.Count -ne 0) {
        throw "$Label is missing field(s): $($missing -join ', ')."
    }
}

function Assert-JsonArrayProperty {
    param(
        [object] $Object,
        [string] $Name,
        [string] $Label
    )
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property -or $property.Value -isnot [Array]) {
        throw "$Label field '$Name' must be a JSON array."
    }
}

function Require-Unique {
    param(
        [object[]] $Rows,
        [string] $Property
    )
    $duplicates = @(
        $Rows |
            Group-Object -Property $Property |
            Where-Object Count -gt 1 |
            ForEach-Object Name
    )
    if ($duplicates.Count -ne 0) {
        throw "Catalog field '$Property' contains duplicate values: $($duplicates -join ', ')."
    }
}

function Assert-NoSetDifference {
    param(
        [string[]] $Expected,
        [string[]] $Actual,
        [string] $Message
    )
    $difference = @(Compare-Object @($Expected) @($Actual))
    if ($difference.Count -ne 0) {
        $paths = @($difference | ForEach-Object InputObject)
        throw "$Message $($paths -join ', ')."
    }
}

function Write-Generated {
    param(
        [string] $RelativePath,
        [string] $Content
    )
    $normalized = $Content.Replace("`r`n", "`n").TrimEnd() + "`n"
    $path = Join-Path $repoRoot $RelativePath
    if ($Check) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Generated catalog artifact is missing: $RelativePath"
        }
        $actual = [IO.File]::ReadAllText($path).Replace("`r`n", "`n")
        if ($actual -cne $normalized) {
            throw "Generated catalog artifact is stale: $RelativePath. Run scripts/sync.ps1."
        }
        return
    }

    $directory = Split-Path -Parent $path
    [IO.Directory]::CreateDirectory($directory) | Out-Null
    if (Test-Path -LiteralPath $path -PathType Leaf) {
        $existing = [IO.File]::ReadAllText($path)
        if ($existing.Replace("`r`n", "`n") -ceq $normalized) {
            return
        }
        # Keep the checkout's line endings, so regenerating a region of a CRLF file
        # does not rewrite every other line.
        if ($existing.Contains("`r`n")) {
            $normalized = $normalized.Replace("`n", "`r`n")
        }
    }

    $temporary = Join-Path $directory (
        '.' + [IO.Path]::GetFileName($path) + '.' +
        [Guid]::NewGuid().ToString('N') + '.tmp')
    try {
        [IO.File]::WriteAllText(
            $temporary,
            $normalized,
            [Text.UTF8Encoding]::new($false))
        if (Test-Path -LiteralPath $path -PathType Leaf) {
            $backup = Join-Path $directory (
                '.' + [IO.Path]::GetFileName($path) + '.' +
                [Guid]::NewGuid().ToString('N') + '.bak')
            try {
                [IO.File]::Replace($temporary, $path, $backup)
            }
            finally {
                if (Test-Path -LiteralPath $backup -PathType Leaf) {
                    [IO.File]::Delete($backup)
                }
            }
        }
        else {
            [IO.File]::Move($temporary, $path)
        }
    }
    finally {
        if (Test-Path -LiteralPath $temporary -PathType Leaf) {
            [IO.File]::Delete($temporary)
        }
    }
}

function Get-NormalizedTextHashes {
    param([string] $Path)
    $text = [IO.File]::ReadAllText($Path).
        Replace("`r`n", "`n").
        Replace("`r", "`n")
    $utf8 = [Text.UTF8Encoding]::new($false)
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $lf = [BitConverter]::ToString(
            $sha.ComputeHash($utf8.GetBytes($text))).Replace('-', '')
        $crlf = [BitConverter]::ToString(
            $sha.ComputeHash($utf8.GetBytes($text.Replace("`n", "`r`n")))).
            Replace('-', '')
        return @{ Lf = $lf; CrLf = $crlf }
    }
    finally {
        $sha.Dispose()
    }
}

function Reconcile-GeneratedDirectory {
    param([string[]] $ExpectedNames)
    $relativeDirectory = "eng/generated"
    $directory = Join-Path $repoRoot $relativeDirectory
    if (-not (Test-Path -LiteralPath $directory -PathType Container)) {
        if ($Check) {
            throw "Generated catalog directory is missing: $relativeDirectory"
        }
        return
    }

    $entries = @(Get-ChildItem -LiteralPath $directory -Force)
    foreach ($entry in $entries) {
        if ($entry.PSIsContainer) {
            throw "Unknown directory in generator-owned output is protected: $($entry.FullName)"
        }
        if ($entry.Name -in $ExpectedNames) {
            continue
        }
        throw "Unknown file in generator-owned output is protected: $($entry.FullName)"
    }
}

if (-not (Test-Path -LiteralPath $catalogPath -PathType Leaf)) {
    throw "Product catalog does not exist: $catalogPath"
}
$source = Get-Content -LiteralPath $catalogPath -Raw | ConvertFrom-Json
Assert-ExactProperties `
    $source `
    @('schemaVersion', 'products') `
    @('schemaVersion', 'products') `
    'Product catalog'
Assert-JsonArrayProperty $source 'products' 'Product catalog'
if ([int] $source.schemaVersion -ne 1) {
    throw "Product catalog schemaVersion must be 1: $catalogPath"
}

$requiredFields = @(
    'id',
    'productName',
    'engine',
    'sdkVersion'
)
$allowedFields = @(
    $requiredFields + @(
        'displayName',
        'sdkPackageId',
        'supplementalPackages'))
$products = @(
    @($source.products) |
        ForEach-Object {
            $row = $_
            $unknownFields = @(
                $row.PSObject.Properties.Name |
                    Where-Object { $_ -notin $allowedFields })
            if ($unknownFields.Count -ne 0) {
                throw "Product row contains unknown field(s): $($unknownFields -join ', ')."
            }
            foreach ($field in $requiredFields) {
                $value = Get-OptionalValue $row $field $null
                if ([string]::IsNullOrWhiteSpace([string] $value)) {
                    throw "Product row is missing catalog field '$field'."
                }
            }

            $id = [string] $row.id
            if ($id -cnotmatch '^[a-z0-9][a-z0-9-]*$') {
                throw "Invalid product id '$id'."
            }
            $productName = [string] $row.productName
            if ($productName -cnotmatch '^[A-Z][A-Za-z0-9]*$') {
                throw "Product '$id' has invalid productName '$productName'."
            }

            $projectName = "Aspose.Cli.Product.$productName"
            $project = Join-Path $layout.SourceRoot "$projectName/$projectName.csproj"
            $testProjectName = "$projectName.Tests"
            $testRoot = Join-Path $layout.TestRoot $testProjectName
            $testProject = Join-Path $testRoot "$testProjectName.csproj"
            foreach ($requiredPath in @(
                $project,
                $testProject)) {
                if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
                    throw "Product '$id' slice is incomplete: $requiredPath"
                }
            }

            $projectText = [IO.File]::ReadAllText($project)
            if ($projectText.Contains('<AsposeProductId>') -or
                $projectText.Contains('<PackageId>')) {
                throw "Product '$id' duplicates generated catalog identity in its project."
            }
            $moduleName = "$($productName)Module"
            $moduleType = "$projectName.$moduleName"
            $moduleSource = Join-Path (Split-Path -Parent $project) "$moduleName.cs"
            if (-not (Test-Path -LiteralPath $moduleSource -PathType Leaf)) {
                throw "Product '$id' module source does not match productName: $moduleSource"
            }
            $moduleText = [IO.File]::ReadAllText($moduleSource)
            if ($moduleText.Contains('[assembly: ProductModule')) {
                throw "Product '$id' duplicates the generated ProductModule attribute."
            }
            foreach ($requiredUse in @(
                'Id = ProductBuildMetadata.ProductId',
                'DisplayName = ProductBuildMetadata.DisplayName',
                'ProductBuildMetadata.EngineName',
                'ProductBuildMetadata.SdkVersion')) {
                if (-not $moduleText.Contains($requiredUse)) {
                    throw "Product '$id' module does not consume generated metadata '$requiredUse'."
                }
            }

            $supplemental = @()
            $supplementalObject = Get-OptionalValue $row 'supplementalPackages' $null
            if ($null -ne $supplementalObject) {
                $supplemental = @(
                    $supplementalObject.PSObject.Properties |
                        ForEach-Object {
                            if ([string]::IsNullOrWhiteSpace($_.Name) -or
                                [string]::IsNullOrWhiteSpace([string] $_.Value)) {
                                throw "Product '$id' has an invalid supplemental package."
                            }
                            [pscustomobject] [ordered] @{
                                packageId = $_.Name
                                version = [string] $_.Value
                            }
                        } |
                        Sort-Object packageId)
            }
            # The catalog is the product's package roster: its project references exactly the
            # SDK package and the supplemental packages, and the versions come from the catalog.
            [xml] $projectXml = $projectText
            $referenced = @(
                $projectXml.SelectNodes('//*[local-name()="PackageReference"]') |
                    ForEach-Object { [string] $_.GetAttribute('Include') })
            $declared = @(
                @([string] (Get-OptionalValue $row 'sdkPackageId' '')) +
                    @($supplemental | ForEach-Object { [string] $_.packageId }) |
                    Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
            $missingReferences = @($declared | Where-Object { $_ -notin $referenced })
            $unexpectedReferences = @($referenced | Where-Object { $_ -notin $declared })
            if ($missingReferences.Count -ne 0 -or $unexpectedReferences.Count -ne 0) {
                throw "Product '$id' package references drift from eng/products.json (missing: [$($missingReferences -join ', ')], not in the catalog: [$($unexpectedReferences -join ', ')]). Keep supplementalPackages and $projectName.csproj in step."
            }
            $displayName = [string] (
                Get-OptionalValue $row 'displayName' $productName)

            [pscustomobject] [ordered] @{
                id = $id
                productName = $productName
                displayName = $displayName
                projectName = $projectName
                projectPath = Relative-ToRepository $project
                testProjectPath = Relative-ToRepository $testProject
                assemblyName = $projectName
                moduleType = $moduleType
                engineName = [string] $row.engine
                sdkPackageId = [string] (
                    Get-OptionalValue $row 'sdkPackageId' '')
                sdkVersion = [string] $row.sdkVersion
                supplementalPackageVersions = $supplemental
                resourceNamespace = "v2/$id"
            }
        } |
        Sort-Object id)
if ($products.Count -eq 0) {
    throw 'The active product catalog is empty.'
}
foreach ($field in @(
    'id', 'projectName', 'projectPath', 'testProjectPath', 'assemblyName',
    'moduleType', 'resourceNamespace')) {
    Require-Unique $products $field
}

$expectedSourceDirectories = @(
    $products |
        ForEach-Object { Normalize-RelativePath (Split-Path $_.projectPath -Parent) } |
        Sort-Object -Unique)
$actualSourceDirectories = @(
    Get-ChildItem -LiteralPath $layout.SourceRoot `
        -Directory -Filter 'Aspose.Cli.Product.*' |
        ForEach-Object { Normalize-RelativePath (Relative-ToRepository $_.FullName) } |
        Sort-Object -Unique)
Assert-NoSetDifference $expectedSourceDirectories $actualSourceDirectories `
    'Authored product source catalog drift:'

$expectedTestDirectories = @(
    $products |
        ForEach-Object { Normalize-RelativePath (Split-Path $_.testProjectPath -Parent) } |
        Sort-Object -Unique)
$actualTestDirectories = @(
    Get-ChildItem -LiteralPath $layout.TestRoot `
        -Directory -Filter 'Aspose.Cli.Product.*.Tests' |
        ForEach-Object { Normalize-RelativePath (Relative-ToRepository $_.FullName) } |
        Sort-Object -Unique)
Assert-NoSetDifference $expectedTestDirectories $actualTestDirectories `
    'Authored product test catalog drift:'

$declaredPackages = @(
    foreach ($product in $products) {
        if (-not [string]::IsNullOrWhiteSpace($product.sdkPackageId)) {
            [pscustomobject] @{
                packageId = $product.sdkPackageId
                version = $product.sdkVersion
                product = $product.id
            }
        }
        foreach ($item in $product.supplementalPackageVersions) {
            [pscustomobject] @{
                packageId = $item.packageId
                version = $item.version
                product = $product.id
            }
        }
    })
foreach ($group in $declaredPackages | Group-Object packageId) {
    $versions = @($group.Group.version | Sort-Object -Unique)
    if ($versions.Count -ne 1) {
        $owners = @($group.Group | ForEach-Object { "$($_.product)=$($_.version)" })
        throw "Product package '$($group.Name)' has conflicting versions: $($owners -join ', ')."
    }
}
$productPackageVersions = @(
    $declaredPackages |
        Group-Object packageId |
        ForEach-Object {
            [pscustomobject] @{
                packageId = $_.Name
                version = [string] $_.Group[0].version
            }
        } |
        Sort-Object packageId)

$allTestProjects = @(
    @($layout.TestRoot) |
        Where-Object { Test-Path -LiteralPath $_ -PathType Container } |
        ForEach-Object {
            Get-ChildItem -LiteralPath $_ -Recurse -Filter '*.csproj' -File
        } |
        ForEach-Object { Relative-ToRepository $_.FullName } |
        Sort-Object -Unique)


$distributionProps = [Text.StringBuilder]::new()
[void]$distributionProps.AppendLine('<!-- <auto-generated /> DO NOT EDIT. Source: eng/distribution.json -->')
[void]$distributionProps.AppendLine('<Project><PropertyGroup>')
$distributionCode = [Text.StringBuilder]::new()
[void]$distributionCode.AppendLine('// <auto-generated /> Source: eng/distribution.json')
[void]$distributionCode.AppendLine('namespace Aspose.Cli.Sdk;')
[void]$distributionCode.AppendLine('/// <summary>Fixed build-time identity of this CLI distribution.</summary>')
[void]$distributionCode.AppendLine('public static class DistributionInfo')
[void]$distributionCode.AppendLine('{')
foreach ($name in @('id','commandName','displayName','edition','environmentVariablePrefix','skillPrefix','schemaBaseUri','configurationDirectoryName','installDirectory')) {
    $value = [string]$layout.Identity.$name
    $propertyName = [char]::ToUpperInvariant($name[0]) + $name.Substring(1)
    $escaped = [Security.SecurityElement]::Escape($value)
    [void]$distributionProps.AppendLine("  <AsposeCli$propertyName>$escaped</AsposeCli$propertyName>")
    $literal = ConvertTo-Json -InputObject $value -Compress
    [void]$distributionCode.AppendLine("    public const string $propertyName = $literal;")
}
[void]$distributionProps.AppendLine('</PropertyGroup></Project>')
[void]$distributionCode.AppendLine('}')
Write-Generated 'eng/generated/DistributionBuild.props' $distributionProps.ToString()
Write-Generated 'eng/generated/DistributionInfo.g.cs' $distributionCode.ToString()


$installerPath = Join-Path $repoRoot 'install.ps1'
if (Test-Path -LiteralPath $installerPath -PathType Leaf) {
    $identity = $layout.Identity
    $names = $layout.Names
    $settings = [Collections.Generic.List[string]]::new()
    $settings.Add('# <generated-distribution-identity>')
    foreach ($entry in ([ordered]@{
        ProductId = [string]$identity.id
        CommandName = [string]$identity.commandName
        DisplayName = [string]$identity.displayName
        ExecutableName = [string]$names.ExecutableName
        MarkerName = [string]$names.MarkerName
        PayloadManifestName = [string]$names.PayloadManifestName
        BuildManifestName = [string]$names.BuildManifestName
        PackageSignatureManifestName = 'PACKAGE-SIGNATURE.json'
        PackageSignatureName = 'PACKAGE-SIGNATURE.sig'
        SkillManifestProductId = [string]$names.SkillManifestProductId
        DefaultInstallDirectory = [string]$names.WindowsInstallDirectory
        ConfigurationDirectoryName = [string]$identity.configurationDirectoryName
        ConfigurationOwnerName = [string]$names.ConfigurationOwnerName
        EnvironmentVariablePrefix = [string]$identity.environmentVariablePrefix
    }).GetEnumerator()) {
        $literal = "'" + ([string]$entry.Value).Replace("'","''") + "'"
        $settings.Add('$script:' + $entry.Key + ' = ' + $literal)
    }
    $settings.Add('$script:Utf8 = [Text.UTF8Encoding]::new($false)')
    $settings.Add('$script:AllowedEditions = @(' + "'" + $identity.edition + "'" + ')')
    $skillNames = @($products | ForEach-Object { "'" + $identity.skillPrefix + $_.id + "'" })
    $settings.Add('$script:AllowedSkills = @(' + ($skillNames -join ', ') + ')')
    $productIds = @($products | ForEach-Object { "'" + $_.id + "'" })
    $settings.Add('$script:AllowedLicenseProducts = @(' + ($productIds -join ', ') + ')')
    $settings.Add('# </generated-distribution-identity>')
    $installerText = [IO.File]::ReadAllText($installerPath).Replace("`r`n","`n")
    $pattern = '(?s)# <generated-distribution-identity>.*?# </generated-distribution-identity>'
    if (-not [Regex]::IsMatch($installerText,$pattern)) { throw 'Installer generated identity region is missing.' }
    $identityText = $settings -join "`n"
    $updated = [Regex]::Replace($installerText,$pattern,[Text.RegularExpressions.MatchEvaluator]{param($match) $identityText})
    Write-Generated 'install.ps1' $updated
}

$catalogHashes = Get-NormalizedTextHashes $catalogPath
$distributionHashes = Get-NormalizedTextHashes $layout.DistributionPath
$catalogSource = Normalize-RelativePath (Relative-ToRepository $catalogPath)
$generatedRelativeRoot = "eng/generated"
$repositoryBuild = [Text.StringBuilder]::new()
[void] $repositoryBuild.AppendLine("<!-- <auto-generated /> DO NOT EDIT. Source: $catalogSource -->")
[void] $repositoryBuild.AppendLine('<Project>')
foreach ($product in $products) {
    [void] $repositoryBuild.AppendLine("  <PropertyGroup Condition=`"'`$(MSBuildProjectName)' == '$($product.projectName)'`">")
    [void] $repositoryBuild.AppendLine("    <AsposeProductId>$($product.id)</AsposeProductId>")
    [void] $repositoryBuild.AppendLine("    <AsposeProductNamespace>$($product.projectName)</AsposeProductNamespace>")
    [void] $repositoryBuild.AppendLine("    <AsposeProductDisplayName>$($product.displayName)</AsposeProductDisplayName>")
    [void] $repositoryBuild.AppendLine("    <AsposeProductEngineName>$($product.engineName)</AsposeProductEngineName>")
    [void] $repositoryBuild.AppendLine("    <AsposeProductSdkPackageId>$($product.sdkPackageId)</AsposeProductSdkPackageId>")
    [void] $repositoryBuild.AppendLine("    <AsposeProductSdkVersion>$($product.sdkVersion)</AsposeProductSdkVersion>")
    [void] $repositoryBuild.AppendLine("    <AsposeProductModuleType>$($product.moduleType)</AsposeProductModuleType>")
    [void] $repositoryBuild.AppendLine("    <AsposeProductAssemblyName>$($product.assemblyName)</AsposeProductAssemblyName>")
    [void] $repositoryBuild.AppendLine("    <AsposeProductResourceNamespace>$($product.resourceNamespace)</AsposeProductResourceNamespace>")
    [void] $repositoryBuild.AppendLine('  </PropertyGroup>')
}
[void] $repositoryBuild.AppendLine('  <PropertyGroup>')
[void] $repositoryBuild.AppendLine("    <GeneratedProductsLfSha256>$($catalogHashes.Lf)</GeneratedProductsLfSha256>")
[void] $repositoryBuild.AppendLine("    <GeneratedProductsCrLfSha256>$($catalogHashes.CrLf)</GeneratedProductsCrLfSha256>")
[void] $repositoryBuild.AppendLine("    <GeneratedDistributionLfSha256>$($distributionHashes.Lf)</GeneratedDistributionLfSha256>")
[void] $repositoryBuild.AppendLine("    <GeneratedDistributionCrLfSha256>$($distributionHashes.CrLf)</GeneratedDistributionCrLfSha256>")
[void] $repositoryBuild.AppendLine('  </PropertyGroup>')
[void] $repositoryBuild.AppendLine('  <Target Name="ValidateRepositoryProjectionSources" BeforeTargets="PrepareForBuild">')
[void] $repositoryBuild.AppendLine("    <Error Condition=`"'`$(GeneratedProductsLfSha256)' == '' Or '`$(GeneratedProductsCrLfSha256)' == '' Or '`$(GeneratedDistributionLfSha256)' == '' Or '`$(GeneratedDistributionCrLfSha256)' == ''`" Text=`"Generated repository projections are missing. Run scripts/sync.ps1 .`" />")
[void] $repositoryBuild.AppendLine('    <GetFileHash Files="$(MSBuildThisFileDirectory)..\products.json" Algorithm="SHA256"><Output TaskParameter="Items" ItemName="_CurrentProductsHash" /></GetFileHash>')
[void] $repositoryBuild.AppendLine('    <GetFileHash Files="$(MSBuildThisFileDirectory)..\distribution.json" Algorithm="SHA256"><Output TaskParameter="Items" ItemName="_CurrentDistributionHash" /></GetFileHash>')
[void] $repositoryBuild.AppendLine('    <PropertyGroup>')
[void] $repositoryBuild.AppendLine('      <CurrentProductsSha256>@(_CurrentProductsHash->''%(FileHash)'')</CurrentProductsSha256>')
[void] $repositoryBuild.AppendLine('      <CurrentDistributionSha256>@(_CurrentDistributionHash->''%(FileHash)'')</CurrentDistributionSha256>')
[void] $repositoryBuild.AppendLine('    </PropertyGroup>')
[void] $repositoryBuild.AppendLine("    <Error Condition=`"'`$(CurrentProductsSha256)' != '`$(GeneratedProductsLfSha256)' And '`$(CurrentProductsSha256)' != '`$(GeneratedProductsCrLfSha256)'`" Text=`"Generated repository projections are stale. Run scripts/sync.ps1 .`" />")
[void] $repositoryBuild.AppendLine("    <Error Condition=`"'`$(CurrentDistributionSha256)' != '`$(GeneratedDistributionLfSha256)' And '`$(CurrentDistributionSha256)' != '`$(GeneratedDistributionCrLfSha256)'`" Text=`"Generated distribution identity projections are stale. Run scripts/sync.ps1 .`" />")
[void] $repositoryBuild.AppendLine('  </Target>')
[void] $repositoryBuild.AppendLine('</Project>')
Write-Generated "$generatedRelativeRoot/RepositoryBuild.props" $repositoryBuild.ToString()

$composition = [Text.StringBuilder]::new()
[void] $composition.AppendLine("<!-- <auto-generated /> DO NOT EDIT. Source: $catalogSource -->")
[void] $composition.AppendLine('<Project>')
[void] $composition.AppendLine('  <ItemGroup Label="Generated active product catalog">')
foreach ($product in $products) {
    [void] $composition.AppendLine("    <ProductPackage Include=`"$($product.id)`" ProductName=`"$($product.productName)`" ProjectPath=`"`$(MSBuildThisFileDirectory)..\..\$($product.projectPath.Replace('/', '\'))`" />")
}
[void] $composition.AppendLine('  </ItemGroup>')
[void] $composition.AppendLine('  <ItemGroup>')
[void] $composition.AppendLine('    <ProjectReference Include="@(ProductPackage->''%(ProjectPath)'')" />')
[void] $composition.AppendLine('  </ItemGroup>')
[void] $composition.AppendLine('</Project>')
Write-Generated "$generatedRelativeRoot/ProductComposition.props" $composition.ToString()

$versions = [Text.StringBuilder]::new()
[void] $versions.AppendLine("<!-- <auto-generated /> DO NOT EDIT. Source: $catalogSource -->")
[void] $versions.AppendLine('<Project>')
[void] $versions.AppendLine('  <ItemGroup>')
foreach ($package in $productPackageVersions) {
    [void] $versions.AppendLine("    <PackageVersion Include=`"$($package.packageId)`" Version=`"$($package.version)`" />")
}
[void] $versions.AppendLine('  </ItemGroup>')
[void] $versions.AppendLine('</Project>')
Write-Generated "$generatedRelativeRoot/ProductVersions.props" $versions.ToString()

$solution = [Text.StringBuilder]::new()
[void] $solution.AppendLine("<!-- <auto-generated /> DO NOT EDIT. Source: $catalogSource -->")
[void] $solution.AppendLine('<Solution>')
[void] $solution.AppendLine('  <Folder Name="/platform/">')
foreach ($project in @(
    'src/Aspose.Cli.Host/Aspose.Cli.Host.csproj',
    'src/Aspose.Cli.Sdk/Aspose.Cli.Sdk.csproj',
    'src/Aspose.Cli.Sdk.Analyzers/Aspose.Cli.Sdk.Analyzers.csproj')) {
    [void] $solution.AppendLine("    <Project Path=`"$project`" />")
}
[void] $solution.AppendLine("    <Project Path=`"src/Aspose.Cli/Aspose.Cli.csproj`" />")
[void] $solution.AppendLine('  </Folder>')
[void] $solution.AppendLine('  <Folder Name="/products/">')
foreach ($product in $products) {
    [void] $solution.AppendLine("    <Project Path=`"$($product.projectPath)`" />")
}
[void] $solution.AppendLine('  </Folder>')
[void] $solution.AppendLine('  <Folder Name="/tests/">')
foreach ($project in $allTestProjects) {
    [void] $solution.AppendLine("    <Project Path=`"$project`" />")
}
[void] $solution.AppendLine('  </Folder>')
[void] $solution.AppendLine('</Solution>')
Write-Generated $layout.Identity.solutionName $solution.ToString()

$expectedGenerated = @(
    'DistributionBuild.props',
    'DistributionInfo.g.cs',
    'RepositoryBuild.props',
    'ProductComposition.props',
    'ProductVersions.props')
Reconcile-GeneratedDirectory $expectedGenerated

Write-Host "$($products.Count) active products validated; generated catalog artifacts are current."
