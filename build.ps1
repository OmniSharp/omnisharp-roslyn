# Build OmniSharp
function _header($title) {
  Write-Host *** $title ***
}

_header "Cleanup"
rm -r -force artifacts -ErrorAction SilentlyContinue

_header "Installing dotnet"
$build_tools="$pwd\.build"
mkdir $build_tools -ErrorAction SilentlyContinue | Out-Null
invoke-webrequest -uri 'https://raw.githubusercontent.com/dotnet/cli/rel/1.0.0/scripts/obtain/install.ps1' -outfile $build_tools\install.ps1

if ($env:APPVEYOR -eq "True")
{
    $env:DOTNET_INSTALL_DIR=$PWD.Path+"\.dotnet"
    $env:PATH=$env:PATH+";$pwd\.dotnet\cli"
}
else
{
    $env:PATH=$env:PATH+";$env:LocalAppData\Microsoft\dotnet\cli"
}

& $build_tools\install.ps1 beta

_header "Build tools"
& dotnet restore tools 
& dotnet publish .\tools\PublishProject -o $build_tools\PublishProject

& $build_tools\PublishProject\PublishProject.exe
