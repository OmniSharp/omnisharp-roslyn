@echo off

pushd %~dp0
set "KRE_NUGET_API_URL=https://www.nuget.org/api/v2"
@powershell -NoProfile -ExecutionPolicy unrestricted -Command "iex ((new-object net.webclient).DownloadString('https://raw.githubusercontent.com/aspnet/Home/master/kvminstall.ps1'))"

set PATH=%PATH%;%userprofile%\.k\bin
set KRE_HOME=%USERPROFILE%\.k 

call kvm install 1.0.0-beta3
call kvm use 1.0.0-beta3
rem set the runtime path because the above commands set \.k<space>\runtimes
set PATH=%USERPROFILE%\.k\runtimes\kre-clr-win-x86.1.0.0-beta3\bin;%PATH%
call kpm restore
cd tests\OmniSharp.Tests
call k test
cd ..\OmniSharp.Stdio.Tests
call k test
cd ..\..
call kpm bundle src\OmniSharp --no-source --out artifacts\build\omnisharp --runtime kre-clr-win-x86.1.0.0-beta3

popd
