#!/bin/bash
rm -rf artifacts
if ! type kvm > /dev/null 2>&1; then
    curl -sSL https://raw.githubusercontent.com/aspnet/Home/release/kvminstall.sh | sh && source ~/.k/kvm/kvm.sh
fi
export KRE_FEED=https://www.nuget.org/api/v2
kvm install 1.0.0-beta3
kpm restore
rc=$?; if [[ $rc != 0 ]]; then exit $rc; fi
cd tests/OmniSharp.Tests
k test
rc=$?; if [[ $rc != 0 ]]; then exit $rc; fi
cd ../OmniSharp.Stdio.Tests
k test
rc=$?; if [[ $rc != 0 ]]; then exit $rc; fi
cd ../../
kvm use 1.0.0-beta3
kpm bundle src/OmniSharp --no-source --out artifacts/build/omnisharp --runtime kre-mono.1.0.0-beta3 2>&1 | tee buildlog
# work around for kpm bundle returning an exit code 0 on failure 
grep "Build succeeded" buildlog

# work around for kpm pack not preserving the executable flag on klr when copied
chmod +x artifacts/build/omnisharp/approot/packages/kre-mono.1.0.0-beta3/bin/klr