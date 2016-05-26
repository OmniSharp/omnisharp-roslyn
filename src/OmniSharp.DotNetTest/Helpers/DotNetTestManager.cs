using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OmniSharp.DotNetTest.Models;

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
            ReadMessage();
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

        public GetTestStartInfoResponse GetTestStartInfo(string methodName)
        {
            SendMessage(new { MessageType = "TestExecution.GetTestRunnerProcessStartInfo" });

            var message = ReadMessage();
            var filename = message.Payload["FileName"].Value<string>();
            var argument = message.Payload["Arguments"].Value<string>();
            var end = argument.IndexOf("--designtime");
            argument = argument.Substring(0, end);

            if (!string.IsNullOrEmpty(methodName))
            {
                argument = $"{argument} -method {methodName}";
            }

            return new GetTestStartInfoResponse
            {
                Argument = argument,
                Executable = filename
            };
        }

        public void Dispose()
        {
            if (_process != null && !_process.HasExited)
            {
                _process.Kill();
            }
        }

        private Message ReadMessage()
        {
            var content = _reader.ReadString();
            _logger.LogInformation($"read: {content}");

            return JsonConvert.DeserializeObject<Message>(content);
        }

        private void SendMessage(object message)
        {
            var content = JsonConvert.SerializeObject(message);
            _logger.LogInformation($"send: {content}");

            _writer.Write(content);
        }

        private class Message
        {
            public string MessageType { get; set; }

            public JToken Payload { get; set; }
        }

        private static Process StartDotnet(string argument, string workingDir)
        {
            var startInfo = new ProcessStartInfo("dotnet", argument)
            {
                WorkingDirectory = workingDir,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false
            };

            return Process.Start(startInfo);
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