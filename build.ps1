# Build OmniSharp
Param([switch] $quick)

$build_tools="$pwd\.build"
$nuget_path="$build_tools\nuget.exe"
$configuration="Debug" # temporarily setting
$dotnet="$pwd\.dotnet\cli\bin\dotnet.exe"

$artifacts="$pwd\artifacts"
$publish_output="$artifacts\publish"
$log_output="$artifacts\logs"
$test_output="$artifacts\tests"

function _header($title) {
  Write-Host *** $title ***
}

function _build($project) {
  & $dotnet build src\$project

  if ($LASTEXITCODE -ne 0) {
     Write-Error "Failed to build $project"
     exit 1
  }
}

function _test([string] $project, [switch] $skipdnxcore50, [switch] $skipdnx451) {
  pushd .\tests\$project

  & $dotnet build --configuration $configuration | Out-File "$log_output\$project-build.log"

  if (-not $skipdnxcore50) {
    & $dotnet test -xml "$log_output\$project-dnxcore50-result.xml" -notrait category=failing

    if ($LASTEXITCODE -ne 0) {
      Write-Error "Test failed: $project / dnxcore50"
      exit 1
    }
  }

  if (-not $skipdnx451) {
    $test_output = ls .\bin\$configuration\dnx451\*\$project.dll | split-path -Parent

    cp $build_tools\xunit.runner.console\tools\* $test_output
    & $test_output\xunit.console.x86.exe $test_output\$project.dll `
     -xml "$log_output\$project-dnx451-result.xml" -parallel none  -notrait category=failing

    if ($LASTEXITCODE -ne 0) {
      Write-Error "Test failed: $project / dnx451"
      exit 1
    }
  }

  popd
}

function _publish($project, $runtime) {
  $name = "$project"
  $src = "src\$project"
  $output = "$publish_output\$project"

  & $dotnet publish $src --framework dnxcore50 --output $output\$runtime\dnxcore50 --configuration $configuration --runtime $runtime
  & $dotnet publish $src --framework dnx451 --output $output\$runtime\dnx451 --configuration $configuration --runtime $runtime

  # copy binding redirect configuration respectively to mitigate dotnet publish bug
  ls $src\bin\$configuration\dnx451\*\$project.exe.config | % {
    cp $_ $output\dnx451\ | Out-Null
  }

  # Use to the current naming, we may need to address this in the future
  $package_runtime = $runtime.Replace("win7-", "win-");
  if ($ENV:APPVEYOR -eq  "True") {
    _zip "$output\$runtime\dnx451" "omnisharp-clr-$package_runtime"
    _zip "$output\$runtime\dnxcore50" "omnisharp-coreclr-$package_runtime"
  }
}

function _zip($path, $file) {
    pushd $path
    Write-Host "Compressing $file.zip"
    7z a -r ..\..\..\..\$file.zip | out-null
    if ($LASTEXITCODE -ne 0) {
      write-error "Zip failed for $path, destination: artifacts\$file.zip"
      exit 1;
    }
    popd
}

##########################
# Main

if ($quick) {
  _header "Quick process"
  _publish OmniSharp
  if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to build in quick process. Try run the full build process without switch -quick.";
    exit 1
  }
  else {
    exit 0
  }
}

_header "Cleanup"
rm -r -force artifacts -ErrorAction SilentlyContinue

_header "Pre-requisite"

$env:DOTNET_INSTALL_DIR=$PWD.Path+"\.dotnet"
mkdir .dotnet -ErrorAction SilentlyContinue | Out-Null

invoke-webrequest -uri 'https://raw.githubusercontent.com/dotnet/cli/rel/1.0.0/scripts/obtain/install.ps1' -outfile .dotnet\install.ps1
& .dotnet\install.ps1 beta

mkdir $build_tools -ErrorAction SilentlyContinue | Out-Null

# Ensure NuGet is downloaded to .build folder
if (-not (Test-Path $nuget_path)) {
    $nuget_version="latest"
    $cached_nuget="$env:LocalAppData\NuGet\nuget.$nuget_version.exe"
    if (-not (Test-Path $cached_nuget)) {
        if (-not (Test-Path $env:LocalAppData\NuGet)) {
            mkdir $env:LocalAppData\NuGet
        }
        Invoke-WebRequest "https://dist.nuget.org/win-x86-commandline/$nuget_version/nuget.exe" -OutFile $cached_nuget
    }

    cp $cached_nuget $nuget_path | Out-Null
}

mkdir $log_output -ErrorAction SilentlyContinue | Out-Null

if (-not (Test-Path $build_tools/xunit.runner.console)) {
  & $nuget_path install xunit.runner.console -ExcludeVersion -o $build_tools -nocache -pre
}

_header "Restoring"
& $dotnet restore
if ($LASTEXITCODE -ne 0) {
    Write-Error "Fail to restore."
    exit 1
}

_header "Building"
_build "OmniSharp.Abstractions"
_build "OmniSharp.Stdio"
_build "OmniSharp.Roslyn"
_build "OmniSharp.Roslyn.CSharp"
_build "OmniSharp.Plugins"
_build "OmniSharp.Bootstrap"
_build "OmniSharp.Host"
_build "OmniSharp"

_header "Testing"
_test OmniSharp.Bootstrap.Tests
_test OmniSharp.MSBuild.Tests       -skipdnxcore50
_test OmniSharp.Roslyn.CSharp.Tests -skipdnx451
_test OmniSharp.Stdio.Tests

# OmniSharp.Roslyn.CSharp.Tests is skipped on dnx451 target because an issue in MEF assembly load on xunit
# Failure repo: https://github.com/troydai/loaderfailure

_header "Publishing"
mkdir $publish_output -ErrorAction SilentlyContinue | Out-Null
_publish "OmniSharp" "win7-x64"
_publish "OmniSharp" "win7-x86"

#_header "Packaging"
