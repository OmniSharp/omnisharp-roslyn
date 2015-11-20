@echo off

pushd %~dp0
set "DNX_FEED=https://www.myget.org/F/aspnetvolatiledev/api/v2"
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

call dnvm upgrade
call dnvm install default -r coreclr -nonative
call dnvm use default

call dnu restore
if %errorlevel% neq 0 exit /b %errorlevel%

pushd src\OmniSharp
call dnu list -a
popd

pushd tests\OmniSharp.Dnx.Tests
call dnx test
if %errorlevel% neq 0 exit /b %errorlevel%
popd

pushd tests\OmniSharp.MSBuild.Tests
call dnx test
if %errorlevel% neq 0 exit /b %errorlevel%
popd

pushd tests\OmniSharp.Roslyn.CSharp.Tests
call dnx test -parallel none
if %errorlevel% neq 0 exit /b %errorlevel%
popd

pushd tests\OmniSharp.Stdio.Tests
call dnx test
if %errorlevel% neq 0 exit /b %errorlevel%
popd

pushd tests\OmniSharp.Tests
call dnx test -parallel none
if %errorlevel% neq 0 exit /b %errorlevel%
popd

call dnu build src/OmniSharp.Abstractions --configuration Release --out artifacts
call dnu build src/OmniSharp.Dnx --configuration Release --out artifacts
call dnu build src/OmniSharp.MSBuild --configuration Release --out artifacts
call dnu build src/OmniSharp.Nuget --configuration Release --out artifacts
call dnu build src/OmniSharp.Roslyn --configuration Release --out artifacts
call dnu build src/OmniSharp.Roslyn.CSharp --configuration Release --out artifacts
call dnu build src/OmniSharp.ScriptCs --configuration Release --out artifacts
call dnu build src/OmniSharp.Stdio --configuration Release --out artifacts
call dnu publish src\OmniSharp --no-source --out artifacts\build\omnisharp --runtime active

popd
