using System.Diagnostics;
using System.IO;
using OmniSharp.Models.V2;

namespace OmniSharp.Roslyn.CSharp.Services.Testing
{
    public class TestActionProvider
    {
        public TestActionProvider()
        {
        }

        public ITestActionExecutor GetTestAction(RunCodeActionRequest request)
        {
            var identifier = request.Identifier;
            if (string.IsNullOrEmpty(identifier) || !request.Identifier.StartsWith("test"))
            {
                return null;
            }

            var sep = identifier.IndexOf('|', 4);
            if (sep == -1)
            {
                return null;
            }

            var action = identifier.Substring(5, sep - 5);
            var method = identifier.Substring(sep + 1);

            if (action != "run" && action != "debug")
            {
                return null;
            }
            else if (string.IsNullOrEmpty(method))
            {
                return null;
            }

            return new XunitTestActionExecutor(action, method, request.FileName);
        }

        private class XunitTestActionExecutor : ITestActionExecutor
        {
            private readonly string _action;
            private readonly string _method;
            private readonly string _projectFolder;

            public XunitTestActionExecutor(string action, string method, string filepath)
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

            public void Run()
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
                testProcess.WaitForExit();
            }

            public override string ToString()
            {
                return $"{nameof(XunitTestActionExecutor)} action: {_action}, method: {_method}";
            }
        }
    }
}