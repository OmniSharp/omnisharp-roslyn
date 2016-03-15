# Hack until dotnet/cli##1701 is accepted
$env:DOTNET_INSTALL_DIR=$PWD.Path+"\.dotnet"

Invoke-WebRequest http://cakebuild.net/bootstrapper/windows -OutFile cake-bootstrap.ps1
.\cake-bootstrap.ps1 --experimental @args