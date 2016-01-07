SETLOCAL

for /F "delims=" %%I in ('dir %USERPROFILE%\.dnx\runtimes\dnx-coreclr-win-*-rc2-* /b /ad /on') do set RUNTIME=%%I
rem make sure we're bootstrapped
for /f "delims=" %%a in ('%~dp0Bootstrap.cmd') do @set LOCATION=%%a
echo exist %LOCATION%\project.lock.json
if not exist %LOCATION%\project.lock.json (
  call "%USERPROFILE%\.dnx\runtimes\%RUNTIME%\bin\dnu.cmd" restore %LOCATION%
)
echo %LOCATION%

"%USERPROFILE%\.dnx\runtimes\%RUNTIME%\bin\dnx" %LOCATION% run %*
