#!/usr/bin/env bash

# Copies Mono MSBuild assets needed by OmniSharp.

# Arguments:
#   $1: output directory

output_path=$1

if [ "$output_path" = "" ]; then
    output_path=`pwd -P`
    echo "No output directory specified. Using $output_path"
fi

script_path="$(cd "$(dirname "$0")" && pwd -P)"

target_path=""

_create_target_path() {
    _cleanup_target_path

    target_path=`mktemp -d 2>/dev/null || mktemp -d -t 'mytmpdir'`
    echo "Using temporary path: $target_path"
}

# deletes the temp directory
_cleanup_target_path() {
    if [ -d "$target_path" ]; then
        rm -rf "$target_path"
        echo "Deleted temp directory: $target_path"
    fi
}

trap _cleanup_target_path EXIT

isMac=false

case `uname` in
    "Darwin")
        isMac=true
        ;;
    "Linux")
        isMac=false
        ;;
    *)
        echo "This operating system is not supported."
        exit 1
        ;;
esac

mono_version=`mono --version | head -n 1 | sed 's/[^0-9.]*\([0-9.]*\).*/\1/'`

msbuild_libraries=(
    "Microsoft.Build.dll"
    "Microsoft.Build.Framework.dll"
    "Microsoft.Build.Tasks.Core.dll"
    "Microsoft.Build.Utilities.Core.dll"
)

msbuild_runtime=(
    "Microsoft.Common.CrossTargeting.targets"
    "Microsoft.Common.CurrentVersion.targets"
    "Microsoft.Common.Mono.targets"
    "Microsoft.Common.overridetasks"
    "Microsoft.Common.targets"
    "Microsoft.Common.tasks"
    "Microsoft.CSharp.CrossTargeting.targets"
    "Microsoft.CSharp.CurrentVersion.targets"
    "Microsoft.CSharp.Mono.targets"
    "Microsoft.CSharp.targets"
    "Microsoft.Data.Entity.targets"
    "Microsoft.NETFramework.CurrentVersion.props"
    "Microsoft.NETFramework.CurrentVersion.targets"
    "Microsoft.NETFramework.props"
    "Microsoft.NETFramework.targets"
    "Microsoft.ServiceModel.targets"
    "Microsoft.VisualBasic.CrossTargeting.targets"
    "Microsoft.VisualBasic.CurrentVersion.targets"
    "Microsoft.VisualBasic.targets"
    "Microsoft.WinFx.targets"
    "Microsoft.WorkflowBuildExtensions.targets"
    "Microsoft.Xaml.targets"
    "MSBuild.dll"
    "MSBuild.dll.config"
"System.Data.Common.dll"
"System.Collections.Immutable.dll"
"System.Diagnostics.StackTrace.dll"
"System.Diagnostics.Tracing.dll"
"System.Globalization.Extensions.dll"
"System.IO.Compression.dll"
"System.Net.Http.dll"
"System.Net.Sockets.dll"
"System.Reflection.Metadata.dll"
"System.Runtime.Serialization.Primitives.dll"
"System.Security.Cryptography.Algorithms.dll"
"System.Security.SecureString.dll"
"System.Threading.Overlapped.dll"
"System.Threading.Tasks.Dataflow.dll"
"System.Xml.XPath.XDocument.dll"
    #"NuGet.targets"
    #"Workflow.VisualBasic.targets"
    #"Workflow.targets"
)

_verify_file() {
    local file_path=$1

    if [ ! -f "$file_path" ]; then
        echo "File does not exist: $file_path"
        exit 1
    fi
}

_copy_file() {
    local file_path=$1
    local target_path=$2

    _verify_file $file_path

    mkdir -p "$(dirname "$target_path")"

    cp "$file_path" "$target_path"
}

_create_archive() {
    local name=$1

    pushd "$target_path"
    zip -r "$output_path/$name" .
    popd
}

_copy_msbuild_library_assets() {
    local mono_msbuild_bin_path=""

    if [ $isMac = true ]; then
        mono_base_path=/Library/Frameworks/Mono.framework/Versions/Current

        mono_msbuild_path=$mono_base_path/lib/mono/msbuild
    else
        mono_msbuild_path=/usr/lib/mono/msbuild
    fi

    for file in "${msbuild_libraries[@]}"; do
        _copy_file "$mono_msbuild_path/15.0/bin/$file" "$target_path/$file"
    done
}

_copy_msbuild_runtime_assets() {
    local mono_msbuild_path=""
    local mono_xbuild_path=""

    if [ $isMac = true ]; then
        mono_base_path=/Library/Frameworks/Mono.framework/Versions/Current

        mono_msbuild_path=$mono_base_path/lib/mono/msbuild
        mono_xbuild_path=$mono_base_path/lib/mono/xbuild
    else
        mono_msbuild_path=/usr/lib/mono/msbuild
        mono_xbuild_path=/usr/lib/mono/xbuild
    fi

    _copy_file "$mono_xbuild_path/15.0/Microsoft.Common.props" "$target_path/15.0/Microsoft.Common.props"

    for file in "${msbuild_runtime[@]}"; do
        _copy_file "$mono_msbuild_path/15.0/bin/$file" "$target_path/$file"
    done
}

_create_target_path
_copy_msbuild_library_assets
_create_archive "Microsoft.Build.Lib.Mono-$mono_version.zip"

_create_target_path
_copy_msbuild_runtime_assets
_create_archive "Microsoft.Build.Runtime.Mono-$mono_version.zip"
