using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Extensions.PlatformAbstractions;

namespace OmniSharp.Tools.PublishProject
{
    public class DotNetExecutor
    {
        private readonly string _executablePath;
        private readonly string _executableName;

        public DotNetExecutor(BuildPlan plan)
        {
            if (PlatformServices.Default.Runtime.OperatingSystemPlatform == Platform.Windows)
            {
                _executableName = "dotnet.exe";
            }
            else
            {
                _executableName = "dotnet";
            }

            if (Directory.Exists(Path.Combine(plan.Root, plan.DotNetFolder)))
            {
                _executablePath = Directory.GetFiles(Path.Combine(plan.Root, plan.DotNetFolder), _executableName, SearchOption.AllDirectories)
                                           .First();
            }
            else
            {
                _executablePath = _executableName;
            }
        }

        public int Build(string testFolder)
        {
            var startInfo = new ProcessStartInfo(_executablePath, "build")
            {
                WorkingDirectory = testFolder,
                UseShellExecute = false
            };

            var process = Process.Start(startInfo);
            process.WaitForExit();

            return process.ExitCode;
        }

        public int Test(string testFolder)
        {
            var startInfo = new ProcessStartInfo(_executablePath, "test")
            {
                WorkingDirectory = testFolder,
                UseShellExecute = false
            };

            var process = Process.Start(startInfo);
            process.WaitForExit();

            return process.ExitCode;
        }

        public int Restore(string folder)
        {
            // restore the package for under given runtime
            var restoreArgument = $"restore";
            var restoreStartInfo = new ProcessStartInfo(_executablePath, restoreArgument)
            {
                UseShellExecute = false,
                WorkingDirectory = folder
            };

            var process = Process.Start(restoreStartInfo);

            process.WaitForExit();
            return process.ExitCode;
        }

        public int Restore(string folder, string rid, TimeSpan timeSpan)
        {
            // restore the package for under given runtime
            var restoreArgument = $"restore --runtime {rid}";
            var restoreStartInfo = new ProcessStartInfo(_executablePath, restoreArgument)
            {
                UseShellExecute = false,
                WorkingDirectory = folder
            };

            var process = Process.Start(restoreStartInfo);

            if (!process.WaitForExit((int)timeSpan.TotalMilliseconds))
            {
                return -1;
            }
            else
            {
                return process.ExitCode;
            }
        }

        public int Publish(string publishOutput, string projectPath, string rid, string framework)
        {
            var publishArgument = $"publish -o {publishOutput} -f {framework} -r {rid}";
            var publisStartInfo = new ProcessStartInfo(_executablePath, publishArgument)
            {
                UseShellExecute = false,
                WorkingDirectory = projectPath
            };

            var process = Process.Start(publisStartInfo);
            if (!process.WaitForExit((int)TimeSpan.FromMinutes(10).TotalMilliseconds))
            {
                return -1;
            }
            else
            {
                return process.ExitCode;
            }
        }

        public override string ToString()
        {
            return _executablePath;
        }
    }
}