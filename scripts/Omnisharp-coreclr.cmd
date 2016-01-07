SETLOCAL

for /F "delims=" %%I in ('dir %USERPROFILE%\.dnx\runtimes\dnx-coreclr-win-*-rc2-* /b /ad /on') do set RUNTIME=%%I
"%USERPROFILE%\.dnx\runtimes\%RUNTIME%\bin\dnx" -p %~dp0..\src\OmniSharp run %*
