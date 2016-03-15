#!/bin/bash

if [ $(uname) == Darwin ]; then
    export OSSTRING=osx
elif [ $(uname) == Linux ]; then
    export OSSTRING=linux
fi

export NUGET_EXE=./tools/nuget.exe

curl -Lsfo cake-bootstrap.sh http://cakebuild.net/bootstrapper/$OSSTRING
bash ./cake-bootstrap.sh "$@"
