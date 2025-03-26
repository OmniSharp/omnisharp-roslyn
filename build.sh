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

echo "Preparing to run build script..."

dotnet tool restore

# Build Cake arguments
CAKE_ARGUMENTS=($SCRIPT);
CAKE_ARGUMENTS+=("--target=$TARGET");
CAKE_ARGUMENTS+=("--configuration=$CONFIGURATION")
CAKE_ARGUMENTS+=("--verbosity=$VERBOSITY")
CAKE_ARGUMENTS+=(${SCRIPT_ARGUMENTS[@]})

# Start Cake
dotnet cake "${CAKE_ARGUMENTS[@]}"
