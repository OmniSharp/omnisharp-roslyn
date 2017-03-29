using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Testing.Abstractions;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.ObjectModel;
using OmniSharp.DotNetTest.Models;
using OmniSharp.DotNetTest.TestFrameworks;
using OmniSharp.Services;
using OmniSharp.Utilities;

namespace OmniSharp.DotNetTest
{
    public partial class DotNetTestManager : TestManager
    {
        private const string TestExecution_GetTestRunnerProcessStartInfo = "TestExecution.GetTestRunnerProcessStartInfo";
        private const string TestExecution_TestResult = "TestExecution.TestResult";

        public DotNetTestManager(string workingDirectory, DotNetCliService dotNetCli, ILogger logger)
            : base(dotNetCli, workingDirectory, logger)
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

        public static DotNetTestManager Start(string workingDirectory, DotNetCliService dotNetCli, ILoggerFactory loggerFactory)
        {
            var manager = new DotNetTestManager(workingDirectory, dotNetCli, loggerFactory.CreateLogger<DotNetTestManager>());
            manager.Connect();
            return manager;
        }

        public RunDotNetTestResponse ExecuteTestMethod(string methodName, string testFrameworkName)
        {
            var testFramework = TestFramework.GetFramework(testFrameworkName);
            if (testFramework == null)
            {
                throw new InvalidOperationException($"Unknown test framework: {testFrameworkName}");
            }

            SendMessage(TestExecution_GetTestRunnerProcessStartInfo);

            var message = ReadMessage();

            var testStartInfo = message.DeserializePayload<TestStartInfo>();

            var fileName = testStartInfo.FileName;
            var arguments = $"{testStartInfo.Arguments} {testFramework.MethodArgument} {methodName}";

            var startInfo = new ProcessStartInfo(fileName, arguments)
            {
                WorkingDirectory = WorkingDirectory,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false
            };

            var testProcess = Process.Start(startInfo);

            var results = new List<TestResult>();
            var done = false;

            while (!done)
            {
                var m = ReadMessage();
                switch (m.MessageType)
                {
                    case TestExecution_TestResult:
                        results.Add(m.DeserializePayload<TestResult>());
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

            return new RunDotNetTestResponse
            {
                Pass = !results.Any(r => r.Outcome == TestOutcome.Failed)
            };
        }

        public GetDotNetTestStartInfoResponse GetTestStartInfo(string methodName, string testFrameworkName)
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

            return new GetDotNetTestStartInfoResponse
            {
                Executable = testStartInfo.FileName,
                Argument = arguments
            };
        }
    }
}
