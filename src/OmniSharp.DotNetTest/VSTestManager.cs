using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using OmniSharp.DotNetTest.Models;
using OmniSharp.Services;

namespace OmniSharp.DotNetTest
{
    public class VSTestManager : TestManager
    {
        public VSTestManager(Project project, string workingDirectory, DotNetCliService dotNetCli, ILoggerFactory loggerFactory)
            : base(project, workingDirectory, dotNetCli, loggerFactory.CreateLogger<VSTestManager>())
        {
        }

        protected override string GetCliTestArguments(int port, int parentProcessId)
        {
            return $"vstest  --Port:{port} --ParentProcessId:{parentProcessId}";
        }

        protected override void VersionCheck()
        {
            SendMessage(MessageType.VersionCheck, 1);

            var message = ReadMessage();
            var version = message.DeserializePayload<int>();

            if (version != 1)
            {
                throw new InvalidOperationException($"Expected ProtocolVersion 1, but was {version}");
            }
        }

        protected override bool PrepareToConnect()
        {
            // The project must be built before we can test.
            if (!File.Exists(Project.OutputFilePath))
            {
                var process = DotNetCli.Start("build", WorkingDirectory);
                process.WaitForExit();
            }

            return File.Exists(Project.OutputFilePath);
        }

        public override GetDotNetTestStartInfoResponse GetTestStartInfo(string methodName, string testFrameworkName)
        {
            var testCases = DiscoverTests(methodName);

            SendMessage(MessageType.GetTestRunnerProcessStartInfoForRunSelected,
                new
                {
                    TestCases = testCases,
                    DebuggingEnabled = true
                });

            var message = ReadMessage();
            var startInfo = message.DeserializePayload<TestProcessStartInfo>();

            return new GetDotNetTestStartInfoResponse
            {
                Executable = startInfo.FileName,
                Argument = startInfo.Arguments,
                WorkingDirectory = startInfo.WorkingDirectory
            };
        }

        public override RunDotNetTestResponse RunTest(string methodName, string testFrameworkName)
        {
            var testCases = DiscoverTests(methodName);

            var results = new List<TestResult>();

            if (testCases.Length > 0)
            {
                // Now, run the tests.
                SendMessage(MessageType.TestRunSelectedTestCasesDefaultHost,
                    new
                    {
                        TestCases = testCases
                    });

                var done = false;

                while (!done)
                {
                    var message = ReadMessage();

                    switch (message.MessageType)
                    {
                        case MessageType.TestMessage:
                            break;

                        case MessageType.TestRunStatsChange:
                            var testRunChange = message.DeserializePayload<TestRunChangedEventArgs>();

                            results.AddRange(testRunChange.NewTestResults);
                            break;

                        case MessageType.ExecutionComplete:
                            var payload = message.DeserializePayload<TestRunCompletePayload>();

                            done = true;
                            break;
                    }
                }
            }

            return new RunDotNetTestResponse
            {
                Pass = !results.Any(r => r.Outcome == TestOutcome.Failed)
            };
        }

        private TestCase[] DiscoverTests(string methodName)
        {
            // First, we need to discover tests.
            SendMessage(MessageType.StartDiscovery,
                new
                {
                    Sources = new[]
                    {
                        Project.OutputFilePath
                    }
                });

            var testCases = new List<TestCase>();
            var done = false;

            while (!done)
            {
                var message = ReadMessage();

                switch (message.MessageType)
                {
                    case MessageType.TestMessage:
                        break;

                    case MessageType.TestCasesFound:
                        foreach (var testCase in message.DeserializePayload<TestCase[]>())
                        {
                            if (testCase.DisplayName.StartsWith(methodName))
                            {
                                testCases.Add(testCase);
                            }
                        }

                        break;

                    case MessageType.DiscoveryComplete:
                        done = true;
                        break;
                }
            }

            return testCases.ToArray();
        }
    }
}
