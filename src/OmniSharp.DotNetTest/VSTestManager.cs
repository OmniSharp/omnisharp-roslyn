using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using NuGet.Versioning;
using OmniSharp.DotNetTest.Models;
using OmniSharp.DotNetTest.TestFrameworks;
using OmniSharp.Eventing;
using OmniSharp.Services;

namespace OmniSharp.DotNetTest
{
    internal class VSTestManager : TestManager
    {
        public VSTestManager(Project project, string workingDirectory, IDotNetCliService dotNetCli, SemanticVersion dotNetCliVersion, IEventEmitter eventEmitter, ILoggerFactory loggerFactory)
            : base(project, workingDirectory, dotNetCli, dotNetCliVersion, eventEmitter, loggerFactory.CreateLogger<VSTestManager>())
        {
        }

        private object LoadRunSettingsOrDefault(string runSettingsPath, string targetFrameworkVersion)
        {
            if (runSettingsPath != null)
            {
                try
                {
                    return File.ReadAllText(runSettingsPath);
                }
                catch (FileNotFoundException)
                {
                    EmitTestMessage(TestMessageLevel.Warning, $"RunSettings file {runSettingsPath} not found. Continuing with default settings...");
                }
                catch (Exception e)
                {
                    EmitTestMessage(TestMessageLevel.Warning, $"There was an error loading runsettings at {runSettingsPath}: {e}. Continuing with default settings...");
                }
            }

            if (!string.IsNullOrWhiteSpace(targetFrameworkVersion))
            {
                return $@"
<RunSettings>
    <RunConfiguration>
        <TargetFrameworkVersion>{targetFrameworkVersion}</TargetFrameworkVersion>
    </RunConfiguration>
</RunSettings>";
            }

            return "<RunSettings/>";
        }

        protected override string GetCliTestArguments(int port, int parentProcessId)
        {
            return $"vstest --Port:{port} --ParentProcessId:{parentProcessId}";
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
            var arguments = "build";

            // If this is .NET CLI version 2.0.0 or greater, we also specify --no-restore to ensure that
            // implicit restore on build doesn't slow the build down.
            if (DotNetCliVersion >= new SemanticVersion(2, 0, 0))
            {
                arguments += " --no-restore";
            }

            var process = DotNetCli.Start(arguments, WorkingDirectory);

            process.OutputDataReceived += (_, e) =>
            {
                EmitTestMessage(TestMessageLevel.Informational, e.Data ?? string.Empty);
            };

            process.ErrorDataReceived += (_, e) =>
            {
                EmitTestMessage(TestMessageLevel.Error, e.Data ?? string.Empty);
            };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            process.WaitForExit();

            return process.ExitCode == 0
                && File.Exists(Project.OutputFilePath);
        }

        private static void VerifyTestFramework(string testFrameworkName)
        {
            var testFramework = TestFramework.GetFramework(testFrameworkName);
            if (testFramework == null)
            {
                throw new InvalidOperationException($"Unknown test framework: {testFrameworkName}");
            }
        }

        public override GetTestStartInfoResponse GetTestStartInfo(string methodName, string runSettings, string testFrameworkName, string targetFrameworkVersion)
        {
            VerifyTestFramework(testFrameworkName);

            var testCases = DiscoverTests(new string[] { methodName }, runSettings, targetFrameworkVersion);

            SendMessage(MessageType.GetTestRunnerProcessStartInfoForRunSelected,
                new
                {
                    TestCases = testCases,
                    DebuggingEnabled = true,
                    RunSettings = LoadRunSettingsOrDefault(runSettings, targetFrameworkVersion)
                });

            var message = ReadMessage();
            var testStartInfo = message.DeserializePayload<TestProcessStartInfo>();

            return new GetTestStartInfoResponse
            {
                Executable = testStartInfo.FileName,
                Argument = testStartInfo.Arguments,
                WorkingDirectory = testStartInfo.WorkingDirectory
            };
        }

        public override async Task<DebugTestGetStartInfoResponse> DebugGetStartInfoAsync(string methodName, string runSettings, string testFrameworkName, string targetFrameworkVersion, CancellationToken cancellationToken)
         => await DebugGetStartInfoAsync(new string[] { methodName }, runSettings, testFrameworkName, targetFrameworkVersion, cancellationToken);

        public override async Task<DebugTestGetStartInfoResponse> DebugGetStartInfoAsync(string[] methodNames, string runSettings, string testFrameworkName, string targetFrameworkVersion, CancellationToken cancellationToken)
        {
            VerifyTestFramework(testFrameworkName);

            var testCases = await DiscoverTestsAsync(methodNames, runSettings, targetFrameworkVersion, cancellationToken);

            SendMessage(MessageType.GetTestRunnerProcessStartInfoForRunSelected,
                new
                {
                    TestCases = testCases,
                    DebuggingEnabled = true,
                    RunSettings = LoadRunSettingsOrDefault(runSettings, targetFrameworkVersion)
                });

            var message = await ReadMessageAsync(cancellationToken);
            var startInfo = message.DeserializePayload<TestProcessStartInfo>();

            return new DebugTestGetStartInfoResponse
            {
                FileName = startInfo.FileName,
                Arguments = startInfo.Arguments,
                WorkingDirectory = startInfo.WorkingDirectory,
                EnvironmentVariables = startInfo.EnvironmentVariables
            };
        }

        public override async Task DebugLaunchAsync(CancellationToken cancellationToken)
        {
            SendMessage(MessageType.CustomTestHostLaunchCallback,
                new
                {
                    HostProcessId = Process.GetCurrentProcess().Id
                });

            var done = false;

            while (!done)
            {
                var (success, message) = await TryReadMessageAsync(cancellationToken);
                if (!success)
                {
                    break;
                }

                switch (message.MessageType)
                {
                    case MessageType.TestMessage:
                        EmitTestMessage(message.DeserializePayload<TestMessagePayload>());
                        break;

                    case MessageType.ExecutionComplete:
                        done = true;
                        break;
                }
            }
        }

        public override RunTestResponse RunTest(string methodName, string runSettings, string testFrameworkName, string targetFrameworkVersion)
            => RunTest(new string[] { methodName }, runSettings, testFrameworkName, targetFrameworkVersion);

        public override RunTestResponse RunTest(string[] methodNames, string runSettings, string testFrameworkName, string targetFrameworkVersion)
        {
            VerifyTestFramework(testFrameworkName);

            var testCases = DiscoverTests(methodNames, runSettings, targetFrameworkVersion);

            var testResults = new List<TestResult>();

            if (testCases.Length > 0)
            {
                // Now, run the tests.
                SendMessage(MessageType.TestRunSelectedTestCasesDefaultHost,
                    new
                    {
                        TestCases = testCases,
                        RunSettings = LoadRunSettingsOrDefault(runSettings, targetFrameworkVersion)
                    });

                var done = false;

                while (!done)
                {
                    var message = ReadMessage();

                    switch (message.MessageType)
                    {
                        case MessageType.TestMessage:
                            EmitTestMessage(message.DeserializePayload<TestMessagePayload>());
                            break;

                        case MessageType.TestRunStatsChange:
                            var testRunChange = message.DeserializePayload<TestRunChangedEventArgs>();
                            testResults.AddRange(testRunChange.NewTestResults);
                            break;

                        case MessageType.ExecutionComplete:
                            var payload = message.DeserializePayload<TestRunCompletePayload>();
                            if (payload.LastRunTests != null && payload.LastRunTests.NewTestResults != null)
                            {
                                testResults.AddRange(payload.LastRunTests.NewTestResults);
                            }
                            done = true;
                            break;
                    }
                }
            }

            var results = testResults.Select(testResult =>
                new DotNetTestResult
                {
                    MethodName = testResult.TestCase.FullyQualifiedName,
                    Outcome = testResult.Outcome.ToString().ToLowerInvariant(),
                    ErrorMessage = testResult.ErrorMessage,
                    ErrorStackTrace = testResult.ErrorStackTrace,
                    StandardOutput = testResult.Messages
                        .Where(message => message.Category == TestResultMessage.StandardOutCategory)
                        .Select(message => message.Text).ToArray(),
                    StandardError = testResult.Messages.Where(message => message.Category == TestResultMessage.StandardErrorCategory)
                        .Select(message => message.Text).ToArray()
                });

            return new RunTestResponse
            {
                Results = results.ToArray(),
                Pass = !testResults.Any(r => r.Outcome == TestOutcome.Failed)
            };
        }

        private async Task<TestCase[]> DiscoverTestsAsync(string[] methodNames, string runSettings, string targetFrameworkVersion, CancellationToken cancellationToken)
        {
            SendMessage(MessageType.StartDiscovery,
                new
                {
                    Sources = new[]
                    {
                        Project.OutputFilePath
                    },
                    RunSettings = LoadRunSettingsOrDefault(runSettings, targetFrameworkVersion)
                });

            var testCases = new List<TestCase>();
            var done = false;
            var hashset = new HashSet<string>(methodNames);

            while (!done)
            {
                var (success, message) = await TryReadMessageAsync(cancellationToken);
                if (!success)
                {
                    return Array.Empty<TestCase>();
                }

                switch (message.MessageType)
                {
                    case MessageType.TestMessage:
                        EmitTestMessage(message.DeserializePayload<TestMessagePayload>());
                        break;

                    case MessageType.TestCasesFound:
                        var foundTestCases = message.DeserializePayload<TestCase[]>();
                        testCases.AddRange(foundTestCases.Where(isInRequestedMethods));
                        break;

                    case MessageType.DiscoveryComplete:
                        var lastDiscoveredTests = message.DeserializePayload<DiscoveryCompletePayload>().LastDiscoveredTests;
                        if (lastDiscoveredTests != null)
                        {
                            testCases.AddRange(lastDiscoveredTests.Where(isInRequestedMethods));
                        }

                        done = true;
                        break;
                }
            }

            return testCases.ToArray();

            // checks whether a discovered test case is matched with the list of the requested method names.
            bool isInRequestedMethods(TestCase testCase)
            {
                var testName = testCase.FullyQualifiedName;

                var testNameEnd = testName.IndexOf('(');
                if (testNameEnd >= 0)
                {
                    testName = testName.Substring(0, testNameEnd);
                }

                testName = testName.Trim();
                return hashset.Contains(testName, StringComparer.Ordinal);
            };
        }

        private TestCase[] DiscoverTests(string[] methodNames, string runSettings, string targetFrameworkVersion)
        {
            return DiscoverTestsAsync(methodNames, runSettings, targetFrameworkVersion, CancellationToken.None).Result;
        }
    }
}
