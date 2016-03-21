#!/bin/bash

work_dir=`pwd`
build_tools=$work_dir/.build

header() {
    if [ "$TRAVIS" == true ]; then
        printf "%b\n" "*** $1 ***"
    else
        printf "%b\n" "\e[1;32m*** $1 ***\e[0m"
    fi
}

header "Cleanup"
rm -rf artifacts
mkdir -p $build_tools

header "Installing dotnet"

DOTNET_CHANNEL="beta"
DOTNET_VERSION="Latest"
DOTNET_INSTALL="$work_dir/.dotnet"
DOTNET_SCRIPT="https://raw.githubusercontent.com/dotnet/cli/rel/1.0.0/scripts/obtain/install.sh"
DOTNET="$work_dir/.dotnet/cli/dotnet"

if [ `uname` == "Linux" ]; then
    # dotnet build on Ubuntu is currently broken, pin to the 001793 build and install script
    DOTNET_VERSION="1.0.0.001793"
    DOTNET_INSTALL="https://raw.githubusercontent.com/dotnet/cli/42a0eec967f878c4a374d2b297aaedb0f14c20d2/scripts/obtain/install.sh"
    DOTNET="$work_dir/.dotnet/bin/dotnet"
fi

echo "Installing dotnet from $DOTNET_CHANNEL channel for version $DOTNET_VERSION"
echo "Execute install script"
echo "   source: $DOTNET_SCRIPT"
echo "  version: $DOTNET_VERSION"
echo "  channel: $DOTNET_CHANNEL"
echo "  install: $DOTNET_INSTALL"

sh -c "`curl -s $DOTNET_SCRIPT`" "install.sh" "-c" "$DOTNET_CHANNEL" "-v" "$DOTNET_VERSION" "-d" "$DOTNET_INSTALL"

$DOTNET --version # || { echo >&2 "dotnet is not installed correctly" && exit 1 }

# Handle to many files on osx
if [ "$TRAVIS_OS_NAME" == "osx" ] || [ `uname` == "Darwin" ]; then
    ulimit -n 4096
fi

# Build 
header "Building"
$DOTNET restore tools
$DOTNET publish ./tools/PublishProject -o $build_tools/PublishProject/
$build_tools/PublishProject/PublishProject

# OmniSharp.Roslyn.CSharp.Tests is skipped on dnx451 target because an issue in MEF assembly load on xunit
# Failure repo: https://github.com/troydai/loaderfailure

