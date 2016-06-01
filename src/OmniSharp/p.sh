kill $(ps aux | grep "/Users/troydai/Code/omnisharp-roslyn/src/OmniSharp/bin/Debug/netcoreapp1.0/osx.10.11-x64/publish/OmniSharp" | grep -v "grep" | cut -d " " -f10)
../../.dotnet/dotnet publish -f netcoreapp1.0
