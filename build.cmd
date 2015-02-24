@echo off

set "KRE_NUGET_API_URL=https://www.nuget.org/api/v2"
CALL kvm install 1.0.0-beta3
CALL kvm use 1.0.0-beta3
CALL kpm bundle src/OmniSharp --no-source --out artifacts/build/omnisharp --runtime kre-clr-win-x86.1.0.0-beta3
