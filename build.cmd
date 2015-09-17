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
call dnvm install 1.0.0-beta7
call dnvm use 1.0.0-beta7
rem set the runtime path because the above commands set \.dnx<space>\runtimes
set PATH=!USERPROFILE!\.dnx\runtimes\dnx-clr-win-x86.1.0.0-beta7\bin;!PATH!

call dnu restore
if %errorlevel% neq 0 exit /b %errorlevel%

pushd tests\OmniSharp.Dnx.Tests
call dnx . test
if %errorlevel% neq 0 exit /b %errorlevel%
popd

pushd tests\OmniSharp.MSBuild.Tests
call dnx . test
if %errorlevel% neq 0 exit /b %errorlevel%
popd

pushd tests\OmniSharp.Plugins.Tests
call dnx . test
if %errorlevel% neq 0 exit /b %errorlevel%
popd

pushd tests\OmniSharp.Roslyn.CSharp.Tests
call dnx . test
if %errorlevel% neq 0 exit /b %errorlevel%
popd

pushd tests\OmniSharp.ScriptCs.Tests
call dnx . test
if %errorlevel% neq 0 exit /b %errorlevel%
popd

pushd tests\OmniSharp.Stdio.Tests
call dnx . test
if %errorlevel% neq 0 exit /b %errorlevel%
popd

pushd tests\OmniSharp.Tests
call dnx . test
if %errorlevel% neq 0 exit /b %errorlevel%
popd

pushd tests\OmniSharp.Tests
call dnx . test
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
call dnu publish src\OmniSharp --no-source --out artifacts\build\omnisharp --runtime dnx-clr-win-x86.1.0.0-beta7

popd
