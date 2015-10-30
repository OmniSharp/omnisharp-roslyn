SETLOCAL

rem make sure we're bootstrapped
for /f "delims=" %%a in ('%~dp0Bootstrap.cmd') do @set LOCATION=%%a
call "%USERPROFILE%\.dnx\runtimes\dnx-clr-win-x86.1.0.0-beta4\bin\dnu.cmd" restore %LOCATION%
"%USERPROFILE%\.dnx\runtimes\dnx-clr-win-x86.1.0.0-beta4\bin\dnx" %LOCATION% run %*
