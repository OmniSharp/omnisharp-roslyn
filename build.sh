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

if [ "$TRAVIS" == true ]; then
    echo "Installing dotnet from beta channel for CI environment ..."

    $_dotnet_install_script -c beta -d "$build_tools/.dotnet"
    dotnet="$build_tools/.dotnet/bin/dotnet"
else
    echo "Installing dotnet from beta channel for local environment ..."

    $_dotnet_install_script -c beta
    dotnet="dotnet"
fi

# set the DOTNET_REFERENCE_ASSEMBLIES_PATH to mono reference assemblies folder
# https://github.com/dotnet/cli/issues/531
if [ -z "$DOTNET_REFERENCE_ASSEMBLIES_PATH" ]; then
    if [ $(uname) == Darwin ] && [ -d "/Library/Frameworks/Mono.framework/Versions/Current/lib/mono/xbuild-frameworks" ]; then
        export DOTNET_REFERENCE_ASSEMBLIES_PATH="/Library/Frameworks/Mono.framework/Versions/Current/lib/mono/xbuild-frameworks"
    elif [ -d "/usr/local/lib/mono/xbuild-frameworks" ]; then
        export DOTNET_REFERENCE_ASSEMBLIES_PATH="/usr/local/lib/mono/xbuild-frameworks"
    elif [ -d "/usr/lib/mono/xbuild-frameworks" ]; then
        export DOTNET_REFERENCE_ASSEMBLIES_PATH="/usr/lib/mono/xbuild-frameworks"
    fi
fi

header "Set DOTNET_REFERENCE_ASSEMBLIES_PATH to $DOTNET_REFERENCE_ASSEMBLIES_PATH"

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
