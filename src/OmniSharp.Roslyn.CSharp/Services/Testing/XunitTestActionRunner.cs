using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using OmniSharp.Models.V2;

namespace OmniSharp.Roslyn.CSharp.Services.Testing
{

    // TODO: Move to it's own project eventually
    internal class XunitTestActionRunner : ITestActionRunner
    {
        private readonly string _action;
        private readonly string _method;
        private readonly string _projectFolder;

        public XunitTestActionRunner(string action, string method, string filepath)
        {
            _action = action;
            _method = method;

            // TODO: revisit this logic, too clumsy
            _projectFolder = Path.GetDirectoryName(filepath);
            while (!File.Exists(Path.Combine(_projectFolder, "project.json")))
            {
                var parent = Path.GetDirectoryName(filepath);
                if (parent == _projectFolder)
                {
                    break;
                }
                else
                {
                    _projectFolder = parent;
                }
            }
        }

        public Task<ITestActionResult> RunAsync()
        {
            var startInfo = new ProcessStartInfo("dotnet", "test")
            {
                Arguments = $"test -method {_method}",
                WorkingDirectory = _projectFolder,
                CreateNoWindow = true,
                UseShellExecute = true,
                RedirectStandardOutput = false,
                RedirectStandardError = false
            };

            var testProcess = Process.Start(startInfo);
            var timeout = !testProcess.WaitForExit((int)TimeSpan.FromSeconds(30).TotalMilliseconds);

            // temporary, result is not correct
            return Task.FromResult<ITestActionResult>(new TestRunnerResult(!timeout));
        }

        public override string ToString()
        {
            return $"{nameof(XunitTestActionRunner)} action: {_action}, method: {_method}";
        }

        private class TestRunnerResult : ITestActionResult
        {
            private readonly bool _pass;

            public TestRunnerResult(bool pass)
            {
                _pass = pass;
            }

            public RunCodeActionResponse ToRespnse()
            {
                return new RunCodeActionResponse { TestResult = _pass };
            }
        }
    }
}