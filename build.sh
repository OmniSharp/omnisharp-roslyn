#!/bin/bash

if (! $TRAVIS) then
    pushd "$(dirname "$0")"
fi

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
dnvm install 1.0.0-beta8
dnvm use 1.0.0-beta8
dnu restore
# end hack

dnvm install 1.0.0-beta4
dnvm use 1.0.0-beta4
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

OMNISHARP_VERSION="v1.0.0-dev";
# Update version numbers on tags
if ( $TRAVIS_TAG ) then OMNISHARP_VERSION=$TRAVIS_TAG; fi

sed -i "s/\"1.0.0-*\"/\"$OMNISHARP_VERSION\"/g" src/OmniSharp/project.json
sed -i "s/\"1.0.0-*\"/\"$OMNISHARP_VERSION\"/g" src/OmniSharp.Abstractions/project.json
sed -i "s/\"1.0.0-*\"/\"$OMNISHARP_VERSION\"/g" src/OmniSharp.Bootstrap/project.json
sed -i "s/\"1.0.0-*\"/\"$OMNISHARP_VERSION\"/g" src/OmniSharp.Dnx/project.json
sed -i "s/\"1.0.0-*\"/\"$OMNISHARP_VERSION\"/g" src/OmniSharp.MSBuild/project.json
sed -i "s/\"1.0.0-*\"/\"$OMNISHARP_VERSION\"/g" src/OmniSharp.Nuget/project.json
sed -i "s/\"1.0.0-*\"/\"$OMNISHARP_VERSION\"/g" src/OmniSharp.Roslyn/project.json
sed -i "s/\"1.0.0-*\"/\"$OMNISHARP_VERSION\"/g" src/OmniSharp.Roslyn.CSharp/project.json
sed -i "s/\"1.0.0-*\"/\"$OMNISHARP_VERSION\"/g" src/OmniSharp.ScriptCs/project.json
sed -i "s/\"1.0.0-*\"/\"$OMNISHARP_VERSION\"/g" src/OmniSharp.Stdio/project.json

dnu pack src/OmniSharp --configuration Release --out artifacts/build/nuget
dnu pack src/OmniSharp.Abstractions --configuration Release --out artifacts/build/nuget
dnu pack src/OmniSharp.Bootstrap --configuration Release --out artifacts/build/nuget
dnu pack src/OmniSharp.Dnx --configuration Release --out artifacts/build/nuget
dnu pack src/OmniSharp.MSBuild --configuration Release --out artifacts/build/nuget
dnu pack src/OmniSharp.Nuget --configuration Release --out artifacts/build/nuget
dnu pack src/OmniSharp.Roslyn --configuration Release --out artifacts/build/nuget
dnu pack src/OmniSharp.Roslyn.CSharp --configuration Release --out artifacts/build/nuget
dnu pack src/OmniSharp.ScriptCs --configuration Release --out artifacts/build/nuget
dnu pack src/OmniSharp.Stdio --configuration Release --out artifacts/build/nuget

mkdir artifacts/OmniSharp.Bootstrapper
# Publish our common base omnisharp configuration (all default language services)
cp bootstrap/bootstrap.json artifacts/OmniSharp.Bootstrapper/project.json
cp src/OmniSharp/config.json artifacts/OmniSharp.Bootstrapper/config.json
dnu restore artifacts/OmniSharp.Bootstrapper
dnu publish artifacts/OmniSharp.Bootstrapper --configuration Release --no-source --out artifacts/build/omnisharp --runtime dnx-mono.1.0.0-beta4

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

pushd artifacts/build/omnisharp
tar -zcf ../../../omnisharp.tar.gz .
popd

pushd artifacts
# list a tree of the results
ls -R | grep ":$" | sed -e 's/:$//' -e 's/[^-][^\/]*\//--/g' -e 's/^/   /' -e 's/-/|/'
popd

if (! $TRAVIS) then
    popd
fi
