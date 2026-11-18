[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ExpectedVersion,

    [Parameter(Mandatory = $true)]
    [string]$VersionFile,

    [Parameter(Mandatory = $true)]
    [string]$EnvironmentFile
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

if (-not (Test-Path $VersionFile -PathType Leaf)) {
    throw "GitVersion metadata file '$VersionFile' does not exist."
}

$json = Get-Content $VersionFile -Raw
$version = $json | ConvertFrom-Json
if ($version.SemVer -ne $ExpectedVersion) {
    throw "Expected GitVersion '$ExpectedVersion', but the checkout produced '$($version.SemVer)'."
}

$propertyMappings = [ordered]@{
    Major = @("Major")
    Minor = @("Minor")
    Patch = @("Patch")
    PreReleaseTag = @("PreReleaseTag", "PreReleaseLabel")
    PreReleaseTagWithDash = @("PreReleaseTagWithDash", "PreReleaseLabelWithDash")
    PreReleaseLabel = @("PreReleaseLabelName", "PreReleaseLabel")
    PreReleaseNumber = @("PreReleaseNumber")
    BuildMetaData = @("BuildMetaData")
    FullBuildMetaData = @("FullBuildMetaData")
    MajorMinorPatch = @("MajorMinorPatch")
    SemVer = @("SemVer")
    AssemblySemVer = @("AssemblySemVer")
    FullSemVer = @("FullSemVer")
    InformationalVersion = @("InformationalVersion")
    BranchName = @("BranchName")
    Sha = @("Sha")
    NuGetVersion = @("NuGetVersion", "SemVer")
    CommitsSinceVersionSource = @("CommitsSinceVersionSource", "VersionSourceDistance")
    CommitDate = @("CommitDate")
}
$requiredProperties = @(
    "Major",
    "Minor",
    "Patch",
    "MajorMinorPatch",
    "SemVer",
    "AssemblySemVer",
    "FullSemVer",
    "InformationalVersion"
)

foreach ($mapping in $propertyMappings.GetEnumerator()) {
    $property = $null
    foreach ($sourceName in $mapping.Value) {
        $property = $version.PSObject.Properties[$sourceName]
        if ($null -ne $property) {
            break
        }
    }

    if ($null -eq $property -and $mapping.Key -in $requiredProperties) {
        throw "GitVersion did not produce '$($mapping.Key)'."
    }

    $value = if ($null -eq $property -or $null -eq $property.Value) { "" } else { $property.Value.ToString() }
    if ($value.Contains("`r") -or $value.Contains("`n")) {
        throw "GitVersion value '$($mapping.Key)' contains a newline."
    }

    Add-Content $EnvironmentFile "GitVersion_$($mapping.Key)=$value" -Encoding utf8
}

Write-Host "Exported GitVersion $($version.SemVer) for the Cake build."
