using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using Microsoft.Extensions.PlatformAbstractions;

namespace OmniSharp.Tools.PublishProject
{
    public class TestActions
    {
        private const string NuGetCacheFileName = "nuget.latest.exe";

        public static bool RunTests(BuildPlan buildPlan)
        {
            var xunitTools = PrepareTools(buildPlan);
            var dotnet = new DotNetExecutor(buildPlan);
            var failures = new List<string>();
            
            dotnet.Restore(Path.Combine(buildPlan.Root, "src"));
            dotnet.Restore(Path.Combine(buildPlan.Root, "tests"));

            foreach (var pair in buildPlan.TestProjects)
            {
                RunTestProject(pair.Key, pair.Value, buildPlan, dotnet, xunitTools, failures);
            }

            if (failures.Any())
            {
                foreach (var f in failures)
                {
                    Console.Error.WriteLine(f);
                }
                
                return false;
            }
            else
            {
                return true;
            }
        }

        private static void RunTestProject(string project,
                                           string[] frameworks,
                                           BuildPlan buildPlan,
                                           DotNetExecutor dotnet,
                                           string xunitTools,
                                           List<string> failures)
        {
            var testFolder = Path.Combine(buildPlan.Root, "tests", project);

            if (dotnet.Build(testFolder) != 0)
            {
                failures.Add($"Test build failed: {project}");
            }

            foreach (var framework in frameworks)
            {
                if (framework == "dnx451")
                {
                    if (!RunTestProjectForFullCLR(testFolder, project, xunitTools))
                    {
                        failures.Add($"Test failed: {project} / {framework}");
                    }
                }
                else
                {
                    if (dotnet.Test(testFolder) != 0)
                    {
                        failures.Add($"Test failed: {project} / {framework}");
                    }
                }
            }
        }

        private static bool RunTestProjectForFullCLR(string testFolder, string project, string xunitTools)
        {
            var output = Directory.GetFiles(Path.Combine(testFolder, "bin", "Debug", "dnx451"), $"{project}.dll", SearchOption.AllDirectories)
                                  .OrderByDescending(path=>path.Length)
                                  .First();
            output = Path.GetDirectoryName(output);

            foreach (var file in Directory.GetFiles(xunitTools))
            {
                File.Copy(file, Path.Combine(output, Path.GetFileName(file)), overwrite: true);
            }

            var argument = $"{Path.Combine(output, project)}.dll -parallel none -notrait category=failing";            
            ProcessStartInfo startInfo;
            if (PlatformServices.Default.Runtime.OperatingSystemPlatform == Platform.Windows)
            {
                startInfo = new ProcessStartInfo(Path.Combine(output, "xunit.console.x86.exe"), argument);
            }
            else
            {
                startInfo = new ProcessStartInfo("mono", Path.Combine(output, "xunit.console.x86.exe") + " " + argument);
            }

            var p = Process.Start(startInfo);
            if (p.WaitForExit((int)TimeSpan.FromMinutes(30).TotalMilliseconds))
            {
                return p.ExitCode == 0;
            }
            else
            {
                return false;
            }
        }

        private static string PrepareTools(BuildPlan buildPlan)
        {
            var nuget = GetNuGet(buildPlan);
            return GetXunitRunner(nuget, buildPlan);
        }

        private static string GetXunitRunner(string nuget, BuildPlan buildPlan)
        {
            var xunitRunnerFolder = Path.Combine(buildPlan.Root, buildPlan.BuildToolsFolder, "xunit.runner.console");
            if (!Directory.Exists(xunitRunnerFolder))
            {
                var argument = $"install xunit.runner.console -ExcludeVersion -o {Path.Combine(buildPlan.Root, buildPlan.BuildToolsFolder)} -nocache -pre -Source https://api.nuget.org/v3/index.json";
                ProcessStartInfo startInfo;
                if (PlatformServices.Default.Runtime.OperatingSystemPlatform == Platform.Windows)
                {
                    startInfo = new ProcessStartInfo(nuget, argument);
                }
                else
                {
                    startInfo = new ProcessStartInfo("mono", $"{nuget} {argument}");
                }

                var process = Process.Start(startInfo);
                if (!process.WaitForExit((int)TimeSpan.FromMinutes(5).TotalMilliseconds))
                {
                    throw new InvalidOperationException($"Downloading NuGet.exe timeout");
                }
            }

            return Path.Combine(xunitRunnerFolder, "tools");
        }

        private static string GetNuGet(BuildPlan buildPlan)
        {
            string nugetCache;
            string home;
            switch (PlatformServices.Default.Runtime.OperatingSystemPlatform)
            {
                case Platform.Windows:
                    nugetCache = Path.Combine(Environment.GetEnvironmentVariable("LocalAppData"), "NuGet");
                    break;
                case Platform.Darwin:
                    home = Environment.GetEnvironmentVariable("HOME");
                    nugetCache = Path.Combine(home, "Library", "Caches", "OmniSharpBuild");
                    break;
                default:
                    home = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
                    if (string.IsNullOrEmpty(home))
                    {
                        home = Environment.GetEnvironmentVariables("HOME");
                        nugetCache = Path.Combine(home, ".local", "share");
                    }
                    else
                    {
                        nugetCache = home;
                    }
                    break;
            }

            if (!Directory.Exists(nugetCache))
            {
                Directory.CreateDirectory(nugetCache);
            }

            nugetCache = Path.Combine(nugetCache, NuGetCacheFileName);

            if (!File.Exists(nugetCache))
            {
                var client = new HttpClient();
                var response = client.GetAsync("https://dist.nuget.org/win-x86-commandline/latest/nuget.exe").Result;
                using (var fs = File.Create(Path.Combine(buildPlan.Root, buildPlan.BuildToolsFolder, "nuget.exe")))
                {
                    response.Content.CopyToAsync(fs).Wait();
                }
            }

            var result = Path.Combine(buildPlan.BuildToolsFolder, "nuget.exe");
            File.Copy(nugetCache, result, overwrite: true);

            return result;
        }
    }
}