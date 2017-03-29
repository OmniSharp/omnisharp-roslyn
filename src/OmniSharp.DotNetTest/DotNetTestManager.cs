using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Testing.Abstractions;
using OmniSharp.DotNetTest.Models;
using OmniSharp.DotNetTest.Models.DotNetTest;
using OmniSharp.DotNetTest.TestFrameworks;
using OmniSharp.Services;
using OmniSharp.Utilities;

namespace OmniSharp.DotNetTest
{
    public class DotNetTestManager : TestManager
    {
        public DotNetTestManager(Process process, BinaryReader reader, BinaryWriter writer, string workingDirectory, ILogger logger)
            : base(process, reader, writer, workingDirectory, logger)
        {
            // Read the inital response
            var message = ReadMessage();
        }

        public static DotNetTestManager Start(string workingDirectory, DotNetCliService dotNetCli, ILoggerFactory loggerFactory)
        {
            var port = FindFreePort();

            var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(new IPEndPoint(IPAddress.Loopback, port));
            listener.Listen(1);

            var currentProcess = Process.GetCurrentProcess();
            var process = dotNetCli.Start($"test --port {port} --parentProcessId {currentProcess.Id}", workingDirectory);

            var socket = listener.Accept();
            var stream = new NetworkStream(socket);
            var reader = new BinaryReader(stream);
            var writer = new BinaryWriter(stream);

            return new DotNetTestManager(process, reader, writer, workingDirectory, loggerFactory.CreateLogger<DotNetTestManager>());
        }

        public RunDotNetTestResponse ExecuteTestMethod(string methodName, string testFrameworkName)
        {
            var testFramework = TestFramework.GetFramework(testFrameworkName);
            if (testFramework == null)
            {
                throw new InvalidOperationException($"Unknown test framework: {testFrameworkName}");
            }

            SendMessage("TestExecution.GetTestRunnerProcessStartInfo");

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
            while (true)
            {
                var m = ReadMessage();

                if (m.MessageType == "TestExecution.TestResult")
                {
                    results.Add(m.Payload.ToObject<TestResult>());
                }
                else if (m.MessageType == "TestExecution.Completed")
                {
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

            SendMessage("TestExecution.GetTestRunnerProcessStartInfo");

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

        private static int FindFreePort()
        {
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                return ((IPEndPoint)socket.LocalEndPoint).Port;
            }
        }
    }
}
