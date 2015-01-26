@echo off

set "KRE_NUGET_API_URL=https://www.myget.org/F/aspnetvnext/api/v2"
set "KRE_NUGET_API_URL=https://www.nuget.org/api/v2"
CALL kvm install 1.0.0-beta2
CALL kpm pack src/Omnisharp.Http --no-source --out artifacts/build/Omnisharp.Http --runtime KRE-CLR-x86.1.0.0-beta2
