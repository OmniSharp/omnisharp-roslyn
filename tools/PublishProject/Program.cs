using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using Microsoft.Extensions.PlatformAbstractions;

namespace ConsoleApplication
{
    public class Program
    {
        public static int Main(string[] args)
        {
            var root = FindRoot();

            var projectPath = Path.Combine(root, "src", args[0]);
            var projectName = args[0];
            if (!Directory.Exists(projectPath))
            {
                Console.WriteLine($"Can't find project {args[0]}");
                return 1;
            }

            var dotnetExecutable = args[1];
            if (!File.Exists(dotnetExecutable) && dotnetExecutable != "dotnet")
            {
                Console.WriteLine($"Can't find dotnet executable {args[1]}");
                return 1;
            }

            var publishOutput = Path.Combine(root, args[2], "publish");
            if (!Directory.Exists(publishOutput))
            {
                Directory.CreateDirectory(publishOutput);
            }

            var packageOutput = Path.Combine(root, args[2], "package");
            if (!Directory.Exists(packageOutput))
            {
                Directory.CreateDirectory(packageOutput);
            }

            var frameworks = new List<string>();
            if (args.Length > 3 && !string.IsNullOrEmpty(args[3]))
            {
                frameworks.AddRange(args[3].Split(';'));
            }
            else
            {
                // TODO: not to hard code default option
                frameworks.Add("dnxcore50");
            }

            var rids = new HashSet<string>();
            rids.Add(PlatformServices.Default.Runtime.GetRuntimeIdentifier());
            if (args.Length > 4 && !string.IsNullOrEmpty(args[4]))
            {
                foreach (var each in args[4].Split(';'))
                {
                    rids.Add(each);
                }
            }

            Console.WriteLine($"       root: {root}");
            Console.WriteLine($"    project: {projectName}");
            Console.WriteLine($"     source: {projectPath}");
            Console.WriteLine($"     dotnet: {dotnetExecutable}");
            Console.WriteLine($"publish out: {publishOutput}");
            Console.WriteLine($"package out: {packageOutput}");
            Console.WriteLine($" frameworks: {string.Join(", ", frameworks)}");
            Console.WriteLine($"    runtime: {string.Join(", ", rids)}");

            foreach (var rid in rids)
            {
                Restore(Path.Combine(root, "src"), rid, dotnetExecutable);

                foreach (var framework in frameworks)
                {
                    var publish = Path.Combine(publishOutput, projectName, rid, framework);
                    Publish(publish, projectPath, rid, framework, dotnetExecutable);
                    Package(publish, packageOutput, projectName, rid, framework);
                }
            }

            return 0;
        }

        private static void Restore(string path, string rid, string dotnetExecutable)
        {
            // restore the package for under given runtime
            var restoreArgument = $"restore --runtime {rid}";
            var restoreStartInfo = new ProcessStartInfo(dotnetExecutable, restoreArgument)
            {
                UseShellExecute = false,
                WorkingDirectory = path
            };

            if (!Process.Start(restoreStartInfo).WaitForExit((int)TimeSpan.FromMinutes(10).TotalMilliseconds))
            {
                throw new InvalidOperationException($"Restore timeout for {path}");
            }
        }

        private static void Publish(string publishOutput,
                                    string projectPath,
                                    string rid,
                                    string framework,
                                    string dotnetExecutable)
        {
            var publishArgument = $"publish -o {publishOutput} -f {framework} -r {rid}";
            var publisStartInfo = new ProcessStartInfo(dotnetExecutable, publishArgument)
            {
                UseShellExecute = false,
                WorkingDirectory = projectPath
            };

            var process = Process.Start(publisStartInfo);
            if (!process.WaitForExit((int)TimeSpan.FromMinutes(10).TotalMilliseconds))
            {
                throw new InvalidOperationException($"Publish timeout for {projectPath}/{framework}/{rid}");
            }
        }

        private static void Package(string publishOutput,
                                    string packageOutput,
                                    string projectName,
                                    string rid,
                                    string framework)
        {
            var zipFilePath = Path.Combine(packageOutput, $"{projectName}-{rid}-{framework}.zip");
            ZipFile.CreateFromDirectory(publishOutput, zipFilePath); 
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
