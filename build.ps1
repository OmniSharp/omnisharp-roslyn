# Hack until dotnet/cli##1701 is accepted
$env:DOTNET_INSTALL_DIR=$PWD.Path+"\.dotnet"

.\cake-bootstrap.ps1 --experimental @args
