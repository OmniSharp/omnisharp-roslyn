#!/bin/bash

work_dir=`pwd`
build_tools=$work_dir/.build
nuget_path=$build_tools/nuget.exe
configuration="Debug"

artifacts=$work_dir/artifacts
log_output=$artifacts/logs

. ./scripts/tasks.sh

#########################
# Main

if [ "$1" == "--quick" ]; then
    install_dotnet
    publish "OmniSharp" || { echo >&2 "Failed to quick build. Try to build the OmniSharp without --quick switch."; exit 1; }
    exit 0
fi

if [ "$1" == "--install" ]; then
    install_dotnet
    if [ -d ~/.omnisharp/local ]; then
        echo "Removing local omnisharp ..."
        rm -rf ~/.omnisharp/local
    fi

    mkdir -p ~/.omnisharp/local

    publish "OmniSharp" "$HOME/.omnisharp/local" || \
        { echo >&2 "Failed to quick build. Try to build the OmniSharp without --install switch."; exit 1; }

    exit 0
fi

# Clean up
header "Cleanup"
rm -rf artifacts

# Set up
mkdir -p $build_tools
mkdir -p $log_output

install_dotnet
install_nuget
install_xunit_runner
set_dotnet_reference_path

# Restore
restore_packages

# Testing
run_test OmniSharp.Bootstrap.Tests
run_test OmniSharp.MSBuild.Tests       -skipdnxcore50
run_test OmniSharp.Roslyn.CSharp.Tests -skipdnx451
run_test OmniSharp.Stdio.Tests

# OmniSharp.Roslyn.CSharp.Tests is skipped on dnx451 target because an issue in MEF assembly load on xunit
# Failure repo: https://github.com/troydai/loaderfailure

# Publish
publish "OmniSharp"

