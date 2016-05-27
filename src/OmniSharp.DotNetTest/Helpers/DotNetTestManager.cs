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

namespace OmniSharp.DotNetTest.Helpers.DotNetTestManager
{
    public class DotNetTestManager : IDisposable
    {
        private readonly string _projectDir;
        private readonly Process _process;
        private readonly BinaryReader _reader;
        private readonly BinaryWriter _writer;
        private readonly ILogger _logger;

        public DotNetTestManager(string projectDir, ILogger logger, Process process, BinaryReader reader, BinaryWriter writer)
        {
            _projectDir = projectDir;
            _logger = logger;
            _process = process;
            _reader = reader;
            _writer = writer;

            // Read the inital response
            ReadMessage<JToken>();
        }

        public static DotNetTestManager Start(string projectDir, ILoggerFactory loggerFactory)
        {
            var port = FindFreePort();

            var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(new IPEndPoint(IPAddress.Loopback, port));
            listener.Listen(1);

            var process = StartDotnet($"test --port {port} --parentProcessId {Process.GetCurrentProcess().Id}", projectDir);
            var stream = new NetworkStream(listener.Accept());
            var reader = new BinaryReader(stream);
            var writer = new BinaryWriter(stream);

            return new DotNetTestManager(projectDir, loggerFactory.CreateLogger<DotNetTestManager>(), process, reader, writer);
        }

        public RunDotNetTestResponse ExecuteTestMethod(string methodName)
        {
            SendMessage(new { MessageType = "TestExecution.GetTestRunnerProcessStartInfo" });

            var startInfo = ReadMessage<TestStartInfo>();
            var testProcess = StartProcess(
                startInfo.Payload.FileName,
                $"{startInfo.Payload.Arguments} -method {methodName}",
                _projectDir);

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

            return new RunDotNetTestResponse
            {
                Pass = !results.Any(r => r.Outcome == TestOutcome.Failed)
            };
        }

        public GetTestStartInfoResponse GetTestStartInfo(string methodName)
        {
            SendMessage(new { MessageType = "TestExecution.GetTestRunnerProcessStartInfo" });

            var message = ReadMessage<TestStartInfo>();
            var end = message.Payload.Arguments.IndexOf("--designtime");
            var argument = message.Payload.Arguments.Substring(0, end);

            if (!string.IsNullOrEmpty(methodName))
            {
                argument = $"{argument} -method {methodName}";
            }

            return new GetTestStartInfoResponse
            {
                Argument = argument,
                Executable = message.Payload.FileName
            };
        }

        public void Dispose()
        {
            if (_process != null && !_process.HasExited)
            {
                _process.Kill();
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

        private static Process StartDotnet(string argument, string workingDir)
        {
            return StartProcess("dotnet", argument, workingDir);
        }

        private static Process StartProcess(string executable, string argument, string workingDir)
        {
            return Process.Start(new ProcessStartInfo(executable, argument)
            {
                WorkingDirectory = workingDir,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false
            });
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