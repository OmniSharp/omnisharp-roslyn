#!/bin/bash

_header() {
  if [ "$TRAVIS" == true ]; then 
    printf "%b\n" "*** $1 ***"
  else
    printf "%b\n" "\e[1;32m*** $1 ***\e[0m"
  fi
}

_test_coreclr() {
  local _project="$1"
  local _target="$TEST_BIN/$_project/coreclr"
  local _log="$LOG_FOLDER/$_project-core-result.xml"

  echo ""
  echo "$_project / CoreCLR"

  $dotnet publish ./tests/$_project --output $_target --framework dnxcore50 \
      >$LOG_FOLDER/$_project-core-build.log 2>&1 \
      || { echo >&2 "Failed to build $_project under CoreCLR."; cat $LOG_FOLDER/$_project-core-build.log; exit 1; }

  $_target/corerun $_target/xunit.console.netcore.exe $_target/$_project.dll \
      -xml $_log -parallel none  -notrait category=failing \
      || { echo >&2 "Test failed [Log $_log]"; exit 1; }
}

_test_clr() {
  local _project="$1"
  local _target="$TEST_BIN/$_project/clr"
  local _log="$LOG_FOLDER/$_project-clr-result.xml"

  echo ""
  echo "$_project / CLR"

  $dotnet publish ./tests/$_project --output $_target --framework dnx451 \
      >$LOG_FOLDER/$_project-clr-build.log 2>&1 \
      || { echo >&2 "Failed to build $_project under CLR."; cat $LOG_FOLDER/$_project-clr-build.log; exit 1; }

  cp $xunit_clr_runner/* $_target
  mono $_target/xunit.console.x86.exe $_target/$_project.dll \
      -xml $_log -parallel none -notrait category=failing \
      || { echo >&2 "Test failed [Log $_log]"; exit 1; }
}

_publish() {
  local _src="src/$1"
  local _output="artifacts/publish/$1"

  $dotnet publish $_src --framework dnxcore50 -o $_output/coreclr/ --configuration $configuration
  $dotnet publish $_src --framework dnx451 -o $_output/clr/ --configuration $configuration

  cp $_src/config.json $_output/coreclr
  cp $_src/config.json $_output/clr
}

_prerequisite() {
  _header "Installing dotnet from beta channel"
  mkdir -p .dotnet

  local dotnet_install_script_source="https://raw.githubusercontent.com/dotnet/cli/rel/1.0.0/scripts/obtain/install.sh"
  local dotnet_install_script=.dotnet/install.sh

  curl -o $dotnet_install_script $dotnet_install_script_source -s
  chmod +x $dotnet_install_script
  $dotnet_install_script -c beta -d .dotnet

  dotnet="./.dotnet/bin/dotnet"

  _header "Installing NuGet"
  build_folder=.build
  nuget_path=$build_folder/nuget.exe

  mkdir -p $build_folder

  nuget_version=latest
  nuget_download_url=https://dist.nuget.org/win-x86-commandline/$nuget_version/nuget.exe
  if [ "$TRAVIS" == true ]; then
    echo "get nuget.exe under travis"
    wget -O $nuget_path $nuget_download_url 2>/dev/null || curl -o $nuget_path --location $nuget_download_url /dev/null
    ls .dotnet
  else
    # Ensure NuGet is downloaded to .build folder
    if test ! -f $nuget_path; then
      if test `uname` = Darwin; then
        cachedir=~/Library/Caches/OmniSharpBuild
      else
        if test -z $XDG_DATA_HOME; then
          cachedir=$HOME/.local/share
        else
          cachedir=$XDG_DATA_HOME
        fi
      fi
      mkdir -p $cachedir
      cache_nuget=$cachedir/nuget.$nuget_version.exe

      if test ! -f $cache_nuget; then
        wget -O $cache_nuget $nuget_download_url 2>/dev/null || curl -o $cache_nuget --location $nuget_download_url /dev/null
      fi

      cp $cache_nuget $nuget_path
    fi
  fi

  _header "Download xunit console runner"
  # Download xunit console runner for CLR based tests
  if test ! -d $build_folder/xunit.runner.console; then
    mono $nuget_path install xunit.runner.console -ExcludeVersion -o $build_folder -nocache -pre
  fi

  ls .dotnet
  pwd

  xunit_clr_runner=$build_folder/xunit.runner.console/tools

  # Handle to many files on osx
  if [ "$TRAVIS_OS_NAME" == "osx" ] || [ `uname` == "Darwin" ]; then
    ulimit -n 4096
  fi
  
  LOG_FOLDER="artifacts/logs"
  TEST_BIN="artifacts/tests"

  mkdir -p $LOG_FOLDER

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
}

#########################
# Main

configuration="Debug"

# Clean up
rm -rf artifacts

# Set up pre-requisite
_prerequisite

# Restore
_header "Restoring packages"
$dotnet restore || { echo >&2 "Failed to restore packages."; exit 1; }

_header "Testing"

_test_coreclr OmniSharp.Bootstrap.Tests
_test_clr     OmniSharp.Bootstrap.Tests

_test_clr     OmniSharp.MSBuild.Tests

_test_coreclr OmniSharp.Roslyn.CSharp.Tests

_test_coreclr OmniSharp.Stdio.Tests
_test_clr     OmniSharp.Stdio.Tests

# OmniSharp.Roslyn.CSharp.Tests is skipped on dnx451 target because an issue in MEF assembly load on xunit
# Failure repo: https://github.com/troydai/loaderfailure

# Publish
_publish "OmniSharp"
