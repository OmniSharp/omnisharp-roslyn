# Build OmniSharp

$build_tools=".build"
$nuget_path="$build_tools\nuget.exe"
$configuration="Debug" # temporarily setting

$artifacts="artifacts"
$publish_output="$artifacts\publish"
$log_output="$artifacts\logs"
$test_output="$artifacts\tests"

# list of the product project. order matters.
$projects_coreclr_list = @(
    "OmniSharp.Abstractions",
    "OmniSharp.Stdio",
    "OmniSharp.Roslyn",
    "OmniSharp.Roslyn.CSharp",
    "OmniSharp.Dnx",
    "OmniSharp.Plugins",
    "OmniSharp.Bootstrap",
    "OmniSharp.Host",
    "OmniSharp")

$projects_coreclr_publish = @(
    "OmniSharp"
    )

$projects_clr_list = @(
    # "OmniSharp.Abstractions",
    # "OmniSharp.Stdio",
    # "OmniSharp.Roslyn",
    # "OmniSharp.Roslyn.CSharp",
    # "OmniSharp.Dnx",
    # "OmniSharp.MSBuild",
    # "OmniSharp.Plugins",
    # "OmniSharp.ScriptCs",
    # "OmniSharp.Bootstrap",
    # "OmniSharp.Host"
    )

function _header($title) {
    Write-Host *** $title ***
}

function _cleanup() {
    _header "Clean up"
    rm -r -force artifacts -ErrorAction SilentlyContinue
}

function _prerequisite() {
    _header "Pre-requisite"

    cmd /C where dotnet | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Error "dotnet is not installed."
        exit 1
    }

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
}

function _restore() {
    _header "Restoring"

    dotnet restore
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Fail to restore."
        exit 1
    }
}

function _run_tests() {
    _header "Testing"    

    if (-not (Test-Path $build_tools/xunit.runner.console)) {
        & $nuget_path install xunit.runner.console -ExcludeVersion -o $build_tools -nocache -pre
    }

    _test_coreclr OmniSharp.Bootstrap.Tests
    _test_coreclr OmniSharp.Dnx.Tests 
    _test_coreclr OmniSharp.Roslyn.CSharp.Tests 
    _test_coreclr OmniSharp.Stdio.Tests 

    # pass: _test_clr OmniSharp.Bootstrap.Tests 
    # pass: _test_clr OmniSharp.Stdio.Tests 
    # pass: _test_clr OmniSharp.MSBuild.Tests

    # skip: _test_clr OmniSharp.Dnx.Tests: 
    # skip: _test_clr OmniSharp.Roslyn.CSharp.Tests 
    # Failure repo: https://github.com/troydai/loaderfailure
}

function _test_coreclr($project) {
    $target="$test_output\$project\coreclr"
    $log="$log_output\$project-core-result.xml"

    Write-Host ""
    Write-Host "$project / CoreCLR"

    dotnet publish ./tests/$project --output $target --framework dnxcore50 |
        Out-File "$log_output\$project-core-build.log"

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to build $project under CoreCLR."
        exit 1
    }

    & $target/corerun $target/xunit.console.netcore.exe $target/$project.dll `
       -xml $log -parallel none  -notrait category=failing

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Test failed [Log $log]"
        exit 1
    }
}

function _test_clr($project) {
    $target="$test_output\$project\clr"
    $log="$log_output\$project-clr-result.xml"

    Write-Host ""
    Write-Host "$project / CLR"

    dotnet publish ./tests/$project --output $target --framework dnx451 --configuration $configuration |
        Out-File "$log_output\$project-clr-build.log"

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to build $project under CLR."
        exit 1
    }

    cp $build_tools/xunit.runner.console/tools/* $target/

    & $target/xunit.console.x86.exe $target/$project.dll `
       -xml $log -parallel none  -notrait category=failing

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Test failed [Log $log]"
        exit 1
    } 
}

function _build_verify() {
    $projects_coreclr_list | % {
        dotnet build src\$_ --framework dnxcore50

        if ($LASTEXITCODE -ne 0) {
           Write-Host "Failed to build $_ for target framework dnxcore50." 
           exit 1
        }
    }
}

function _publish() {
    mkdir -p $publish_output -ErrorAction SilentlyContinue | Out-Null
    $projects_coreclr_publish | % {
        $name = "$_"
        $src = "src\$_"
        $output = "$publish_output\dnxcore50\$_"

        dotnet publish $src --framework dnxcore50 --output $output --configuration $configuration
        ls $src\config.json | % { cp $src\config.json $output\config.json | Out-Null }
    }
}

function _package() {
    _header "Packaging"
}

# _cleanup
_prerequisite
_restore
_build_verify
_run_tests
_publish
# _package
