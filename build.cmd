@echo off

pushd %~dp0
set "KRE_NUGET_API_URL=https://www.nuget.org/api/v2"
setlocal EnableDelayedExpansion 
where kvm
if %ERRORLEVEL% neq 0 (
    @powershell -NoProfile -ExecutionPolicy unrestricted -Command "iex ((new-object net.webclient).DownloadString('https://raw.githubusercontent.com/aspnet/Home/master/kvminstall.ps1'))"
    set PATH=!PATH!;!userprofile!\.k\bin
    set KRE_HOME=!USERPROFILE!\.k
    goto install
)

:install
call kvm install 1.0.0-beta3
call kvm use 1.0.0-beta3
rem set the runtime path because the above commands set \.k<space>\runtimes
set PATH=!USERPROFILE!\.k\runtimes\kre-clr-win-x86.1.0.0-beta3\bin;!PATH!

call kpm restore
if %errorlevel% neq 0 exit /b %errorlevel%
cd tests\OmniSharp.Tests
call k test
if %errorlevel% neq 0 exit /b %errorlevel%
cd ..\OmniSharp.Stdio.Tests
call k test
if %errorlevel% neq 0 exit /b %errorlevel%
cd ..\..
call kpm bundle src\OmniSharp --no-source --out artifacts\build\omnisharp --runtime kre-clr-win-x86.1.0.0-beta3

popd
