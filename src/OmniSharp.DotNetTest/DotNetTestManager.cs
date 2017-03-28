using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Testing.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OmniSharp.DotNetTest.Models;
using OmniSharp.DotNetTest.Models.DotNetTest;
using OmniSharp.DotNetTest.TestFrameworks;
using OmniSharp.Services;
using OmniSharp.Utilities;

namespace OmniSharp.DotNetTest
{
    public class DotNetTestManager : IDisposable
    {
        private readonly string _workingDirectory;
        private readonly Process _process;
        private readonly BinaryReader _reader;
        private readonly BinaryWriter _writer;
        private readonly ILogger _logger;

        public DotNetTestManager(string workingDirectory, ILogger logger, Process process, BinaryReader reader, BinaryWriter writer)
        {
            _workingDirectory = workingDirectory;
            _logger = logger;
            _process = process;
            _reader = reader;
            _writer = writer;

            // Read the inital response
            ReadMessage<JToken>();
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

            return new DotNetTestManager(workingDirectory, loggerFactory.CreateLogger<DotNetTestManager>(), process, reader, writer);
        }

        public RunDotNetTestResponse ExecuteTestMethod(string methodName, string testFrameworkName)
        {
            var testFramework = TestFramework.GetFramework(testFrameworkName);
            if (testFramework == null)
            {
                throw new InvalidOperationException($"Unknown test framework: {testFrameworkName}");
            }

            SendMessage(new { MessageType = "TestExecution.GetTestRunnerProcessStartInfo" });

            var testStartInfo = ReadMessage<TestStartInfo>();

            var fileName = testStartInfo.Payload.FileName;
            var arguments = $"{testStartInfo.Payload.Arguments} {testFramework.MethodArgument} {methodName}";

            var startInfo = new ProcessStartInfo(fileName, arguments)
            {
                WorkingDirectory = _workingDirectory,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false
            };

            var testProcess = Process.Start(startInfo);

            var results = new List<TestResult>();
            while (true)
            {
                var message = ReadMessage<JObject>();

                if (message.MessageType == "TestExecution.TestResult")
                {
                    results.Add(message.Payload.ToObject<TestResult>());
                }
                else if (message.MessageType == "TestExecution.Completed")
                {
                    break;
                }
            }

            if (!testProcess.HasExited)
            {
                if (!testProcess.WaitForExit(3000))
                {
                    testProcess.KillAll();
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

            SendMessage(new { MessageType = "TestExecution.GetTestRunnerProcessStartInfo" });

            var testStartInfo = ReadMessage<TestStartInfo>();

            var fileName = testStartInfo.Payload.FileName;
            var arguments = testStartInfo.Payload.Arguments;

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
                Executable = fileName,
                Argument = arguments
            };
        }

        public void Dispose()
        {
            if (_process != null && !_process.HasExited)
            {
                _process.KillAll();
            }
        }

        private Message<T> ReadMessage<T>()
        {
            var content = _reader.ReadString();
            _logger.LogInformation($"read: {content}");

            return JsonConvert.DeserializeObject<Message<T>>(content);
        }

        private void SendMessage(object message)
        {
            var content = JsonConvert.SerializeObject(message);
            _logger.LogInformation($"send: {content}");

            _writer.Write(content);
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
