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
call dnvm install 1.0.0-rc2-16444 -u -r clr -arch x86
call dnvm install 1.0.0-rc2-16444 -u -r clr -arch x64
call dnvm install 1.0.0-rc2-16444 -u -r coreclr -arch x86
call dnvm install 1.0.0-rc2-16444 -u -r coreclr -arch x64

call dnu restore --quiet --parallel
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
call:_publish "OmniSharp" "clr" "x86" "clr-win-x86" "omnisharp-clr-win-x86"
:: omnisharp-coreclr-win-x86.zip
call:_publish "OmniSharp" "coreclr" "x86" "coreclr-win-x86" "omnisharp-coreclr-win-x86"
:: omnisharp-clr-win-x64.zip
call:_publish "OmniSharp" "clr" "x64" "clr-win-x64" "omnisharp-clr-win-x64"
:: omnisharp-coreclr-win-x64.zip
call:_publish "OmniSharp" "coreclr" "x64" "coreclr-win-x64" "omnisharp-coreclr-win-x64"
:: omnisharp.zip
:::: TODO

:: omnisharp.bootstrap-clr-win-x86.zip
call:_publish "OmniSharp.Bootstrap" "clr" "x86" "boot-clr-win-x86" "omnisharp.bootstrap-clr-win-x86"
:: omnisharp.bootstrap-coreclr-win-x86.zip
call:_publish "OmniSharp.Bootstrap" "coreclr" "x86" "boot-coreclr-win-x86" "omnisharp.bootstrap-coreclr-win-x86"
:: omnisharp.bootstrap-clr-win-x64.zip
call:_publish "OmniSharp.Bootstrap" "clr" "x64" "boot-clr-win-x64" "omnisharp.bootstrap-clr-win-x64"
:: omnisharp.bootstrap-coreclr-win-x64.zip
call:_publish "OmniSharp.Bootstrap" "coreclr" "x64" "boot-coreclr-win-x64" "omnisharp.bootstrap-coreclr-win-x64"
:: omnisharp.bootstrap.zip
:::: TODO

call dnvm use 1.0.0-rc2-16444 -r coreclr -arch x86
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
call dnvm use 1.0.0-rc2-16444 -r %~2 -arch x86
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
call dnu restore src\%~1 --quiet
call dnu pack src\%~1 --configuration Release --quiet --out artifacts\nuget
if %errorlevel% neq 0 (
  echo Package failed for src/%~1
  (goto) 2>nul & endlocal & exit /b YOUR_EXITCODE_HERE
)
endlocal
GOTO:EOF

:_publish - %~1=project %~2=runtime %~3=arch %~4=dest %~5=zip
setlocal
call dnvm use 1.0.0-rc2-16444 -r %~2 -arch %~3
call dnu publish "src\%~1" --configuration Release --no-source --quiet --runtime active --out "artifacts\%~4"
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
