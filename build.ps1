# Build OmniSharp
Param([switch] $quick)

$build_tools="$pwd\.build"
$dotnet="$pwd\.dotnet\cli\bin\dotnet.exe"

function _header($title) {
  Write-Host *** $title ***
}

_header "Cleanup"
rm -r -force artifacts -ErrorAction SilentlyContinue

_header "Pre-requisite"

$env:DOTNET_INSTALL_DIR=$PWD.Path+"\.dotnet"
mkdir .dotnet -ErrorAction SilentlyContinue | Out-Null

invoke-webrequest -uri 'https://raw.githubusercontent.com/dotnet/cli/rel/1.0.0/scripts/obtain/install.ps1' -outfile .dotnet\install.ps1
& .dotnet\install.ps1 beta

_header "Build tools"

& $dotnet restore tools | Out-Null
Write-Host "Restored tools' packages"

ls tools | % {
    & $dotnet publish .\tools\$_ -o $build_tools\$_ | Out-Null
    Write-Host "Built $_"
}
    
& .build\PublishProject\PublishProject.exe

exit 0