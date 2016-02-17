#!/bin/bash

work_dir=`pwd`
build_tools=$work_dir/.build
nuget_path=$build_tools/nuget.exe
configuration="Debug"
dotnet=$work_dir/.dotnet/bin/dotnet

artifacts=$work_dir/artifacts
publish_output=$artifacts/publish
log_output=$artifacts/logs

_header() {
  if [ "$TRAVIS" == true ]; then 
    printf "%b\n" "*** $1 ***"
  else
    printf "%b\n" "\e[1;32m*** $1 ***\e[0m"
  fi
}

_test() {
  cd $work_dir/tests/$1 

  $dotnet build --configuration $configuration \
    >$log_output/$1-build.log 2>&1 \
    || { echo >&2 "Failed to build $1"; cat $log_output/$1-build.log; exit 1; }

  if [ "$2" != "-skipdnxcore50" ]; then
    $dotnet test -xml $log_output\$1-dnxcore50-result.xml -notrait category=failing \
      || { echo >&2 "Test failed: $1 / dnxcore50"; exit 1; }
  fi

  if [ "$2" != "-skipdnx451" ]; then
    test_output="$(dirname `ls ./bin/$configuration/dnx451/*/$1.dll`)"
    cp $build_tools/xunit.runner.console/tools/* $test_output
    mono $test_output/xunit.console.x86.exe $test_output/$1.dll \
      -xml $log_output/$1-dnx451-result.xml -notrait category=failing \
      || { echo >&2 "Test failed: $1 / dnx451"; exit 1; }
  fi

  cd $work_dir
}

_publish() {
  local _src="src/$1"
  local _output="artifacts/publish/$1"

  $dotnet publish $_src --framework dnxcore50 -o $_output/dnxcore50/ --configuration $configuration
  $dotnet publish $_src --framework dnx451 -o $_output/dnx451/ --configuration $configuration

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

  _header "Installing NuGet"
  mkdir -p $build_tools

  nuget_version=latest
  nuget_download_url=https://dist.nuget.org/win-x86-commandline/$nuget_version/nuget.exe
  if [ "$TRAVIS" == true ]; then
    echo "get nuget.exe under travis"
    wget -O $nuget_path $nuget_download_url 2>/dev/null || curl -o $nuget_path --location $nuget_download_url /dev/null
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
  if test ! -d $build_tools/xunit.runner.console; then
    mono $nuget_path install xunit.runner.console -ExcludeVersion -o $build_tools -nocache -pre
  fi

  xunit_clr_runner=$build_tools/xunit.runner.console/tools

  # Handle to many files on osx
  if [ "$TRAVIS_OS_NAME" == "osx" ] || [ `uname` == "Darwin" ]; then
    ulimit -n 4096
  fi
  
  mkdir -p $log_output

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

_restore() {
  _header "Restoring packages"
  if [ "$TRAVIS" == true ]; then 
    $dotnet restore -v Warning || { echo >&2 "Failed to restore packages."; exit 1; }
  else
    $dotnet restore || { echo >&2 "Failed to restore packages."; exit 1; }
  fi
}

#########################
# Clean up
_header "Cleanup"
rm -rf artifacts

# Set up pre-requisite
_prerequisite

# Restore

_header "Testing"

_test OmniSharp.Bootstrap.Tests
_test OmniSharp.MSBuild.Tests       -skipdnxcore50
_test OmniSharp.Roslyn.CSharp.Tests -skipdnx451
_test OmniSharp.Stdio.Tests

# OmniSharp.Roslyn.CSharp.Tests is skipped on dnx451 target because an issue in MEF assembly load on xunit
# Failure repo: https://github.com/troydai/loaderfailure

# Publish
_publish "OmniSharp"
