#!/bin/bash
rm -rf artifacts
if ! type kvm > /dev/null 2>&1; then
    curl -sSL https://raw.githubusercontent.com/aspnet/Home/dev/dnvminstall.sh | DNX_BRANCH=dev sh && source ~/.dnx/dnvm/dnvm.sh
fi
dnvm install 1.0.0-beta4
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
dnu publish src/OmniSharp --no-source --out artifacts/build/omnisharp --runtime dnx-mono.1.0.0-beta4 2>&1 | tee buildlog
# work around for kpm bundle returning an exit code 0 on failure 
grep "Build failed" buildlog
rc=$?; if [[ $rc == 0 ]]; then exit 1; fi

# work around for kpm pack not preserving the executable flag on klr when copied
chmod +x artifacts/build/omnisharp/approot/packages/dnx-mono.1.0.0-beta4/bin/klr
