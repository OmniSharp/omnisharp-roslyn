# Hack until dotnet/cli##1701 is accepted
$env:DOTNET_INSTALL_DIR=$PWD.Path+"\.dotnet"

$TOOLS_DIR = Join-Path $PSScriptRoot "tools"
$PACKAGES_CONFIG = Join-Path $TOOLS_DIR "packages.config"

 # Make sure tools folder exists
if ((Test-Path $PSScriptRoot) -and !(Test-Path $TOOLS_DIR)) {
    Write-Verbose -Message "Creating tools directory..."
    New-Item -Path $TOOLS_DIR -Type directory | out-null
}

# Make sure that packages.config exist.
if (!(Test-Path $PACKAGES_CONFIG)) {
    Write-Verbose -Message "Downloading packages.config..."
    try { Invoke-WebRequest -Uri https://raw.githubusercontent.com/cake-build/website/master/tools/packages.config -OutFile $PACKAGES_CONFIG } catch {
        Throw "Could not download packages.config."
    }
}

Invoke-WebRequest https://raw.githubusercontent.com/cake-build/cake/main/build.ps1 -OutFile cake-bootstrap.ps1
.\cake-bootstrap.ps1 --experimental @args