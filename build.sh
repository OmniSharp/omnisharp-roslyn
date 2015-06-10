#!/bin/bash
rm -rf artifacts
if ! type dnvm > /dev/null 2>&1; then
    curl -sSL https://raw.githubusercontent.com/aspnet/Home/dev/dnvminstall.sh | DNX_BRANCH=dev sh && source ~/.dnx/dnvm/dnvm.sh
fi

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
cd tests/OmniSharp.Tests
dnx . test -parallel none
rc=$?; if [[ $rc != 0 ]]; then exit $rc; fi
cd ../OmniSharp.Stdio.Tests
dnx . test -parallel none
rc=$?; if [[ $rc != 0 ]]; then exit $rc; fi
cd ../../
dnvm use 1.0.0-beta4
dnu publish src/OmniSharp --configuration Release --no-source --out artifacts/build/omnisharp --runtime dnx-mono.1.0.0-beta4 2>&1 | tee buildlog
OMNISHARP_VERSION=1.0.0
wget https://www.nuget.org/nuget.exe -O /tmp/nuget.exe
mono /tmp/nuget.exe push artifacts/build/omnisharp/approot/packages/OmniSharp/$OMNISHARP_VERSION/OmniSharp.$OMNISHARP_VERSION.nupkg $MYGET_AUTH -Source https://www.myget.org/F/omnisharp/api/v2/package
mono /tmp/nuget.exe push artifacts/build/omnisharp/approot/packages/OmniSharp.Stdio/$OMNISHARP_VERSION/OmniSharp.Stdio.$OMNISHARP_VERSION.nupkg $MYGET_AUTH -Source https://www.myget.org/F/omnisharp/api/v2/package
# work around for kpm bundle returning an exit code 0 on failure
grep "Build failed" buildlog
rc=$?; if [[ $rc == 0 ]]; then exit 1; fi
