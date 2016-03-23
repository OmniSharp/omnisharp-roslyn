using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
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

            foreach (var rid in buildPlan.Rids)
            {
                foreach (var framework in buildPlan.Frameworks)
                {
                    var publish = Path.Combine(publishOutput, buildPlan.MainProject, rid, framework);
                    if(dotnetExecutable.Publish(publish, projectPath, rid, framework) != 0)
                    {
                        Console.Error.WriteLine($"Fail to publish {projectPath} on {framework} for {rid}");
                        return 1;
                    }

                    if (!buildPlan.SkipPackaging)
                    {
                        Package(publish, packageOutput, buildPlan.MainProject, rid, framework);
                    }
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
            var runtimeString = Regex.Replace(rid, "(\\d|\\.)*-", "-");

            // Disable for now, while travis isn't working correctly.
            if (PlatformServices.Default.Runtime.OperatingSystemPlatform == Platform.Darwin) {
                return;
            }
            
            // Simplify Ubuntu to Linux
            runtimeString = runtimeString.Replace("ubuntu", "linux");
            var buildIdentifier = $"{runtimeString}-{framework}";
            // Linux + dnx451 is renamed to Mono
            if (runtimeString.Contains("linux-") && framework.Equals("dnx451"))
                buildIdentifier ="linux-mono";
            // No need to package OSX + dnx451
            else if (runtimeString.Contains("osx-") && framework.Equals("dnx451"))
                return;
            var baseFilePath = Path.GetFullPath(Path.Combine(packageOutput, $"{projectName.ToLower()}-{buildIdentifier}"));
            // On all platforms use ZIP for Windows runtimes
            if (runtimeString.Contains("win-"))
            {
                var zipFilePath = Path.ChangeExtension(baseFilePath, "zip");
                ZipFile.CreateFromDirectory(publishOutput, zipFilePath);
            }
            // On all platforms use TAR.GZ for Unix runtimes
            else
            {
                var tarFilePath = Path.ChangeExtension(baseFilePath, "tar.gz");
                // Use 7z to create TAR.GZ on Windows
                if (PlatformServices.Default.Runtime.OperatingSystemPlatform == Platform.Windows)
                {
                    var tempFilePath = Path.ChangeExtension(baseFilePath, "tar");
                    var tarStartInfo = new ProcessStartInfo("7z", $"a {tempFilePath}")
                    {
                        UseShellExecute = false,
                        WorkingDirectory = publishOutput
                    };
                    var tarProcess = Process.Start(tarStartInfo);
                    tarProcess.WaitForExit();
                    if (tarProcess.ExitCode != 0)
                        throw new InvalidOperationException($"Tar-ing failed for {projectName} {rid}");
                    var compressStartInfo = new ProcessStartInfo("7z", $"a {tarFilePath} {tempFilePath}")
                    {
                        UseShellExecute = false,
                        WorkingDirectory = publishOutput
                    };
                    var compressProcess = Process.Start(compressStartInfo);
                    compressProcess.WaitForExit();
                    if (tarProcess.ExitCode != 0)
                        throw new InvalidOperationException($"Compression failed for {projectName} {rid}");
                    File.Delete(tempFilePath);
                }
                // Use tar to create TAR.GZ on Unix
                else
                {
                    var tarStartInfo = new ProcessStartInfo("tar", $"czf {tarFilePath} .")
                    {
                        UseShellExecute = false,
                        WorkingDirectory = publishOutput
                    };
                    var tarProcess = Process.Start(tarStartInfo);
                    tarProcess.WaitForExit();
                    if (tarProcess.ExitCode != 0)
                        throw new InvalidOperationException($"Compression failed for {projectName} {rid}");
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
