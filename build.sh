#!/bin/bash
# Handle to many files on osx

if [ "$TRAVIS_OS_NAME" == "osx" ] || [ `uname` == "Darwin" ]; then
  ulimit -n 4096
fi
bash ./scripts/cake-bootstrap.sh "$@"
