@echo off

pushd %~dp0
set "DNX_FEED=https://www.nuget.org/api/v2"
setlocal EnableDelayedExpansion
where dnvm
if %ERRORLEVEL% neq 0 (
    @powershell -NoProfile -ExecutionPolicy unrestricted -Command "&{$Branch='dev';iex ((new-object net.webclient).DownloadString('https://raw.githubusercontent.com/aspnet/Home/dev/dnvminstall.ps1'))}"
    set PATH=!PATH!;!USERPROFILE!\.dnx\bin
    set DNX_HOME=!USERPROFILE!\.dnx
    goto install
)

:install
set
call dnvm install 1.0.0-beta4
call dnvm use 1.0.0-beta4
rem set the runtime path because the above commands set \.dnx<space>\runtimes
set PATH=!USERPROFILE!\.dnx\runtimes\dnx-clr-win-x86.1.0.0-beta4\bin;!PATH!

call dnu restore
if %errorlevel% neq 0 exit /b %errorlevel%
cd tests\OmniSharp.Tests
call dnx . test
if %errorlevel% neq 0 exit /b %errorlevel%
cd ..\OmniSharp.Stdio.Tests
call dnx . test
if %errorlevel% neq 0 exit /b %errorlevel%
cd ..\..
call dnu publish src\OmniSharp --no-source --out artifacts\build\omnisharp --runtime dnx-clr-win-x86.1.0.0-beta4

popd
