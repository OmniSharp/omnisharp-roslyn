SETLOCAL

SET "K_APPBASE=%~dp0..\src\Omnisharp.Http"
%USERPROFILE%\.kre\packages\KRE-CLR-x86.1.0.0-beta2\bin\k run %*
