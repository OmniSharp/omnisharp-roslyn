#!/bin/bash

_header()
{
    printf "%b\n" "\e[1;32m*** $1 ***\e[0m"
}

_run_tests() {
  _header "Testing"
  # Download xunit console runner for CLR based tests
  if test ! -d $build_folder/xunit.runner.console; then
    mono $nuget_path install xunit.runner.console -ExcludeVersion -o $build_folder -nocache -pre
  fi

  xunit_clr_runner=$build_folder/xunit.runner.console/tools

  _test_coreclr OmniSharp.Bootstrap.Tests
  _test_coreclr OmniSharp.Dnx.Tests
  # _test_coreclr OmniSharp.Roslyn.CSharp.Tests
  # _test_coreclr OmniSharp.Stdio.Tests

  _test_clr OmniSharp.Bootstrap.Tests
  _test_clr OmniSharp.Dnx.Tests
  # _test_clr OmniSharp.Roslyn.CSharp.Tests
  _test_clr OmniSharp.Stdio.Tests
  _test_clr OmniSharp.MSBuild.Tests
}

_test_coreclr() {
  local _project="$1"
  local _target="$TEST_BIN/$_project/coreclr"
  local _log="$LOG_FOLDER/$_project-core-result.xml"

  echo ""
  echo "$_project / CoreCLR"

  dotnet publish ./tests/$_project --output $_target --framework dnxcore50 \
      >$LOG_FOLDER/$_project-core-build.log 2>&1 \
      || { echo >&2 "Failed to build $_project under CoreCLR."; exit 1; }

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

  dotnet publish ./tests/$_project --output $_target --framework dnx451 \
      >$LOG_FOLDER/$_project-clr-build.log 2>&1 \
      || { echo >&2 "Failed to build $_project under CLR."; exit 1; }

  cp $xunit_clr_runner/* $_target
  mono $_target/xunit.console.x86.exe $_target/$_project.dll \
      -xml $_log -parallel none -notrait category=failing \
      || { echo >&2 "Test failed [Log $_log]"; exit 1; }
}

_patch_project() {
  local _project="$1"
  jq '.version="'$OMNISHARP_VERSION'"' src/$_project/project.json > src/$_project/project.json.temp
  mv src/$_project/project.json.temp src/$_project/project.json
}

_pack() {
  local _project="$1"
  dnu restore src/$_project --quiet
  dnu pack src/$_project --configuration Release --out artifacts/nuget --quiet
  rc=$?; if [[ $rc != 0 ]]; then
    echo "Pack failed for src/$_project"
    exit 1;
  fi
}

_publish() {
  local _project="$1"
  local _runtime="$2"
  local _version="1.0.0-rc2-16444"
  local _dest="$3"
  local _tar="$4"

  dnvm use $_version -r $_runtime
  dnu publish src/$_project --configuration Release --no-source --quiet --runtime active --out $_dest
  rc=$?; if [[ $rc != 0 ]]; then
    echo "Publish failed for src/$_project with runtime $_runtime, destination: $_dest"
    exit 1;
  fi

  pushd $_dest/approot/packages/$_project/1.0.0/root/
  jq '.entryPoint="OmniSharp.Host"' project.json > project.json.temp
  mv project.json.temp project.json
  popd

  tree -if $_dest | grep .nupkg | xargs rm
  pushd $_dest/approot
  tar -zcf "../$_tar.tar.gz" .
  rc=$?; if [[ $rc != 0 ]]; then
    echo "Tar failed for src/$_project with runtime $_runtime, destination: $_dest"
    exit 1;
  fi
  popd
}

_prerequisite() {
  _header "Pre-requisite"

  build_folder=.build
  mkdir -p $build_folder
  nuget_path=$build_folder/nuget.exe

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
    nuget_version=latest
    cache_nuget=$cachedir/nuget.$nuget_version.exe
    nuget_download_url=https://dist.nuget.org/win-x86-commandline/$nuget_version/nuget.exe

    if test ! -f $cache_nuget; then
      wget -O $cache_nuget $nuget_download_url 2>/dev/null || curl -o $cache_nuget --location $nuget_download_url /dev/null
    fi

    cp $cache_nuget $nuget_path
  fi

  if (! $TRAVIS) then
    pushd "$(dirname "$0")"
  fi

  # TODO: install dotnet automatically
  command -v dotnet >/dev/null 2>&1 || { echo >&2 "dotnet is not installed."; exit 1; }
  
  # if ! type dnvm > /dev/null 2>&1; then
  #   curl -sSL https://raw.githubusercontent.com/aspnet/Home/dev/dnvminstall.sh | DNX_BRANCH=dev sh && source ~/.dnx/dnvm/dnvm.sh
  # fi
  
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

_cleanup() {  
  rm -rf artifacts
}

_restore() {
  _header "Restoring"
  
  for d in $(ls -d src/*/)
  do
    dotnet restore $d --disable-parallel || { echo >&2 "Fail to restore $d. Exiting ..."; exit 1; }
  done
  
  for d in $(ls -d test/*/)
  do
    dotnet restore $d --disable-parallel || { echo >&2 "Fail to restore $d. Exiting ..."; exit 1; }
  done
}

#########################
_cleanup
_prerequisite
#_restore

_run_tests

exit 0;


OMNISHARP_VERSION="1.0.0-dev";
if [ $TRAVIS_TAG ]; then
  OMNISHARP_VERSION=${TRAVIS_TAG:1};
fi

if [ "$TRAVIS_OS_NAME" == "osx" ]; then
  # omnisharp-coreclr-darwin-x64.tar.gz
  _publish "OmniSharp" "coreclr" "artifacts/omnisharp-coreclr" "../omnisharp-coreclr-darwin-x64"
  # omnisharp.bootstrap-coreclr-darwin-x64.tar.gz
  _publish "OmniSharp.Bootstrap" "coreclr" "artifacts/omnisharp.bootstrap-coreclr" "../omnisharp.bootstrap-coreclr-darwin-x64"
else
  # omnisharp-coreclr-linux-x64.tar.gz
  _publish "OmniSharp" "coreclr" "artifacts/omnisharp-coreclr" "../omnisharp-coreclr-linux-x64"
  # omnisharp-mono.tar.gz
  _publish "OmniSharp" "mono" "artifacts/omnisharp-coreclr" "../omnisharp-mono"

  # omnisharp-coreclr-linux-x64.tar.gz
  _publish "OmniSharp.Bootstrap" "coreclr" "artifacts/omnisharp.bootstrap-coreclr" "../omnisharp.bootstrap-coreclr-linux-x64"
  # omnisharp-mono.tar.gz
  _publish "OmniSharp.Bootstrap" "mono" "artifacts/omnisharp.bootstrap-coreclr" "../omnisharp.bootstrap-mono"

  if [ $TRAVIS ]; then
    _patch_project "OmniSharp.Host"
    _patch_project "OmniSharp.Abstractions"
    _patch_project "OmniSharp.Bootstrap"
    _patch_project "OmniSharp.Dnx"
    _patch_project "OmniSharp.MSBuild"
    _patch_project "OmniSharp.Nuget"
    _patch_project "OmniSharp.Roslyn"
    _patch_project "OmniSharp.Roslyn.CSharp"
    _patch_project "OmniSharp.ScriptCs"
    _patch_project "OmniSharp.Stdio"
  fi

  _pack "OmniSharp.Host"
  _pack "OmniSharp.Abstractions"
  _pack "OmniSharp.Bootstrap"
  _pack "OmniSharp.Dnx"
  _pack "OmniSharp.MSBuild"
  _pack "OmniSharp.Nuget"
  _pack "OmniSharp.Roslyn"
  _pack "OmniSharp.Roslyn.CSharp"
  _pack "OmniSharp.ScriptCs"
  _pack "OmniSharp.Stdio"
fi

tree artifacts

if (! $TRAVIS) then
    popd
fi
