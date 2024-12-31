#!/bin/bash

# Define default arguments.
SCRIPT="build.cake"
TARGET="Default"
CONFIGURATION="Release"
VERBOSITY="Verbose"
SCRIPT_ARGUMENTS=()

# Parse arguments.
for i in "$@"; do
    case $1 in
        -s|--script) SCRIPT="$2"; shift ;;
        -t|--target) TARGET="$2"; shift ;;
        -c|--configuration) CONFIGURATION="$2"; shift ;;
        -v|--verbosity) VERBOSITY="$2"; shift ;;
        --) shift; SCRIPT_ARGUMENTS+=("$@"); break ;;
        *) SCRIPT_ARGUMENTS+=("$1") ;;
    esac
    shift
done

# Define md5sum or md5 depending on Linux/OSX
MD5_EXE=
if [[ "$(uname -s)" == "Darwin" ]]; then
    MD5_EXE="md5 -r"
else
    MD5_EXE="md5sum"
fi

echo "Preparing to run build script..."

# Define directories.
ROOT_DIR=$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )
TOOLS_DIR=$ROOT_DIR/tools
NUGET_EXE=$TOOLS_DIR/nuget.exe
PACKAGES_CONFIG=$TOOLS_DIR/packages.config
PACKAGES_CONFIG_MD5=$TOOLS_DIR/packages.config.md5sum

# Make sure the tools folder exist.
if [ ! -d "$TOOLS_DIR" ]; then
  echo "Creating tools directory..."
  mkdir "$TOOLS_DIR"
fi

# Make sure that packages.config exist.
if [ ! -f "$PACKAGES_CONFIG" ]; then
    echo "Downloading packages.config..."
    curl -Lsfo "$PACKAGES_CONFIG" https://cakebuild.net/download/bootstrapper/packages
    if [ $? -ne 0 ]; then
        echo "An error occurred while downloading packages.config."
        exit 1
    fi
fi

# Download NuGet if it does not exist.
if [ ! -f "$NUGET_EXE" ]; then
    echo "Downloading NuGet..."
    curl -Lsfo "$NUGET_EXE" https://dist.nuget.org/win-x86-commandline/latest/nuget.exe
    if [ $? -ne 0 ]; then
        echo "An error occurred while downloading nuget.exe."
        exit 1
    fi
fi

# Restore tools from NuGet.
if [ -f "$PACKAGES_CONFIG" ]; then
    pushd "$TOOLS_DIR" >/dev/null

    # Check for changes in packages.config and remove installed tools if true.
    if [ ! -f "$PACKAGES_CONFIG_MD5" ] || [ "$( cat "$PACKAGES_CONFIG_MD5" | sed 's/\r$//' )" != "$( $MD5_EXE "$PACKAGES_CONFIG" | awk '{ print $1 }' )" ]; then
        find . -type d ! -name . | xargs rm -rf
    fi

    echo "Restoring tools from NuGet..."
    mono "$NUGET_EXE" install -ExcludeVersion
    if [ $? -ne 0 ]; then
        echo "An error occurred while restoring NuGet tools."
        exit 1
    else
        $MD5_EXE "$PACKAGES_CONFIG" | awk '{ print $1 }' >| "$PACKAGES_CONFIG_MD5"
    fi

    popd >/dev/null
fi

dotnet tool restore

# Build Cake arguments
CAKE_ARGUMENTS=($SCRIPT);
CAKE_ARGUMENTS+=("--target=$TARGET");
CAKE_ARGUMENTS+=("--configuration=$CONFIGURATION")
CAKE_ARGUMENTS+=("--verbosity=$VERBOSITY")
CAKE_ARGUMENTS+=(${SCRIPT_ARGUMENTS[@]})

# Start Cake
dotnet cake "${CAKE_ARGUMENTS[@]}"
