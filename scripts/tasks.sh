# Major build tasks

header() {
    if [ "$TRAVIS" == true ]; then
        printf "%b\n" "*** $1 ***"
    else
        printf "%b\n" "\e[1;32m*** $1 ***\e[0m"
    fi
}

run_test() {
    header "Running tests: $1"
    cd $work_dir/tests/$1

    $dotnet build --configuration $configuration \
        >$log_output/$1-build.log 2>&1 \
        || { echo >&2 "Failed to build $1"; cat $log_output/$1-build.log; exit 1; }

    if [ "$2" != "-skipdnxcore50" ]; then
        $dotnet test -xml $log_output/$1-dnxcore50-result.xml -notrait category=failing \
            || { echo >&2 "Test failed: $1 / dnxcore50"; exit 1; }
    fi

    if [ "$2" != "-skipdnx451" ]; then
        test_output="$(dirname `ls ./bin/$configuration/dnx451/*/$1.dll`)"
        cp $build_tools/xunit.runner.console/tools/* $test_output
        mono $test_output/xunit.console.x86.exe $test_output/$1.dll \
        -xml $log_output/$1-dnx451-result.xml -notrait category=failing \
            || { echo >&2 "Test failed: $1 / dnx451"; exit 1; }
    fi

    cd $work_dir
}

restore_packages() {
    header "Restoring packages"

    # Handle to many files on osx
    if [ "$TRAVIS_OS_NAME" == "osx" ] || [ `uname` == "Darwin" ]; then
        ulimit -n 4096
    fi

    if [ "$TRAVIS" == true ]; then
        $dotnet restore -v Warning || { echo >&2 "Failed to restore packages."; exit 1; }
    else
        $dotnet restore || { echo >&2 "Failed to restore packages."; exit 1; }
    fi
}

install_dotnet() {
    header "Installing dotnet"
    local _dotnet_install_script_source="https://raw.githubusercontent.com/dotnet/cli/rel/1.0.0/scripts/obtain/install.sh"
    local _dotnet_install_script=$build_tools/install.sh

    curl -o $_dotnet_install_script $_dotnet_install_script_source -s
    chmod +x $_dotnet_install_script

    if [ "$TRAVIS" == true ]; then
        echo "Installing dotnet from beta channel for CI environment ..."

        $_dotnet_install_script -c beta -d "$build_tools/.dotnet"
        dotnet="$build_tools/.dotnet/bin/dotnet"
    else
        echo "Installing dotnet from beta channel for local environment ..."

        $_dotnet_install_script -c beta
        dotnet="dotnet"
    fi
}

install_nuget() {
    header "Installing NuGet"
    nuget_version=latest
    nuget_download_url=https://dist.nuget.org/win-x86-commandline/$nuget_version/nuget.exe

    if [ "$TRAVIS" == true ]; then
        wget -O $nuget_path $nuget_download_url 2>/dev/null || curl -o $nuget_path --location $nuget_download_url /dev/null
    else
        # Ensure NuGet is downloaded to .build folder
        if test ! -f $nuget_path; then
        if test `uname` = Darwin; then
            cachedir=~/Library/Caches/OmniSharpBuild
        else
            if test -z $XDG_DATA_HOME; then
            cachedir=$HOME/.local/share
            else
            cachedir=$XDG_DATA_HOME
            fi
        fi
        mkdir -p $cachedir
        cache_nuget=$cachedir/nuget.$nuget_version.exe

        if test ! -f $cache_nuget; then
            wget -O $cache_nuget $nuget_download_url 2>/dev/null || curl -o $cache_nuget --location $nuget_download_url /dev/null
        fi

        cp $cache_nuget $nuget_path
        fi
    fi
}

# Install xunit console runner for CLR
install_xunit_runner() {
    header "Downloading xunit console runner"

    if test ! -d $build_tools/xunit.runner.console; then
        mono $nuget_path install xunit.runner.console -ExcludeVersion -o $build_tools -nocache -pre -Source https://api.nuget.org/v3/index.json
    fi

    xunit_clr_runner=$build_tools/xunit.runner.console/tools
}

set_dotnet_reference_path() {
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

    header "Set DOTNET_REFERENCE_ASSEMBLIES_PATH to $DOTNET_REFERENCE_ASSEMBLIES_PATH"
}

publish() {
    header "Publishing $1"

    _output="$artifacts/publish/$1"
    if [ -n "$2" ]; then
        _output="$2"
    fi

    echo "Publish project to $_output"
    for framework in dnx451 dnxcore50; do
        $dotnet publish $work_dir/src/$1 \
                --framework $framework \
                --output $_output/$framework \
                --configuration $configuration

        # is there a better way? not sure.
        if [[ "$TRAVIS_OS_NAME" == "osx" ]] && [[ "$framework" == "dnxcore50" ]]; then
            # omnisharp-coreclr-darwin-x64.tar.gz
            tar $_output/$framework "../../../omnisharp-coreclr-darwin-x64"
        elif [[ "$TRAVIS_OS_NAME" == "linux" ]] && [[ "$framework" == "dnxcore50" ]]; then
            # omnisharp-coreclr-linux-x64.tar.gz
            tar $_output/$framework "../../../omnisharp-coreclr-linux-x64"
        elif [[ "$TRAVIS_OS_NAME" == "linux" ]] && [[ "$framework" == "dnx451" ]]; then
            # omnisharp-mono.tar.gz
            tar $_output/$framework "../../../omnisharp-mono"
        fi
    done

    # copy binding redirect configuration respectively to mitigate dotnet publish bug
    cp $work_dir/src/$1/bin/$configuration/dnx451/*/$1.exe.config $_output/dnx451/
}

tar() {
  pushd $1
  tar -zcf "$2.tar.gz" .
  rc=$?; if [[ $rc != 0 ]]; then
    echo "Tar failed for $1 with runtime $framework"
    exit 1;
  fi
  popd
}

package() {
    header "Packaging $1"

    for c in Release Debug; do
        $dotnet pack $work_dir/src/$1 \
                --output $artifacts/packages/$c/ \
                --configuration $c
    done
}