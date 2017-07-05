using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using OmniSharp.DotNetTest.Models;
using OmniSharp.DotNetTest.Models.Events;
using OmniSharp.DotNetTest.TestFrameworks;
using OmniSharp.Eventing;
using OmniSharp.Services;
using OmniSharp.Utilities;

using LegacyTestOutcome = Microsoft.Extensions.Testing.Abstractions.TestOutcome;
using LegacyTestResult = Microsoft.Extensions.Testing.Abstractions.TestResult;

namespace OmniSharp.DotNetTest.Legacy
{
    /// <summary>
    /// Handles 'dotnet test' for .NET Core SDK earlier than "1.0.0-preview3"
    /// </summary>
    internal partial class LegacyTestManager : TestManager
    {
        private const string TestExecution_GetTestRunnerProcessStartInfo = "TestExecution.GetTestRunnerProcessStartInfo";
        private const string TestExecution_TestResult = "TestExecution.TestResult";

        public LegacyTestManager(Project project, string workingDirectory, DotNetCliService dotNetCli, IEventEmitter eventEmitter, ILoggerFactory loggerFactory)
            : base(project, workingDirectory, dotNetCli, eventEmitter, loggerFactory.CreateLogger<LegacyTestManager>())
        {
        }

        protected override string GetCliTestArguments(int port, int parentProcessId)
        {
            return $"test --port {port} --parentProcessId {parentProcessId}";
        }

        protected override void VersionCheck()
        {
            SendMessage(MessageType.VersionCheck);

            var message = ReadMessage();
            var payload = message.DeserializePayload<ProtocolVersion>();

            if (payload.Version != 1)
            {
                throw new InvalidOperationException($"Expected ProtocolVersion 1, but was {payload.Version}");
            }
        }

        public override RunTestResponse RunTest(string methodName, string testFrameworkName)
        {
            var testFramework = TestFramework.GetFramework(testFrameworkName);
            if (testFramework == null)
            {
                throw new InvalidOperationException($"Unknown test framework: {testFrameworkName}");
            }

            SendMessage(TestExecution_GetTestRunnerProcessStartInfo);

            var testStartInfoMessage = ReadMessage();

            var testStartInfo = testStartInfoMessage.DeserializePayload<TestStartInfo>();

            var fileName = testStartInfo.FileName;
            // We pass in the tests as a list. So trimming out the wait-command that expects the tests to be sent as a separate message.
            var startInfoArguments = testStartInfo.Arguments.Replace("--wait-command", "");
            var arguments = $"{startInfoArguments} {testFramework.MethodArgument} {methodName}";

            var startInfo = new ProcessStartInfo(fileName, arguments)
            {
                WorkingDirectory = WorkingDirectory,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var testProcess = Process.Start(startInfo);

            var output = new StringBuilder();
            var error = new StringBuilder();

            testProcess.OutputDataReceived += (_, e) =>
            {
                EmitTestMessage(TestMessageLevel.Informational, e.Data ?? string.Empty);
                output.AppendLine(e.Data);
            };

            testProcess.ErrorDataReceived += (_, e) =>
            {
                EmitTestMessage(TestMessageLevel.Error, e.Data ?? string.Empty);
                error.AppendLine(e.Data);
            };

            testProcess.BeginOutputReadLine();
            testProcess.BeginErrorReadLine();

            var testResults = new List<LegacyTestResult>();
            var done = false;

            while (!done)
            {
                var message = ReadMessage();

                switch (message.MessageType)
                {
                    case TestExecution_TestResult:
                        testResults.Add(message.DeserializePayload<LegacyTestResult>());
                        break;

                    case MessageType.ExecutionComplete:
                        done = true;
                        break;
                }
            }

            if (!testProcess.HasExited)
            {
                if (!testProcess.WaitForExit(3000))
                {
                    testProcess.KillChildrenAndThis();
                }
            }

            var results = testResults.Select(testResult =>
                new DotNetTestResult
                {
                    MethodName = testResult.Test.FullyQualifiedName,
                    Outcome = testResult.Outcome.ToString().ToLowerInvariant(),
                    ErrorMessage = testResult.ErrorMessage,
                    ErrorStackTrace = testResult.ErrorStackTrace
                });

            return new RunTestResponse
            {
                Results = results.ToArray(),
                Pass = !testResults.Any(r => r.Outcome == LegacyTestOutcome.Failed)
            };
        }

        public override GetTestStartInfoResponse GetTestStartInfo(string methodName, string testFrameworkName)
        {
            var testFramework = TestFramework.GetFramework(testFrameworkName);
            if (testFramework == null)
            {
                throw new InvalidOperationException($"Unknown test framework: {testFrameworkName}");
            }

            SendMessage(TestExecution_GetTestRunnerProcessStartInfo);

            var message = ReadMessage();

            var testStartInfo = message.DeserializePayload<TestStartInfo>();

            var arguments = testStartInfo.Arguments;

            var endIndex = arguments.IndexOf("--designtime");
            if (endIndex >= 0)
            {
                arguments = arguments.Substring(0, endIndex).TrimEnd();
            }
            
            if (!string.IsNullOrEmpty(methodName))
            {
                arguments = $"{arguments} {testFramework.MethodArgument} {methodName}";
            }

            return new GetTestStartInfoResponse
            {
                Executable = testStartInfo.FileName,
                Argument = arguments,
                WorkingDirectory = WorkingDirectory
            };
        }

        public override Task<DebugTestGetStartInfoResponse> DebugGetStartInfoAsync(string methodName, string testFrameworkName, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public override Task DebugLaunchAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
