#!/bin/bash
if ! type kvm > /dev/null 2>&1; then
    curl -sSL https://raw.githubusercontent.com/aspnet/Home/master/kvminstall.sh | sh && source ~/.kre/kvm/kvm.sh
fi
export KRE_FEED=https://www.nuget.org/api/v2
kvm install 1.0.0-beta2
kpm restore
rc=$?; if [[ $rc != 0 ]]; then exit $rc; fi
cd tests/OmniSharp.Tests
k test
rc=$?; if [[ $rc != 0 ]]; then exit $rc; fi
cd ../../
kvm use 1.0.0-beta2
kpm pack src/OmniSharp --no-source --out artifacts/build/OmniSharp --runtime KRE-Mono.1.0.0-beta2 2>&1 | tee buildlog
# work around for kpm pack returning an exit code 0 on failure 
grep "Build succeeded" buildlog
