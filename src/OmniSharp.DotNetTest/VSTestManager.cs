using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using NuGet.Versioning;
using OmniSharp.DotNetTest.Models;
using OmniSharp.DotNetTest.TestFrameworks;
using OmniSharp.Eventing;
using OmniSharp.Extensions;
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

        protected override bool PrepareToConnect(bool noBuild)
        {
            if (noBuild)
            {
                return true;
            }

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

        public override async Task<DiscoverTestsResponse> DiscoverTestsAsync(string runSettings, string testFrameworkName, string targetFrameworkVersion, CancellationToken cancellationToken)
        {
            var testCases = await DiscoverTestsAsync(null, runSettings, targetFrameworkVersion, cancellationToken);
            return new DiscoverTestsResponse
            {
                Tests = testCases.Select(o => new Test
                {
                    FullyQualifiedName = o.FullyQualifiedName,
                    DisplayName = o.DisplayName,
                    Source = o.Source,
                    CodeFilePath = o.CodeFilePath,
                    LineNumber = o.LineNumber
                }).ToArray()
            };
        }

        public override async Task<GetTestStartInfoResponse> GetTestStartInfoAsync(string methodName, string runSettings, string testFrameworkName, string targetFrameworkVersion, CancellationToken cancellationToken)
        {
            VerifyTestFramework(testFrameworkName);

            var testCases = await DiscoverTestsAsync(new string[] { methodName }, runSettings, targetFrameworkVersion, cancellationToken);

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
                EnvironmentVariables = startInfo.EnvironmentVariables,
                Succeeded = true
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

#nullable enable
        public override async Task<(string[]? MethodNames, string? TestFramework)> GetContextTestMethodNames(int line, int column, Document contextDocument, CancellationToken cancellationToken)
        {
            Logger.LogDebug($"Loading info for {contextDocument.FilePath} {line}:{column}");
            var syntaxTree = await contextDocument.GetSyntaxTreeAsync(cancellationToken);
            if (syntaxTree is null)
            {
                return default;
            }

            var semanticModel = await contextDocument.GetSemanticModelAsync(cancellationToken);
            if (semanticModel is null)
            {
                return default;
            }

            var sourceText = await contextDocument.GetTextAsync();

            var position = sourceText.Lines.GetPosition(new LinePosition(line, column));
            var node = (await syntaxTree.GetRootAsync()).FindToken(position).Parent;

            string[]? methodNames = null;
            TestFramework? testFramework = null;

            while (node is object)
            {
                if (node is MethodDeclarationSyntax methodDeclaration)
                {
                    // If a user invokes a test before or after a test method, it's likely that
                    // they meant the context to be the entire containing type, not the current
                    // methodsyntax to which the trivia was attached to. If we're in that scenario,
                    // just continue searching up.
                    if (position < methodDeclaration.SpanStart || position >= methodDeclaration.Span.End)
                    {
                        node = node.Parent;
                        continue;
                    }

                    var methodSymbol = semanticModel.GetDeclaredSymbol(methodDeclaration, cancellationToken);
                    if (methodSymbol is null)
                    {
                        Logger.LogWarning($"Could not find method symbol for method syntax {node} {contextDocument.FilePath} {node.SpanStart}:{node.Span.End}. This should not be possible.");
                        Debug.Fail($"Did not find method symbol");
                        continue;
                    }

                    if (isTestMethod(methodSymbol, ref testFramework))
                    {
                        methodNames = new[] { methodSymbol.GetMetadataName() };
                        Logger.LogDebug($"Found test method {methodNames[0]}");
                        break;
                    }

                    Logger.LogDebug($"Method {methodSymbol.Name} is not a test method, searching containing type");
                }
                else if (node is ClassDeclarationSyntax classDeclaration)
                {
                    var typeSymbol = semanticModel.GetDeclaredSymbol(classDeclaration);
                    if (typeSymbol is null)
                    {
                        Logger.LogWarning($"Could not find type symbol for class declaration syntax {node} {contextDocument.FilePath} {node.SpanStart}:{node.Span.End}. This should not be possible.");
                        Debug.Fail($"Did not find class symbol symbol");
                        continue;
                    }

                    var members = typeSymbol.GetMembers();
                    ImmutableArray<string>.Builder? nameBuilder = null;

                    foreach (var member in members)
                    {
                        if (!(member is IMethodSymbol methodSymbol) || !isTestMethod(methodSymbol, ref testFramework))
                        {
                            continue;
                        }

                        // This might be longer than the members we end up needing, but at least we won't do expensive
                        // array reallocation during search.
                        nameBuilder ??= ImmutableArray.CreateBuilder<string>(members.Length);
                        nameBuilder.Add(member.GetMetadataName());
                    }

                    if (nameBuilder is object)
                    {
                        methodNames = nameBuilder.ToArray();
                        Logger.LogDebug($"Found test methods {string.Join(", ", methodNames)}");
                        break;
                    }

                    Logger.LogDebug($"Class {typeSymbol.Name} does not contain test methods, searching containing type (if applicable)");
                }

                node = node.Parent;
            }

            return (methodNames, testFramework?.Name);

            static bool isTestMethod(IMethodSymbol methodSymbol, ref TestFramework? framework)
            {
                if (framework is object)
                {
                    return framework.IsTestMethod(methodSymbol);
                }

                foreach (var f in TestFramework.Frameworks)
                {
                    if (f.IsTestMethod(methodSymbol))
                    {
                        framework = f;
                        return true;
                    }
                }

                return false;
            }
        }
#nullable restore

        public override Task<RunTestResponse> RunTestAsync(string methodName, string runSettings, string testFrameworkName, string targetFrameworkVersion, CancellationToken cancellationToken)
            => RunTestAsync(new string[] { methodName }, runSettings, testFrameworkName, targetFrameworkVersion, cancellationToken);

        public override async Task<RunTestResponse> RunTestAsync(string[] methodNames, string runSettings, string testFrameworkName, string targetFrameworkVersion, CancellationToken cancellationToken)
        {
            VerifyTestFramework(testFrameworkName);

            var testCases = await DiscoverTestsAsync(methodNames, runSettings, targetFrameworkVersion, cancellationToken);

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
                Pass = !testResults.Any(r => r.Outcome == TestOutcome.Failed),
                ContextHadNoTests = false
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
            HashSet<string> hashset = null;
            if (methodNames != null)
            {
                hashset = new HashSet<string>(methodNames);
            }

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
                        testCases.AddRange(methodNames != null ? foundTestCases.Where(isInRequestedMethods) : foundTestCases);
                        break;

                    case MessageType.DiscoveryComplete:
                        var lastDiscoveredTests = message.DeserializePayload<DiscoveryCompletePayload>().LastDiscoveredTests;
                        if (lastDiscoveredTests != null)
                        {
                            testCases.AddRange(methodNames != null ? lastDiscoveredTests.Where(isInRequestedMethods) : lastDiscoveredTests);
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

                // Discovered tests in generic classes come back in the form `Namespace.GenericClass<TParam>.TestName`
                // however requested test names are sent from the IDE in the form of `Namespace.GenericClass`1.TestName`
                // to compensate we format each part of the discovered test name to match what the IDE would send.
                testName = string.Join(".", testName.Split('.').Select(FormatAsMetadata));

                return hashset.Contains(testName, StringComparer.Ordinal);
            };

            static string FormatAsMetadata(string name)
            {
                if (!name.EndsWith(">"))
                {
                    return name;
                }

                var genericParamStart = name.IndexOf('<');
                if (genericParamStart < 0)
                {
                    return name;
                }

                var genericParams = name.Substring(genericParamStart, name.Length - genericParamStart - 1);
                var paramCount = genericParams.Split(',').Length;
                return $"{name.Substring(0, genericParamStart)}`{paramCount}";
            }
        }
    }
}
