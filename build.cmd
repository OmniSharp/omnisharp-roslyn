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
rem if %errorlevel% neq 0 exit /b %errorlevel%

rem pushd tests\OmniSharp.Dnx.Tests
rem call dnx . test
rem if %errorlevel% neq 0 exit /b %errorlevel%
rem popd

rem pushd tests\OmniSharp.MSBuild.Tests
rem call dnx . test
rem if %errorlevel% neq 0 exit /b %errorlevel%
rem popd

rem pushd tests\OmniSharp.Plugins.Tests
rem call dnx . test
rem if %errorlevel% neq 0 exit /b %errorlevel%
rem popd

rem pushd tests\OmniSharp.Roslyn.CSharp.Tests
rem call dnx . test
rem if %errorlevel% neq 0 exit /b %errorlevel%
rem popd

rem pushd tests\OmniSharp.ScriptCs.Tests
rem call dnx . test
rem if %errorlevel% neq 0 exit /b %errorlevel%
rem popd

rem pushd tests\OmniSharp.Stdio.Tests
rem call dnx . test
rem if %errorlevel% neq 0 exit /b %errorlevel%
rem popd

rem pushd tests\OmniSharp.Tests
rem call dnx . test
rem if %errorlevel% neq 0 exit /b %errorlevel%
rem popd

rem pushd tests\OmniSharp.Tests
rem call dnx . test
rem if %errorlevel% neq 0 exit /b %errorlevel%
rem popd

call dnu pack src\OmniSharp --configuration Release --out artifacts\build\nuget
call dnu pack src\OmniSharp.Abstractions --configuration Release --out artifacts\build\nuget
call dnu pack src\OmniSharp.Bootstrap --configuration Release --out artifacts\build\nuget
call dnu pack src\OmniSharp.Dnx --configuration Release --out artifacts\build\nuget
call dnu pack src\OmniSharp.MSBuild --configuration Release --out artifacts\build\nuget
call dnu pack src\OmniSharp.Nuget --configuration Release --out artifacts\build\nuget
call dnu pack src\OmniSharp.Roslyn --configuration Release --out artifacts\build\nuget
call dnu pack src\OmniSharp.Roslyn.CSharp --configuration Release --out artifacts\build\nuget
call dnu pack src\OmniSharp.ScriptCs --configuration Release --out artifacts\build\nuget
call dnu pack src\OmniSharp.Stdio --configuration Release --out artifacts\build\nuget

rem Build both into the omnisharp package
call dnu publish src\OmniSharp --configuration Release --no-source --out artifacts\build\omnisharp --runtime dnx-clr-win-x86.1.0.0-beta4
call dnu publish src\OmniSharp.Bootstrap --configuration Release --no-source --out artifacts\build\omnisharp --runtime dnx-clr-win-x86.1.0.0-beta4



popd
