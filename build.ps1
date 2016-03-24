# Build OmniSharp
function _header($title) {
  Write-Host *** $title ***
}

_header "Cleanup"
rm -r -force artifacts -ErrorAction SilentlyContinue

$build_tools="$pwd\.build"
mkdir $build_tools -ErrorAction SilentlyContinue | Out-Null

if ($env:APPVEYOR -eq "True")
{
    _header "Installing dotnet"
    invoke-webrequest -uri 'https://raw.githubusercontent.com/dotnet/cli/rel/1.0.0/scripts/obtain/install.ps1' -outfile $build_tools\install.ps1
    $env:DOTNET_INSTALL_DIR=$PWD.Path+"\.dotnet"
    $env:PATH=$env:PATH+";$pwd\.dotnet\cli"
    & $build_tools\install.ps1 beta
}
else
{
    _header "Use local dotnet"
}

& dotnet --version

_header "Build tools"
& dotnet restore tools 
& dotnet publish .\tools\PublishProject -o $build_tools\PublishProject

& $build_tools\PublishProject\PublishProject.exe
