using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.DotNetTest.Models;
using OmniSharp.Mef;

namespace OmniSharp.DotNetTest.Services
{
    [OmniSharpHandler(OmnisharpEndpoints.RunDotNetTest, LanguageNames.CSharp)]
    public class RunTestServices : RequestHandler<RunDotNetTestRequest, RunDotNetTestResponse>
    {
        public Task<RunDotNetTestResponse> Handle(RunDotNetTestRequest request)
        {
            return Task.FromResult(GetResponse(request.FileName, request.MethodName));
        }

        private RunDotNetTestResponse GetResponse(string filepath, string methodName)
        {
            var projectFolder = Path.GetDirectoryName(filepath);
            while (!File.Exists(Path.Combine(projectFolder, "project.json")))
            {
                var parent = Path.GetDirectoryName(filepath);
                if (parent == projectFolder)
                {
                    break;
                }
                else
                {
                    projectFolder = parent;
                }
            }

            var startInfo = new ProcessStartInfo("dotnet", "test")
            {
                Arguments = $"test -method {methodName}",
                WorkingDirectory = projectFolder,
                CreateNoWindow = true,
                UseShellExecute = true,
                RedirectStandardOutput = false,
                RedirectStandardError = false
            };

            var testProcess = Process.Start(startInfo);
            var timeout = !testProcess.WaitForExit((int)TimeSpan.FromSeconds(30).TotalMilliseconds);

            // temporary, result is not correct
            return new RunDotNetTestResponse
            {
                Pass = true,
                Failure = timeout ? "Timeout" : null
            };
        }
    }
}