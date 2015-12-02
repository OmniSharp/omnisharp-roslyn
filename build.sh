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

pushd tests/OmniSharp.Bootstrap.Tests
dnx . test -parallel none
rc=$?; if [[ $rc != 0 ]]; then exit $rc; fi
popd

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

OMNISHARP_VERSION="1.0.0-dev";
if [ $TRAVIS_TAG ]; then
  OMNISHARP_VERSION=${TRAVIS_TAG:1};
fi

if [ $TRAVIS ]; then
  jq '.version="'$OMNISHARP_VERSION'"' src/OmniSharp/project.json > src/OmniSharp/project.json.temp
  jq '.version="'$OMNISHARP_VERSION'"' src/OmniSharp.Abstractions/project.json > src/OmniSharp.Abstractions/project.json.temp
  jq '.version="'$OMNISHARP_VERSION'"' src/OmniSharp.Bootstrap/project.json > src/OmniSharp.Bootstrap/project.json.temp
  jq '.version="'$OMNISHARP_VERSION'"' src/OmniSharp.Dnx/project.json > src/OmniSharp.Dnx/project.json.temp
  jq '.version="'$OMNISHARP_VERSION'"' src/OmniSharp.MSBuild/project.json > src/OmniSharp.MSBuild/project.json.temp
  jq '.version="'$OMNISHARP_VERSION'"' src/OmniSharp.Nuget/project.json > src/OmniSharp.Nuget/project.json.temp
  jq '.version="'$OMNISHARP_VERSION'"' src/OmniSharp.Roslyn/project.json > src/OmniSharp.Roslyn/project.json.temp
  jq '.version="'$OMNISHARP_VERSION'"' src/OmniSharp.Roslyn.CSharp/project.json > src/OmniSharp.Roslyn.CSharp/project.json.temp
  jq '.version="'$OMNISHARP_VERSION'"' src/OmniSharp.ScriptCs/project.json > src/OmniSharp.ScriptCs/project.json.temp
  jq '.version="'$OMNISHARP_VERSION'"' src/OmniSharp.Stdio/project.json > src/OmniSharp.Stdio/project.json.temp

  mv src/OmniSharp/project.json.temp src/OmniSharp/project.json
  mv src/OmniSharp.Abstractions/project.json.temp src/OmniSharp.Abstractions/project.json
  mv src/OmniSharp.Bootstrap/project.json.temp src/OmniSharp.Bootstrap/project.json
  mv src/OmniSharp.Dnx/project.json.temp src/OmniSharp.Dnx/project.json
  mv src/OmniSharp.MSBuild/project.json.temp src/OmniSharp.MSBuild/project.json
  mv src/OmniSharp.Nuget/project.json.temp src/OmniSharp.Nuget/project.json
  mv src/OmniSharp.Roslyn/project.json.temp src/OmniSharp.Roslyn/project.json
  mv src/OmniSharp.Roslyn.CSharp/project.json.temp src/OmniSharp.Roslyn.CSharp/project.json
  mv src/OmniSharp.ScriptCs/project.json.temp src/OmniSharp.ScriptCs/project.json
  mv src/OmniSharp.Stdio/project.json.temp src/OmniSharp.Stdio/project.json
fi

dnu pack src/OmniSharp --configuration Release --out artifacts/build/nuget --quiet
dnu pack src/OmniSharp.Abstractions --configuration Release --out artifacts/build/nuget --quiet
dnu pack src/OmniSharp.Bootstrap --configuration Release --out artifacts/build/nuget --quiet
dnu pack src/OmniSharp.Dnx --configuration Release --out artifacts/build/nuget --quiet
dnu pack src/OmniSharp.MSBuild --configuration Release --out artifacts/build/nuget --quiet
dnu pack src/OmniSharp.Nuget --configuration Release --out artifacts/build/nuget --quiet
dnu pack src/OmniSharp.Roslyn --configuration Release --out artifacts/build/nuget --quiet
dnu pack src/OmniSharp.Roslyn.CSharp --configuration Release --out artifacts/build/nuget --quiet
dnu pack src/OmniSharp.ScriptCs --configuration Release --out artifacts/build/nuget --quiet
dnu pack src/OmniSharp.Stdio --configuration Release --out artifacts/build/nuget --quiet

mkdir artifacts/OmniSharp.Bootstrapper
# Publish our common base omnisharp configuration (all default language services)
cp bootstrap/bootstrap.json artifacts/OmniSharp.Bootstrapper/project.json
cp src/OmniSharp/config.json artifacts/OmniSharp.Bootstrapper/config.json

dnu restore artifacts/OmniSharp.Bootstrapper
dnu publish artifacts/OmniSharp.Bootstrapper --configuration Release --no-source --out artifacts/build/omnisharp --runtime dnx-mono.1.0.0-beta4

pushd artifacts/build/omnisharp/approot/packages/OmniSharp.Bootstrapper/1.0.0/root/
jq '.entryPoint="OmniSharp.Host"' project.json > project.json.temp
mv project.json.temp project.json
cat project.json
popd

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

tree -if artifacts/build/omnisharp | grep .nupkg | xargs rm
pushd artifacts/build/omnisharp
mv Bootstrapper omnisharp
mv roslyn/Bootstrapper.cmd omnisharp.cmd
tar -zcf ../../../omnisharp.tar.gz .
popd

# Publish just the bootstrap
dnu publish src/OmniSharp.Bootstrap --configuration Release --no-source --out artifacts/build/omnisharp.bootstrap --runtime dnx-mono.1.0.0-beta4

# work around for kpm bundle returning an exit code 0 on failure
grep "Build failed" buildlog
rc=$?; if [[ $rc == 0 ]]; then exit 1; fi

curl -LO http://nuget.org/nuget.exe
mono nuget.exe install dnx-clr-win-x86 -Version 1.0.0-beta4 -Prerelease -OutputDirectory artifacts/build/omnisharp.bootstrap/approot/packages

if [ ! -d "artifacts/build/omnisharp.bootstrap/approot/packages/dnx-clr-win-x86.1.0.0-beta4" ]; then
    echo 'ERROR: Can not find dnx-clr-win-x86.1.0.0-beta4 in output exiting!'
    exit 1
fi

if [ ! -d "artifacts/build/omnisharp.bootstrap/approot/packages/dnx-mono.1.0.0-beta4" ]; then
    echo 'ERROR: Can not find dnx-mono.1.0.0-beta4 in output exiting!'
    exit 1
fi

tree -if artifacts/build/omnisharp.bootstrap | grep .nupkg | xargs rm
pushd artifacts/build/omnisharp.bootstrap
tar -zcf ../../../omnisharp.bootstrap.tar.gz .
popd

tree artifacts

if (! $TRAVIS) then
    popd
fi
