@echo off

set "KRE_NUGET_API_URL=https://www.myget.org/F/aspnetvnext/api/v2"
CALL kvm upgrade
set "KRE_NUGET_API_URL=https://www.nuget.org/api/v2"
CALL kvm install 1.0.0-beta1
CALL kpm pack src/OmniSharp --no-source --out artifacts/build/OmniSharp --runtime KRE-CLR-x86.1.0.0-beta1
