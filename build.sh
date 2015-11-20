#!/bin/bash

pushd "$(dirname "$0")"

rm -rf artifacts
if ! type dnvm > /dev/null 2>&1; then
    curl -sSL https://raw.githubusercontent.com/aspnet/Home/dev/dnvminstall.sh | DNX_BRANCH=dev sh && source ~/.dnx/dnvm/dnvm.sh
fi

# work around restore timeouts on Mono
[ -z "$MONO_THREADS_PER_CPU" ] && export MONO_THREADS_PER_CPU=50

DNX_FEED="https://www.myget.org/F/aspnetvolatiledev/api/v2"

dnvm upgrade
dnvm install default -r coreclr
dnvm use default

dnu restore
rc=$?; if [[ $rc != 0 ]]; then exit $rc; fi

pushd src\OmniSharp
call dnu list -a
popd

pushd tests/OmniSharp.Dnx.Tests
dnx test -parallel none
rc=$?; if [[ $rc != 0 ]]; then exit $rc; fi
popd

pushd tests/OmniSharp.MSBuild.Tests
dnx test -parallel none
rc=$?; if [[ $rc != 0 ]]; then exit $rc; fi
popd

pushd tests/OmniSharp.Roslyn.CSharp.Tests
dnx test -parallel none
rc=$?; if [[ $rc != 0 ]]; then exit $rc; fi
popd

pushd tests/OmniSharp.Stdio.Tests
dnx test -parallel none
rc=$?; if [[ $rc != 0 ]]; then exit $rc; fi
popd

pushd tests/OmniSharp.Tests
dnx test -parallel none
rc=$?; if [[ $rc != 0 ]]; then exit $rc; fi
popd

dnvm use 1.0.0-beta8
dnu build src/OmniSharp.Abstractions --configuration Release --out artifacts
dnu build src/OmniSharp.Dnx --configuration Release --out artifacts
dnu build src/OmniSharp.MSBuild --configuration Release --out artifacts
dnu build src/OmniSharp.Nuget --configuration Release --out artifacts
dnu build src/OmniSharp.Roslyn --configuration Release --out artifacts
dnu build src/OmniSharp.Roslyn.CSharp --configuration Release --out artifacts
dnu build src/OmniSharp.ScriptCs --configuration Release --out artifacts
dnu build src/OmniSharp.Stdio --configuration Release --out artifacts
dnu publish src/OmniSharp --configuration Release --no-source --out artifacts/build/omnisharp --runtime active 2>&1 | tee build.log
# work around for kpm bundle returning an exit code 0 on failure
grep "Build failed" buildlog
rc=$?; if [[ $rc == 0 ]]; then exit 1; fi

cd artifacts/build/omnisharp
tar -zcf ../../../omnisharp.tar.gz .
cd ../../..
