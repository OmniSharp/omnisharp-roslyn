using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.PlatformAbstractions;

namespace OmniSharp.Tools.PublishProject
{
    public class Program
    {
        public static int Main(string[] args)
        {
            Console.WriteLine("Publish project OmniSharp");
            var root = FindRoot();
            var buildPlan = BuildPlan.Parse(root);

            var projectPath = Path.Combine(root, "src", buildPlan.MainProject);
            if (!Directory.Exists(projectPath))
            {
                Console.WriteLine($"Can't find project {buildPlan.MainProject}");
                return 1;
            }

            var publishOutput = Path.Combine(root, buildPlan.ArtifactsFolder, "publish");
            if (!Directory.Exists(publishOutput))
            {
                Directory.CreateDirectory(publishOutput);
            }

            var packageOutput = Path.Combine(root, buildPlan.ArtifactsFolder, "package");
            if (!Directory.Exists(packageOutput))
            {
                Directory.CreateDirectory(packageOutput);
            }

            var dotnetExecutable = new DotNetExecutor(buildPlan);

            Console.WriteLine($"       root: {root}");
            Console.WriteLine($"    project: {buildPlan.MainProject}");
            Console.WriteLine($"     dotnet: {dotnetExecutable}");
            Console.WriteLine($"     source: {projectPath}");
            Console.WriteLine($"publish out: {publishOutput}");
            Console.WriteLine($"package out: {packageOutput}");
            Console.WriteLine($" frameworks: {string.Join(", ", buildPlan.Frameworks)}");
            Console.WriteLine($"    runtime: {string.Join(", ", buildPlan.Rids)}");

            if (!TestActions.RunTests(buildPlan))
            {
                return 1;
            }

            if (dotnetExecutable.Restore(Path.Combine(root, "src")) != 0)
            {
                Console.Error.WriteLine("Fail to restore projects for {rid}");
                return 1;
            }

            foreach (var combination in from rid in buildPlan.Rids
                                        from framework in buildPlan.Frameworks
                                        select new { Rid = rid, Framework = framework })
            {
                var publish = Path.Combine(publishOutput, buildPlan.MainProject, combination.Rid, combination.Framework);
                if (dotnetExecutable.Publish(publish, projectPath, combination.Rid, combination.Framework) != 0)
                {
                    Console.Error.WriteLine($"Fail to publish {projectPath} on {combination.Framework} for {combination.Rid}");
                    return 1;
                }

                if (!buildPlan.SkipPackaging)
                {
                    Package(publish, packageOutput, buildPlan.MainProject, combination.Rid, combination.Framework);
                }
            }

            return 0;
        }

        private static void Package(string publishOutput,
                                    string packageOutput,
                                    string projectName,
                                    string rid,
                                    string framework)
        {
            var runtimeString = Regex.Replace(rid, @"(\d|\.)*-", "-");
            var buildIdentifier = $"{runtimeString}-{framework}";

            // Disable for now, while travis isn't working correctly.
            if (PlatformServices.Default.Runtime.OperatingSystemPlatform == Platform.Darwin)
            {
                return;
            }

            // Rename framework name to mono for non-windows platforms
            if (runtimeString.Contains("ubuntu-") || runtimeString.Contains("osx-"))
            {
                buildIdentifier.Replace("dnx451", "mono");
            }
            
            var baseFilePath = Path.GetFullPath(Path.Combine(packageOutput, $"{projectName.ToLower()}-{buildIdentifier}"));
            if (runtimeString.Contains("win-"))
            {
                // ZIP for Windows targeted packages
                var zipFilePath = Path.ChangeExtension(baseFilePath, "zip");
                ZipFile.CreateFromDirectory(publishOutput, zipFilePath);
            }
            else
            {
                // TAR.GZ for Unix targeted packages
                var tarFilePath = Path.ChangeExtension(baseFilePath, "tar.gz");

                if (PlatformServices.Default.Runtime.OperatingSystemPlatform == Platform.Windows)
                {
                    // if the build is running on Windows, use 7z
                    var probeStartInfo = new ProcessStartInfo("where", "7z") { UseShellExecute = false };
                    var probe = Process.Start(probeStartInfo);
                    if (!probe.WaitForExit(1000) || probe.ExitCode != 0)
                    {
                        throw new InvalidOperationException($"7z is not installed on this machine. It is required for packaging.");
                    }

                    var tempFilePath = Path.ChangeExtension(baseFilePath, "tar");
                    var tarStartInfo = new ProcessStartInfo("7z", $"a {tempFilePath}")
                    {
                        UseShellExecute = false,
                        WorkingDirectory = publishOutput
                    };

                    var tarProcess = Process.Start(tarStartInfo);
                    tarProcess.WaitForExit();
                    if (tarProcess.ExitCode != 0)
                    {
                        throw new InvalidOperationException($"Tar-ing failed for {projectName} {rid}");
                    }

                    var compressStartInfo = new ProcessStartInfo("7z", $"a {tarFilePath} {tempFilePath}")
                    {
                        UseShellExecute = false,
                        WorkingDirectory = publishOutput
                    };

                    var compressProcess = Process.Start(compressStartInfo);
                    compressProcess.WaitForExit();
                    if (tarProcess.ExitCode != 0)
                    {
                        throw new InvalidOperationException($"Compression failed for {projectName} {rid}");
                    }
                    File.Delete(tempFilePath);
                }
                else
                {
                    // if build is running on *nix, use tar
                    var tarStartInfo = new ProcessStartInfo("tar", $"czf {tarFilePath} .")
                    {
                        UseShellExecute = false,
                        WorkingDirectory = publishOutput
                    };
                    
                    var tarProcess = Process.Start(tarStartInfo);
                    tarProcess.WaitForExit();
                    if (tarProcess.ExitCode != 0)
                    {
                        throw new InvalidOperationException($"Compression failed for {projectName} {rid}");
                    }
                }
            }
        }

        private static string FindRoot()
        {
            var countDown = 100;
            var currentDir = AppContext.BaseDirectory;
            while (!File.Exists(Path.Combine(currentDir, "OmniSharp.sln")) && Path.GetPathRoot(currentDir) != currentDir && countDown > 0)
            {
                currentDir = Path.GetDirectoryName(currentDir);
                countDown--;
            }

            if (countDown < 0)
            {
                throw new InvalidOperationException("Can't find root directory");
            }

            return currentDir;
        }
    }
}
