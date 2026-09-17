[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ArtifactRoot,

    [Parameter(Mandatory = $true)]
    [string]$ManifestPath,

    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

if (-not (Test-Path $ArtifactRoot -PathType Container)) {
    throw "Artifact root '$ArtifactRoot' does not exist."
}

if (-not (Test-Path $ManifestPath -PathType Leaf)) {
    throw "Release manifest '$ManifestPath' does not exist."
}

if (Test-Path $OutputDirectory) {
    if (@(Get-ChildItem $OutputDirectory -Force).Count -ne 0) {
        throw "Output directory '$OutputDirectory' must be empty."
    }
}
else {
    New-Item $OutputDirectory -ItemType Directory | Out-Null
}

$manifest = Get-Content $ManifestPath -Raw | ConvertFrom-Json
$expectedAssets = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)

foreach ($package in $manifest.packages.PSObject.Properties) {
    foreach ($platform in $package.Value) {
        [void]$expectedAssets.Add("$($package.Name)-$platform.zip")
        if (-not $platform.StartsWith("win", [System.StringComparison]::Ordinal)) {
            [void]$expectedAssets.Add("$($package.Name)-$platform.tar.gz")
        }
    }
}

$candidateAssets = @(
    Get-ChildItem $ArtifactRoot -Recurse -File |
        Where-Object { $_.Name.EndsWith(".zip") -or $_.Name.EndsWith(".tar.gz") }
)
$assetsByName = $candidateAssets | Group-Object Name
$duplicates = @($assetsByName | Where-Object Count -GT 1)
if ($duplicates.Count -ne 0) {
    $duplicateNames = ($duplicates.Name | Sort-Object) -join ", "
    throw "Duplicate release assets were produced: $duplicateNames"
}

$actualAssets = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
foreach ($assetName in $assetsByName.Name) {
    [void]$actualAssets.Add($assetName)
}
$missingAssets = @($expectedAssets | Where-Object { -not $actualAssets.Contains($_) } | Sort-Object)
$unexpectedAssets = @($actualAssets | Where-Object { -not $expectedAssets.Contains($_) } | Sort-Object)

if ($missingAssets.Count -ne 0 -or $unexpectedAssets.Count -ne 0) {
    $message = @("Release asset validation failed.")
    if ($missingAssets.Count -ne 0) {
        $message += "Missing: $($missingAssets -join ', ')"
    }
    if ($unexpectedAssets.Count -ne 0) {
        $message += "Unexpected: $($unexpectedAssets -join ', ')"
    }
    throw $message -join [Environment]::NewLine
}

foreach ($asset in $candidateAssets) {
    Copy-Item $asset.FullName (Join-Path $OutputDirectory $asset.Name)
}

$checksumLines = foreach ($asset in Get-ChildItem $OutputDirectory -File | Sort-Object Name) {
    $hash = (Get-FileHash $asset.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $($asset.Name)"
}
$checksumLines | Set-Content (Join-Path $OutputDirectory "SHA256SUMS") -Encoding ascii

Write-Host "Validated and assembled $($candidateAssets.Count) release assets."
