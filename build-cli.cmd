@echo off

rem Temporary build script

where dotnet
rmdir artifacts\dotnet /s /q

pushd .\src\OmniSharp.CliHost
dotnet publish --output ..\..\artifacts\dotnet --configuration Debug --framework dnxcore50
popd

