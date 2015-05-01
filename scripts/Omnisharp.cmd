SETLOCAL

SET "DNX_APPBASE=%~dp0..\src\Omnisharp"
%USERPROFILE%\.dnx\runtimes\dnx-clr-win-x86.1.0.0-beta4\bin\dnx run %*
