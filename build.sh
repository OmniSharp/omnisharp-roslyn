#!/bin/bash

pushd "$(dirname "$0")"

rm -rf artifacts
if ! type dnvm > /dev/null 2>&1; then
    curl -sSL https://raw.githubusercontent.com/aspnet/Home/dev/dnvminstall.sh | DNX_BRANCH=dev sh && source ~/.dnx/dnvm/dnvm.sh
fi

# work around restore timeouts on Mono
[ -z "$MONO_THREADS_PER_CPU" ] && export MONO_THREADS_PER_CPU=50

# HACK - dnu restore with beta4 fails most of the time
# due to timeouts or other failures.
# Fetch the latest dnu and use that instead
#export DNX_UNSTABLE_FEED=https://www.myget.org/F/aspnetmaster/api/v2/
dnvm update-self
dnvm upgrade -unstable
dnu restore
# end hack

dnvm install 1.0.0-beta4
dnvm alias default 1.0.0-beta4
dnu restore
rc=$?; if [[ $rc != 0 ]]; then exit $rc; fi

pushd tests/OmniSharp.Dnx.Tests
dnx . test -parallel none
rc=$?; if [[ $rc != 0 ]]; then exit $rc; fi
popd

pushd tests/OmniSharp.MSBuild.Tests
dnx . test -parallel none
rc=$?; if [[ $rc != 0 ]]; then exit $rc; fi
popd

pushd tests/OmniSharp.Plugins.Tests
dnx . test -parallel none
rc=$?; if [[ $rc != 0 ]]; then exit $rc; fi
popd

pushd tests/OmniSharp.Roslyn.CSharp.Tests
dnx . test -parallel none
rc=$?; if [[ $rc != 0 ]]; then exit $rc; fi
popd

pushd tests/OmniSharp.ScriptCs.Tests
dnx . test -parallel none
rc=$?; if [[ $rc != 0 ]]; then exit $rc; fi
popd

pushd tests/OmniSharp.Stdio.Tests
dnx . test -parallel none
rc=$?; if [[ $rc != 0 ]]; then exit $rc; fi
popd

pushd tests/OmniSharp.Tests
dnx . test -parallel none
rc=$?; if [[ $rc != 0 ]]; then exit $rc; fi
popd

dnvm use 1.0.0-beta4
dnu build src/OmniSharp.Abstractions --configuration Release --out artifacts
dnu build src/OmniSharp.Dnx --configuration Release --out artifacts
dnu build src/OmniSharp.MSBuild --configuration Release --out artifacts
dnu build src/OmniSharp.Nuget --configuration Release --out artifacts
dnu build src/OmniSharp.Roslyn --configuration Release --out artifacts
dnu build src/OmniSharp.Roslyn.CSharp --configuration Release --out artifacts
dnu build src/OmniSharp.ScriptCs --configuration Release --out artifacts
dnu build src/OmniSharp.Stdio --configuration Release --out artifacts
dnu publish src/OmniSharp --configuration Release --no-source --out artifacts/build/omnisharp --runtime dnx-mono.1.0.0-beta4 2>&1 | tee buildlog
# work around for kpm bundle returning an exit code 0 on failure
grep "Build failed" buildlog
rc=$?; if [[ $rc == 0 ]]; then exit 1; fi

curl -LO http://nuget.org/nuget.exe
mono nuget.exe install dnx-clr-win-x86 -Version 1.0.0-beta4 -Prerelease -OutputDirectory artifacts/build/omnisharp/approot/packages

if [ ! -d "artifacts/build/omnisharp/approot/packages/dnx-clr-win-x86.1.0.0-beta4" ]; then
    echo 'ERROR: Can not find dnx-clr-win-x86.1.0.0-beta4 in output exiting!'
    exit 1
fi

if [ ! -d "artifacts/build/omnisharp/approot/packages/dnx-mono.1.0.0-beta4" ]; then
    echo 'ERROR: Can not find dnx-mono.1.0.0-beta4 in output exiting!'
    exit 1
fi

cd artifacts/build/omnisharp
tar -zcf ../../../omnisharp.tar.gz .
cd ../../..

popd
