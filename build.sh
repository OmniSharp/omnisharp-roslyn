#!/bin/bash
_test() {
  local _project="$1"
  local _runtime="$2"
  dnvm use 1.0.0-rc2-16444 -r $_runtime
  pushd tests/$_project
  dnx test -parallel none
  rc=$?; if [[ $rc != 0 ]]; then
    echo "Tests failed for tests/$_project with runtime $_runtime"
    exit $rc;
  fi
  popd
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
#########################
if (! $TRAVIS) then
    pushd "$(dirname "$0")"
fi

rm -rf artifacts
if ! type dnvm > /dev/null 2>&1; then
    curl -sSL https://raw.githubusercontent.com/aspnet/Home/dev/dnvminstall.sh | DNX_BRANCH=dev sh && source ~/.dnx/dnvm/dnvm.sh
fi

# Handle to many files on osx
if [ "$TRAVIS_OS_NAME" == "osx" ]; then
  ulimit -n 4096
fi

# work around restore timeouts on Mono
[ -z "$MONO_THREADS_PER_CPU" ] && export MONO_THREADS_PER_CPU=50

export DNX_UNSTABLE_FEED=https://www.myget.org/F/aspnetcidev/api/v2
dnvm update-self

dnvm install 1.0.0-rc2-16444 -u -r mono
rc=$?; if [[ $rc != 0 ]]; then exit $rc; fi

dnvm install 1.0.0-rc2-16444 -u -r coreclr
rc=$?; if [[ $rc != 0 ]]; then exit $rc; fi

dnu restore --quiet --parallel
rc=$?; if [[ $rc != 0 ]]; then exit $rc; fi

_test "OmniSharp.Bootstrap.Tests" "coreclr"
_test "OmniSharp.Bootstrap.Tests" "mono"
_test "OmniSharp.Dnx.Tests" "coreclr"
_test "OmniSharp.Dnx.Tests" "mono"
#_test "OmniSharp.MSBuild.Tests" "coreclr"
_test "OmniSharp.MSBuild.Tests" "mono"
_test "OmniSharp.Plugins.Tests" "coreclr"
_test "OmniSharp.Plugins.Tests" "mono"
_test "OmniSharp.Roslyn.CSharp.Tests" "coreclr"
_test "OmniSharp.Roslyn.CSharp.Tests" "mono"
#_test "OmniSharp.ScriptCs.Tests" "coreclr"
_test "OmniSharp.ScriptCs.Tests" "mono"
_test "OmniSharp.Stdio.Tests" "coreclr"
_test "OmniSharp.Stdio.Tests" "mono"
_test "OmniSharp.Tests" "coreclr"
_test "OmniSharp.Tests" "mono"

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
