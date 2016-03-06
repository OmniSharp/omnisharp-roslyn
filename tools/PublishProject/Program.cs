using System;
using System.IO;
using System.IO.Compression;

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
            Console.WriteLine($"     source: {projectPath}");
            Console.WriteLine($"     dotnet: {dotnetExecutable}");
            Console.WriteLine($"publish out: {publishOutput}");
            Console.WriteLine($"package out: {packageOutput}");
            Console.WriteLine($" frameworks: {string.Join(", ", buildPlan.Frameworks)}");
            Console.WriteLine($"    runtime: {string.Join(", ", buildPlan.Rids)}");            
            
            if (!TestActions.RunTests(buildPlan))
            {
                return 1;
            }

            foreach (var rid in buildPlan.Rids)
            {
                if (dotnetExecutable.Restore(Path.Combine(root, "src"), rid, TimeSpan.FromMinutes(10)) != 0)
                {
                    Console.Error.WriteLine("Fail to restore projects for {rid}");
                    return 1;
                }

                foreach (var framework in buildPlan.Frameworks)
                {
                    var publish = Path.Combine(publishOutput, buildPlan.MainProject, rid, framework);
                    if( dotnetExecutable.Publish(publish, projectPath, rid, framework) != 0)
                    {
                        Console.Error.WriteLine($"Fail to publish {projectPath} on {framework} for {rid}");
                        return 1;
                    }
                    
                    Package(publish, packageOutput, buildPlan.MainProject, rid, framework);
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
