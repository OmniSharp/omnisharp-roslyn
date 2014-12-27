#!/bin/bash

export KRE_FEED=https://www.nuget.org/api/v2
kvm install 1.0.0-beta1
kpm restore
rc=$?; if [[ $rc != 0 ]]; then exit $rc; fi
cd tests/OmniSharp.Tests
k test
rc=$?; if [[ $rc != 0 ]]; then exit $rc; fi
cd ../../
export KRE_FEED=https://www.myget.org/F/aspnetvnext/api/v2
kvm upgrade
kpm pack src/OmniSharp --no-source --out artifacts/build/OmniSharp --runtime KRE-Mono.1.0.0-beta1
rc=$?; if [[ $rc != 0 ]]; then exit $rc; fi

