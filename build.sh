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
_dotnet_install_script_source="https://raw.githubusercontent.com/dotnet/cli/rel/1.0.0/scripts/obtain/install.sh"
_dotnet_install_script=$build_tools/install.sh

curl -o $_dotnet_install_script $_dotnet_install_script_source -s
chmod +x $_dotnet_install_script

DONTET_CHANNEL="beta"
DOTNET_VERSION="Latest"

if [ `uname` == "Linux" ]; then
    # dotnet build on Ubuntu is currently broken, pin to the 001793 build and install script
    DOTNET_VERSION="1.0.0.001793"
    _dotnet_install_script_source="https://raw.githubusercontent.com/dotnet/cli/42a0eec967f878c4a374d2b297aaedb0f14c20d2/scripts/obtain/install.sh"
fi

if [ "$TRAVIS" == true ]; then
    echo "Installing dotnet from beta channel for CI environment ..."
    $_dotnet_install_script -c "$DOTNET_CHANNEL" -v "$DOTNET_VERSION" -d "$work_dir/.dotnet" 
    dotnet="$work_dir/.dotnet/cli/dotnet"
else
    echo "Installing dotnet from beta channel for local environment ..."
    $_dotnet_install_script -c "$DOTNET_CHANNEL" -v "$DOTNET_VERSION"
    dotnet="dotnet"
fi

$dotnet --version

# Handle to many files on osx
if [ "$TRAVIS_OS_NAME" == "osx" ] || [ `uname` == "Darwin" ]; then
    ulimit -n 4096
fi

# Build 
header "Building"
$dotnet restore tools
$dotnet publish ./tools/PublishProject -o $build_tools/PublishProject/
$build_tools/PublishProject/PublishProject

# OmniSharp.Roslyn.CSharp.Tests is skipped on dnx451 target because an issue in MEF assembly load on xunit
# Failure repo: https://github.com/troydai/loaderfailure
