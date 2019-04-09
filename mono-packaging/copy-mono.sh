#!/usr/bin/env bash

# Copies Mono assets needed to run OmniSharp.

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

readonly OS_MAC="macOS"
readonly OS_Linux32="linux-x86"
readonly OS_Linux64="linux-x86_64"

os=""

case `uname` in
    "Darwin")
        os=$OS_MAC
        ;;
    "Linux")
        case `uname -m` in
            "x86" | "i386" | "i686")
                os=$OS_Linux32
                ;;
            "x86_64")
                os=$OS_Linux64
                ;;
        esac
        ;;
    *)
        echo "This operating system is not supported."
        exit 1
        ;;
esac

mono_version=`mono --version | head -n 1 | sed 's/[^0-9.]*\([0-9.]*\).*/\1/'`

gac_assemblies=(
    "Microsoft.Build.Engine/4.0.0.0__b03f5f7f11d50a3a/Microsoft.Build.Engine.dll"
    "Microsoft.Build.Tasks.v4.0/4.0.0.0__b03f5f7f11d50a3a/Microsoft.Build.Tasks.v4.0.dll"
    "Microsoft.Build.Utilities.v4.0/4.0.0.0__b03f5f7f11d50a3a/Microsoft.Build.Utilities.v4.0.dll"
    "Mono.Data.Tds/4.0.0.0__0738eb9f132ed756/Mono.Data.Tds.dll"
    "Mono.Posix/4.0.0.0__0738eb9f132ed756/Mono.Posix.dll"
    "Mono.Security/4.0.0.0__0738eb9f132ed756/Mono.Security.dll"
    "System/4.0.0.0__b77a5c561934e089/System.dll"
    "System.ComponentModel.Composition/4.0.0.0__b77a5c561934e089/System.ComponentModel.Composition.dll"
    "System.ComponentModel.DataAnnotations/4.0.0.0__31bf3856ad364e35/System.ComponentModel.DataAnnotations.dll"
    "System.Configuration/4.0.0.0__b03f5f7f11d50a3a/System.Configuration.dll"
    "System.Core/4.0.0.0__b77a5c561934e089/System.Core.dll"
    "System.Data/4.0.0.0__b77a5c561934e089/System.Data.dll"
    "System.EnterpriseServices/4.0.0.0__b03f5f7f11d50a3a/System.EnterpriseServices.dll"
    "System.IO.Compression/4.0.0.0__b77a5c561934e089/System.IO.Compression.dll"
    "System.IO.Compression.FileSystem/4.0.0.0__b77a5c561934e089/System.IO.Compression.FileSystem.dll"
    "System.Net.Http/4.0.0.0__b03f5f7f11d50a3a/System.Net.Http.dll"
    "System.Numerics/4.0.0.0__b77a5c561934e089/System.Numerics.dll"
    "System.Numerics.Vectors/4.0.0.0__b03f5f7f11d50a3a/System.Numerics.Vectors.dll"
    "System.Runtime.Serialization/4.0.0.0__b77a5c561934e089/System.Runtime.Serialization.dll"
    "System.Security/4.0.0.0__b03f5f7f11d50a3a/System.Security.dll"
    "System.ServiceModel.Internals/0.0.0.0__b77a5c561934e089/System.ServiceModel.Internals.dll"
    "System.Threading.Tasks.Dataflow/4.0.0.0__b77a5c561934e089/System.Threading.Tasks.Dataflow.dll"
    "System.Transactions/4.0.0.0__b77a5c561934e089/System.Transactions.dll"
    "System.Xaml/4.0.0.0__b77a5c561934e089/System.Xaml.dll"
    "System.Xml/4.0.0.0__b77a5c561934e089/System.Xml.dll"
    "System.Xml.Linq/4.0.0.0__b77a5c561934e089/System.Xml.Linq.dll"
)

framework_facades=(
    "netstandard.dll"
    "System.AppContext.dll"
    "System.Collections.dll"
    "System.Collections.Concurrent.dll"
    "System.ComponentModel.dll"
    "System.ComponentModel.Annotations.dll"
    "System.ComponentModel.EventBasedAsync.dll"
    "System.ComponentModel.Primitives.dll"
    "System.ComponentModel.TypeConverter.dll"
    "System.Console.dll"
    "System.Diagnostics.Contracts.dll"
    "System.Diagnostics.Debug.dll"
    "System.Diagnostics.Tools.dll"
    "System.Diagnostics.Tracing.dll"
    "System.Dynamic.Runtime.dll"
    "System.Globalization.dll"
    "System.IO.dll"
    "System.IO.FileSystem.dll"
    "System.IO.FileSystem.Primitives.dll"
    "System.Linq.dll"
    "System.Linq.Expressions.dll"
    "System.Linq.Parallel.dll"
    "System.ObjectModel.dll"
    "System.Reflection.dll"
    "System.Reflection.Extensions.dll"
    "System.Reflection.Primitives.dll"
    "System.Resources.ResourceManager.dll"
    "System.Runtime.dll"
    "System.Runtime.Extensions.dll"
    "System.Runtime.InteropServices.dll"
    "System.Runtime.InteropServices.RuntimeInformation.dll"
    "System.Runtime.Numerics.dll"
    "System.Security.Cryptography.Encoding.dll"
    "System.Security.Cryptography.Primitives.dll"
    "System.Security.Cryptography.X509Certificates.dll"
    "System.Text.Encoding.dll"
    "System.Text.Encoding.Extensions.dll"
    "System.Text.RegularExpressions.dll"
    "System.Threading.dll"
    "System.Threading.Tasks.dll"
    "System.Threading.Tasks.Parallel.dll"
    "System.Threading.Thread.dll"
    "System.Xml.ReaderWriter.dll"
    "System.Xml.XDocument.dll"
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

_copy_runtime_assets() {
    local mono_runtime_path=""
    local mono_lib_path=""
    local mono_etc_path=""
    local libMonoPosixHelper_name=""
    local libMonoBtlsShared_name=""
    local libMonoSystemNative_name=""
    local libMonoSystemNative_target_name=""

    if [ "$os" = "$OS_MAC" ]; then
        mono_base_path=/Library/Frameworks/Mono.framework/Versions/Current

        mono_runtime_path=$mono_base_path/bin/mono-sgen64
        mono_lib_path=$mono_base_path/lib
        mono_etc_path=$mono_base_path/etc/mono
        libMonoPosixHelper_name=libMonoPosixHelper.dylib
        libMonoSystemNative_name=libmono-system-native.0.dylib
        libMonoSystemNative_target_name=libmono-system-native.dylib
    else # Linux
        mono_runtime_path=/usr/bin/mono-sgen
        mono_lib_path=/usr/lib
        mono_etc_path=/etc/mono
        libMonoPosixHelper_name=libMonoPosixHelper.so
        libMonoBtlsShared_name=libmono-btls-shared.so
        libMonoSystemNative_name=libmono-system-native.so.0.0.0
        libMonoSystemNative_target_name=libmono-system-native.so
    fi

    local mono_libMonoSystemNative_path=$mono_lib_path/$libMonoSystemNative_name
    local mono_libMonoPosixHelper_path=$mono_lib_path/$libMonoPosixHelper_name
    local mono_libMonoBtlsShared_path=$mono_lib_path/$libMonoBtlsShared_name
    local mono_config_path=$mono_etc_path/config
    local mono_machine_config_path=$mono_etc_path/4.5/machine.config

    _verify_file "$mono_runtime_path"
    _verify_file "$mono_libMonoPosixHelper_path"
    _verify_file "$mono_libMonoSystemNative_path"

    _verify_file "$mono_libMonoBtlsShared_path"
    _verify_file "$mono_config_path"
    _verify_file "$mono_machine_config_path"

    if [ -d "$target_path" ]; then
        rm -rf "$target_path"
    fi

    target_bin_path=$target_path/bin
    target_lib_path=$target_path/lib
    target_etc_path=$target_path/etc

    mkdir -p "$target_bin_path"
    mkdir -p "$target_lib_path"
    mkdir -p "$target_etc_path"
    mkdir -p "$target_etc_path/mono/4.5"

    target_runtime_path=$target_bin_path/mono
    target_libMonoPosixHelper_path=$target_lib_path/$libMonoPosixHelper_name
    target_liblibMonoSystemNative_path=$target_lib_path/$libMonoSystemNative_target_name

    target_libMonoBtlsShared_path=$target_lib_path/$libMonoBtlsShared_name
    target_config_path=$target_etc_path/config
    target_machine_config_path=$target_etc_path/mono/4.5/machine.config

    cp "$mono_runtime_path" "$target_runtime_path"
    cp "$mono_libMonoPosixHelper_path" "$target_libMonoPosixHelper_path"
    cp "$mono_libMonoSystemNative_path" "$target_liblibMonoSystemNative_path"
    cp "$mono_libMonoBtlsShared_path" "$target_libMonoBtlsShared_path"
    cp "$mono_config_path" "$target_config_path"
    cp "$mono_machine_config_path" "$target_machine_config_path"

    # copy run script
    cp "$script_path/run" "$target_path/run"
    chmod 755 "$target_path/run"
}

_copy_framework_assets() {
    local mono_gac_path=""
    local mono_45_path=""
    local mono_45_facades_path=""

    if [ "$os" = "$OS_MAC" ]; then
        mono_base_path=/Library/Frameworks/Mono.framework/Versions/Current

        mono_gac_path=$mono_base_path/lib/mono/gac
        mono_45_path=$mono_base_path/lib/mono/4.5
        mono_45_facades_path=$mono_base_path/lib/mono/4.5/Facades
    else # Linux
        mono_gac_path=/usr/lib/mono/gac
        mono_45_path=/usr/lib/mono/4.5
        mono_45_facades_path=/usr/lib/mono/4.5/Facades
    fi

    target_gac_path=$target_path/lib/mono/gac
    target_45_path=$target_path/lib/mono/4.5
    target_45_facades_path=$target_45_path/Facades

    mkdir -p "$target_gac_path"
    mkdir -p "$target_45_facades_path"
    
    _copy_file "$mono_45_path/mscorlib.dll" "$target_45_path/mscorlib.dll"

    for file in "${gac_assemblies[@]}"; do
        _copy_file "$mono_gac_path/$file" "$target_gac_path/$file"
    done

    for file in "${framework_facades[@]}"; do
        _copy_file "$mono_45_facades_path/$file" "$target_45_facades_path/$file"
    done
}

_create_target_path
_copy_runtime_assets
_copy_framework_assets
_create_archive "mono.$os-$mono_version.zip"
