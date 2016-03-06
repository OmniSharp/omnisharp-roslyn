# set the DOTNET_REFERENCE_ASSEMBLIES_PATH to mono reference assemblies folder
# https://github.com/dotnet/cli/issues/531
if [ -z "$DOTNET_REFERENCE_ASSEMBLIES_PATH" ]; then
    if [ $(uname) == Darwin ] && [ -d "/Library/Frameworks/Mono.framework/Versions/Current/lib/mono/xbuild-frameworks" ]; then
        export DOTNET_REFERENCE_ASSEMBLIES_PATH="/Library/Frameworks/Mono.framework/Versions/Current/lib/mono/xbuild-frameworks"
    elif [ -d "/usr/local/lib/mono/xbuild-frameworks" ]; then
        export DOTNET_REFERENCE_ASSEMBLIES_PATH="/usr/local/lib/mono/xbuild-frameworks"
    elif [ -d "/usr/lib/mono/xbuild-frameworks" ]; then
        export DOTNET_REFERENCE_ASSEMBLIES_PATH="/usr/lib/mono/xbuild-frameworks"
    fi
fi

if [ $(uname) == Darwin ]; then
    export OSSTRING=osx
elif [ $(uname) == Linux ]; then
    export OSSTRING=linux
fi
#curl -Lsfo cake-bootstrap.sh http://cakebuild.net/bootstrapper/$OSSTRING
#chmod +x cake-bootstrap.sh
./cake-bootstrap.sh $@