echo test
$PSScriptRoot = Split-Path -Parent -Path $MyInvocation.MyCommand.Definition
$SDK_DIR = "$PSScriptRoot/.dotnet/sdk/1.0.1/"
$ENV:PATH = "$PSScriptRoot/.dotnet" + ";" + $ENV:PATH;

echo $SDK_DIR

$ENV:MSBuildExtensionsPath="$SDK_DIR/"
$ENV:MSBuildSDKsPath="$SDK_DIR/Sdks"

echo dotnet build @args
dotnet build @args
