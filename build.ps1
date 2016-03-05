# Hack until updated install.sh is used with -InstallDir option
$env:DOTNET_INSTALL_DIR=$PWD.Path+"\.dotnet"

.\cake-bootstrap.ps1 --experimental @args
exit $LASTEXITCODE
