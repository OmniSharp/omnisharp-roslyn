using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OmniSharp.Models.V2;

namespace OmniSharp.Roslyn.CSharp.Services.Testing
{

    // TODO: Move to it's own project eventually
    internal class XunitTestActionDebugger : ITestActionRunner
    {
        private readonly string _action;
        private readonly string _method;
        private readonly string _projectFolder;
        private readonly ILogger _logger;

        public XunitTestActionDebugger(string action, string method, string filepath, ILoggerFactory loggerFactory)
        {
            _action = action;
            _method = method;
            _logger = loggerFactory.CreateLogger<XunitTestActionDebugger>();

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

        public Task<ITestActionResult> RunAsync()
        {
            var port = FindFreePort();
            var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(new IPEndPoint(IPAddress.Loopback, port));
            listener.Listen(10);

            var testController = StartDotnet($"test --port {port} --parentProcessId {Process.GetCurrentProcess().Id}");

            var stream = new NetworkStream(listener.Accept());
            var reader = new BinaryReader(stream);
            var writer = new BinaryWriter(stream);

            ReadMessage(reader);
            SendMessage(writer, new { MessageType = "TestExecution.GetTestRunnerProcessStartInfo" });

            var message = ReadMessage(reader);
            var filename = message.Payload["FileName"].Value<string>();
            var argument = message.Payload["Arguments"].Value<string>();
            var end = argument.IndexOf("--designtime");
            argument = argument.Substring(0, end) + $"-method {_method}";

            _logger.LogInformation($"argu: {argument}");

            testController.Kill();
            
            return Task.FromResult<ITestActionResult>(new TestDebuggerResult(filename, argument));
        }

        public override string ToString()
        {
            return $"{nameof(XunitTestActionDebugger)} action: {_action}, method: {_method}";
        }

        private Process StartDotnet(string argument)
        {
            var startInfo = new ProcessStartInfo("dotnet", argument)
            {
                WorkingDirectory = _projectFolder,
                CreateNoWindow = true,
                UseShellExecute = true,
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

        private Message ReadMessage(BinaryReader reader)
        {
            var content = reader.ReadString();
            _logger.LogInformation($"read: {content}");

            return JsonConvert.DeserializeObject<Message>(content);
        }

        private void SendMessage(BinaryWriter writer, object message)
        {
            var content = JsonConvert.SerializeObject(message);
            _logger.LogInformation($"send: {content}");

            writer.Write(content);
            writer.Flush();
        }

        private class Message
        {
            public string MessageType { get; set; }

            public JToken Payload { get; set; }
        }

        private class TestDebuggerResult : ITestActionResult
        {
            private readonly string _filename;
            private readonly string _argument;
            public TestDebuggerResult(string filename, string argument)
            {
                _filename = filename;
                _argument = argument;    
            }
            
            public RunCodeActionResponse ToRespnse()
            {
                return new RunCodeActionResponse { DebugTestCommand = $"{_filename} {_argument}"};
            }
        }
    }
}