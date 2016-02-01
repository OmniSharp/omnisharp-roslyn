SETLOCAL

rem make sure we're bootstrapped
for /f "delims=" %%a in ('%~dp0Bootstrap.cmd') do @set LOCATION=%%a
echo exist %LOCATION%\project.lock.json
if not exist %LOCATION%\project.lock.json (
  call "%USERPROFILE%\.dnx\runtimes\dnx-clr-win-x86.1.0.0-rc2-16444\bin\dnu.cmd" restore %LOCATION%
)
echo %LOCATION%
"%USERPROFILE%\.dnx\runtimes\dnx-clr-win-x86.1.0.0-rc2-16444\bin\dnx" -p %LOCATION% run %*
