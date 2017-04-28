#!/usr/bin/env bash

SDK_DIR="$(cd "$(dirname "$0")"/.dotnet/sdk/1.0.1/ && pwd -P)"

echo $SDK_DIR

export MSBuildExtensionsPath=$SDK_DIR/
export CscToolExe=$SDK_DIR/Roslyn/RunCsc.sh
export MSBuildSDKsPath=$SDK_DIR/Sdks

msbuild "$@"