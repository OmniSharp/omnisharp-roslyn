$Env:OMNISHARP_PACKAGE_OSNAME = "win-x64"
.\scripts\cake-bootstrap.ps1 -experimental @args
exit $LASTEXITCODE
