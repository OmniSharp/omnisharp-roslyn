[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ExpectedVersion,

    [Parameter(Mandatory = $true)]
    [string]$EnvironmentFile
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$json = & dotnet tool run dotnet-gitversion /output json
if ($LASTEXITCODE -ne 0) {
    throw "GitVersion failed with exit code $LASTEXITCODE."
}

$version = $json | ConvertFrom-Json
if ($version.SemVer -ne $ExpectedVersion) {
    throw "Expected GitVersion '$ExpectedVersion', but the checkout produced '$($version.SemVer)'."
}

$propertyNames = @(
    "Major",
    "Minor",
    "Patch",
    "PreReleaseTag",
    "PreReleaseTagWithDash",
    "PreReleaseLabel",
    "PreReleaseNumber",
    "BuildMetaData",
    "BuildMetaDataPadded",
    "FullBuildMetaData",
    "MajorMinorPatch",
    "SemVer",
    "LegacySemVer",
    "LegacySemVerPadded",
    "AssemblySemVer",
    "FullSemVer",
    "InformationalVersion",
    "BranchName",
    "Sha",
    "NuGetVersion",
    "CommitsSinceVersionSource",
    "CommitsSinceVersionSourcePadded",
    "CommitDate"
)

foreach ($propertyName in $propertyNames) {
    $property = $version.PSObject.Properties[$propertyName]
    if ($null -eq $property) {
        throw "GitVersion did not produce '$propertyName'."
    }

    $value = if ($null -eq $property.Value) { "" } else { $property.Value.ToString() }
    if ($value.Contains("`r") -or $value.Contains("`n")) {
        throw "GitVersion value '$propertyName' contains a newline."
    }

    Add-Content $EnvironmentFile "GitVersion_$propertyName=$value" -Encoding utf8
}

Write-Host "Exported GitVersion $($version.SemVer) for the Cake build."
