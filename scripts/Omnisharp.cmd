SETLOCAL

SET "KRE_APPBASE=%~dp0..\src\Omnisharp"
%USERPROFILE%\.k\runtimes\kre-clr-win-x86.1.0.0-beta3\bin\k run %*
