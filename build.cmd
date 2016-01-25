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
set "DNX_VERSION=1.0.0-rc2-16444"
call dnvm update-self
call dnvm install %DNX_VERSION% -u -r clr -arch x86
call dnvm install %DNX_VERSION% -u -r clr -arch x64
call dnvm install %DNX_VERSION% -u -r coreclr -arch x86
call dnvm install %DNX_VERSION% -u -r coreclr -arch x64

where dotnet
if %ERRORLEVEL% neq 0 (
    @powershell -NoProfile -ExecutionPolicy unrestricted -Command "&{$Branch='dev';iex ((new-object net.webclient).DownloadString('https://raw.githubusercontent.com/dotnet/cli/master/scripts/obtain/install.ps1'))}"
    set PATH=!PATH!;!LOCALAPPDATA!\Microsoft\dotnet\cli\bin
    set DOTNET_HOME=!LOCALAPPDATA!\Microsoft\dotnet\cli
)

call dotnet restore
if %errorlevel% neq 0 exit /b %errorlevel%

call:_test "OmniSharp.Bootstrap.Tests" "clr"
call:_test "OmniSharp.Bootstrap.Tests" "coreclr"
call:_test "OmniSharp.Dnx.Tests" "clr"
call:_test "OmniSharp.Dnx.Tests" "coreclr"
call:_test "OmniSharp.MSBuild.Tests" "clr"
:: Not supported yet
::call:_test "OmniSharp.MSBuild.Tests" "coreclr"
call:_test "OmniSharp.Plugins.Tests" "clr"
call:_test "OmniSharp.Plugins.Tests" "coreclr"
call:_test "OmniSharp.Roslyn.CSharp.Tests" "clr" "none"
call:_test "OmniSharp.Roslyn.CSharp.Tests" "coreclr" "none"
call:_test "OmniSharp.ScriptCs.Tests" "clr"
:: Not supported yet
::call:_test "OmniSharp.ScriptCs.Tests" "coreclr"
call:_test "OmniSharp.Stdio.Tests" "clr"
call:_test "OmniSharp.Stdio.Tests" "coreclr"
call:_test "OmniSharp.Tests" "clr"
call:_test "OmniSharp.Tests" "coreclr"

:: omnisharp-clr-win-x86.zip
call:_publish "OmniSharp" "dnx451" "x86" "artifacts\clr-win-x86" "omnisharp-clr-win-x86"
:: omnisharp-coreclr-win-x86.zip
call:_publish "OmniSharp" "dnxcore50" "x86" "artifacts\coreclr-win-x86" "omnisharp-coreclr-win-x86"
:: omnisharp-clr-win-x64.zip
call:_publish "OmniSharp" "dnx451" "x64" "artifacts\clr-win-x64" "omnisharp-clr-win-x64"
:: omnisharp-coreclr-win-x64.zip
call:_publish "OmniSharp" "dnxcore50" "x64" "artifacts\coreclr-win-x64" "omnisharp-coreclr-win-x64"
:: omnisharp.zip
:::: TODO

:: omnisharp.bootstrap-clr-win-x86.zip
call:_publish "OmniSharp.Bootstrap" "dnx451" "x86" "artifacts\boot-clr-win-x86" "omnisharp.bootstrap-clr-win-x86"
:: omnisharp.bootstrap-coreclr-win-x86.zip
call:_publish "OmniSharp.Bootstrap" "dnxcore50" "x86" "artifacts\boot-coreclr-win-x86" "omnisharp.bootstrap-coreclr-win-x86"
:: omnisharp.bootstrap-clr-win-x64.zip
call:_publish "OmniSharp.Bootstrap" "dnx451" "x64" "artifacts\boot-clr-win-x64" "omnisharp.bootstrap-clr-win-x64"
:: omnisharp.bootstrap-coreclr-win-x64.zip
call:_publish "OmniSharp.Bootstrap" "dnxcore50" "x64" "artifacts\boot-coreclr-win-x64" "omnisharp.bootstrap-coreclr-win-x64"
:: omnisharp.bootstrap.zip
:::: TODO

call:_pack OmniSharp.Host
call:_pack OmniSharp.Abstractions
call:_pack OmniSharp.Bootstrap
call:_pack OmniSharp.Dnx
call:_pack OmniSharp.MSBuild
call:_pack OmniSharp.Nuget
call:_pack OmniSharp.Roslyn
call:_pack OmniSharp.Roslyn.CSharp
call:_pack OmniSharp.ScriptCs
call:_pack OmniSharp.Stdio

popd
GOTO:EOF

::--------------------------------------------------------
::-- Functions
::--------------------------------------------------------
:_test - %~1=project %~2=parallel
setlocal
call dnvm use %DNX_VERSION% -r %~2 -arch x86
pushd tests\%~1
if "%~2" == "" (
  call dnx test
) else (
  call dnx test -parallel none
)
if %errorlevel% neq 0 (
  echo Tests failed for src/%~1 with runtime %~2
  (goto) 2>nul & endlocal & exit /b YOUR_EXITCODE_HERE
)
popd
endlocal
GOTO:EOF

:_pack - %~1=project
setlocal
call dotnet pack src\%~1 --configuration Release --output artifacts\nuget
if %errorlevel% neq 0 (
  echo Package failed for src/%~1
  (goto) 2>nul & endlocal & exit /b YOUR_EXITCODE_HERE
)
endlocal
GOTO:EOF

:_publish - %~1=project %~2=runtime %~3=arch %~4=dest %~5=zip
setlocal
call dotnet publish "src\%~1" --configuration Release --framework "%~2" --output "%~4"
if %errorlevel% neq 0 (
  echo Publish failed for src/%~1 with runtime %~2-%~3, destination: artifacts\%~4
  (goto) 2>nul & endlocal & exit /b YOUR_EXITCODE_HERE
)
if "%APPVEYOR%" == "True" (
  pushd artifacts\%~4\approot
  echo Compressing %~5.zip
  call 7z a -r ..\..\%~5.zip . > NUL
  if %errorlevel% neq 0 (
    echo Zip failed for src/%~1 with runtime %~2-%~3, destination: artifacts\%~4
    (goto) 2>nul & endlocal & exit /b YOUR_EXITCODE_HERE
  )
)
popd
endlocal
GOTO:EOF
