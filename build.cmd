@echo off

pushd %~dp0

set "DNX_UNSTABLE_FEED=https://www.myget.org/F/aspnetcidev/api/v2"
setlocal EnableDelayedExpansion
where dnvm
if %ERRORLEVEL% neq 0 (
    @powershell -NoProfile -ExecutionPolicy unrestricted -Command "&{$Branch='dev';iex ((new-object net.webclient).DownloadString('https://raw.githubusercontent.com/aspnet/Home/dev/dnvminstall.ps1'))}"
    set PATH=!PATH!;!USERPROFILE!\.dnx\bin
    set DNX_HOME=!USERPROFILE!\.dnx
    goto install
)

:install
rmdir /s /q artifacts
set
call dnvm update-self
call dnvm upgrade -u
call dnvm upgrade -u -r coreclr

call dnu restore
if %errorlevel% neq 0 exit /b %errorlevel%

pushd tests\OmniSharp.Bootstrap.Tests
call dnx test
if %errorlevel% neq 0 exit /b %errorlevel%
popd

pushd tests\OmniSharp.Dnx.Tests
call dnx test
if %errorlevel% neq 0 exit /b %errorlevel%
popd

pushd tests\OmniSharp.Plugins.Tests
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
call dnx test
if %errorlevel% neq 0 exit /b %errorlevel%
popd

call dnvm upgrade -u -r clr

pushd tests\OmniSharp.Bootstrap.Tests
call dnx test
if %errorlevel% neq 0 exit /b %errorlevel%
popd

pushd tests\OmniSharp.Dnx.Tests
call dnx test
if %errorlevel% neq 0 exit /b %errorlevel%
popd

pushd tests\OmniSharp.Plugins.Tests
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
call dnx test
if %errorlevel% neq 0 exit /b %errorlevel%
popd

pushd tests\OmniSharp.MSBuild.Tests
call dnx test
if %errorlevel% neq 0 exit /b %errorlevel%
popd

pushd tests\OmniSharp.ScriptCs.Tests
call dnx test
if %errorlevel% neq 0 exit /b %errorlevel%
popd

call dnvm upgrade -u -r coreclr

rem call dnu pack src\OmniSharp.Host --configuration Release --out artifacts\build\nuget
rem if %errorlevel% neq 0 exit /b %errorlevel%
rem call dnu pack src\OmniSharp.Abstractions --configuration Release --out artifacts\build\nuget
rem if %errorlevel% neq 0 exit /b %errorlevel%
rem call dnu pack src\OmniSharp.Bootstrap --configuration Release --out artifacts\build\nuget
rem if %errorlevel% neq 0 exit /b %errorlevel%
rem call dnu pack src\OmniSharp.Dnx --configuration Release --out artifacts\build\nuget
rem if %errorlevel% neq 0 exit /b %errorlevel%
rem call dnu pack src\OmniSharp.MSBuild --configuration Release --out artifacts\build\nuget
rem if %errorlevel% neq 0 exit /b %errorlevel%
rem call dnu pack src\OmniSharp.Nuget --configuration Release --out artifacts\build\nuget
rem if %errorlevel% neq 0 exit /b %errorlevel%
rem call dnu pack src\OmniSharp.Roslyn --configuration Release --out artifacts\build\nuget
rem if %errorlevel% neq 0 exit /b %errorlevel%
rem call dnu pack src\OmniSharp.Roslyn.CSharp --configuration Release --out artifacts\build\nuget
rem if %errorlevel% neq 0 exit /b %errorlevel%
rem call dnu pack src\OmniSharp.ScriptCs --configuration Release --out artifacts\build\nuget
rem if %errorlevel% neq 0 exit /b %errorlevel%
rem call dnu pack src\OmniSharp.Stdio --configuration Release --out artifacts\build\nuget
rem if %errorlevel% neq 0 exit /b %errorlevel%

rem call dnu publish artifacts\OmniSharp --configuration Release --no-source --out artifacts\build\omnisharp --runtime active
rem if %errorlevel% neq 0 exit /b %errorlevel%

rem pushd artifacts\build\omnisharp
rem call tar -zcf ..\..\..\omnisharp.tar.gz .
rem popd

rem call dnu publish src\OmniSharp.Bootstrap --configuration Release --no-source --out artifacts\build\omnisharp.bootstrap --runtime active

rem pushd artifacts\build\omnisharp.bootstrap
rem call tar -zcf ..\..\..\omnisharp.bootstrap.tar.gz .
rem popd

popd
