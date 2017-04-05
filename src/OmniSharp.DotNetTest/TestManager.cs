using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.ObjectModel;
using OmniSharp.DotNetTest.Models;
using OmniSharp.Services;
using OmniSharp.Utilities;

namespace OmniSharp.DotNetTest
{
    public abstract class TestManager : DisposableObject
    {
        protected readonly Project Project;
        protected readonly DotNetCliService DotNetCli;
        protected readonly IEventEmitter EventEmitter;
        protected readonly ILogger Logger;
        protected readonly string WorkingDirectory;

        private bool _isConnected;
        private Process _process;
        private Socket _socket;
        private NetworkStream _stream;
        private BinaryReader _reader;
        private BinaryWriter _writer;
        private StringBuilder _outputBuilder;
        private StringBuilder _errorBuilder;

        public bool IsConnected => _isConnected;

        protected TestManager(Project project, string workingDirectory, DotNetCliService dotNetCli, IEventEmitter eventEmitter, ILogger logger)
        {
            Project = project ?? throw new ArgumentNullException(nameof(project));
            WorkingDirectory = workingDirectory ?? throw new ArgumentNullException(nameof(workingDirectory));
            DotNetCli = dotNetCli ?? throw new ArgumentNullException(nameof(dotNetCli));
            EventEmitter = eventEmitter ?? throw new ArgumentNullException(nameof(eventEmitter));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public static TestManager Start(Project project, DotNetCliService dotNetCli, IEventEmitter eventEmitter, ILoggerFactory loggerFactory)
        {
            var manager = Create(project, dotNetCli, eventEmitter, loggerFactory);
            manager.Connect();
            return manager;
        }

        public static TestManager Create(Project project, DotNetCliService dotNetCli, IEventEmitter eventEmitter, ILoggerFactory loggerFactory)
        {
            var workingDirectory = Path.GetDirectoryName(project.FilePath);
            var version = dotNetCli.GetVersion(workingDirectory);

            if (version.Major < 1)
            {
                throw new InvalidOperationException($"'dotnet test' is not supported for .NET CLI {version}");
            }

            if (version.Major == 1 &&
                version.Minor == 0 &&
                version.Patch == 0)
            {
                if (version.Release.StartsWith("preview1") ||
                    version.Release.StartsWith("preview2"))
                {
                    return new LegacyTestManager(project, workingDirectory, dotNetCli, eventEmitter, loggerFactory);
                }
            }

            return new VSTestManager(project, workingDirectory, dotNetCli, eventEmitter, loggerFactory);
        }

        protected abstract string GetCliTestArguments(int port, int parentProcessId);
        protected abstract void VersionCheck();

        public abstract RunDotNetTestResponse RunTest(string methodName, string testFrameworkName);
        public abstract GetDotNetTestStartInfoResponse GetTestStartInfo(string methodName, string testFrameworkName);

        protected virtual bool PrepareToConnect()
        {
            // Descendents can override.
            return true;
        }

        private void Connect()
        {
            if (_isConnected)
            {
                throw new InvalidOperationException("Already connected.");
            }

            if (!PrepareToConnect())
            {
                return;
            }

            var port = FindFreePort();

            var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(new IPEndPoint(IPAddress.Loopback, port));
            listener.Listen(1);

            var currentProcess = Process.GetCurrentProcess();
            _process = DotNetCli.Start(GetCliTestArguments(port, currentProcess.Id), WorkingDirectory);

            _outputBuilder = new StringBuilder();
            _errorBuilder = new StringBuilder();

            _process.OutputDataReceived += (_, e) => _outputBuilder.AppendLine(e.Data);
            _process.ErrorDataReceived += (_, e) => _errorBuilder.AppendLine(e.Data);

            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            _socket = listener.Accept();
            _stream = new NetworkStream(_socket);
            _reader = new BinaryReader(_stream);
            _writer = new BinaryWriter(_stream);

            // Read the initial "connected" response
            var message = ReadMessage();

            if (message.MessageType != MessageType.SessionConnected)
            {
                throw new InvalidOperationException($"Expected {MessageType.SessionConnected} but was {message.MessageType}");
            }

            VersionCheck();

            _isConnected = true;
        }

        protected override void DisposeCore(bool disposing)
        {
            if (_isConnected)
            {
                SendMessage(MessageType.SessionEnd);
                _process.WaitForExit(2000);
            }

            if (_process?.HasExited == false)
            {
                _process.KillChildrenAndThis();
            }

            if (_process != null)
            {
                _process = null;
            }

            if (_reader != null)
            {
                _reader.Dispose();
                _reader = null;
            }

            if (_writer != null)
            {
                _writer.Dispose();
                _writer = null;
            }

            if (_stream != null)
            {
                _stream.Dispose();
                _stream = null;
            }

            if (_socket != null)
            {
                _socket.Dispose();
                _socket = null;
            }

            _isConnected = false;
        }

        private static int FindFreePort()
        {
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                return ((IPEndPoint)socket.LocalEndPoint).Port;
            }
        }

        protected Message ReadMessage()
        {
            var rawMessage = _reader.ReadString();
            Logger.LogInformation($"read: {rawMessage}");

            return JsonDataSerializer.Instance.DeserializeMessage(rawMessage);
        }

        protected void SendMessage(string messageType)
        {
            var rawMessage = JsonDataSerializer.Instance.SerializePayload(messageType, new object());
            Logger.LogInformation($"send: {rawMessage}");

            _writer.Write(rawMessage);
        }

        protected void SendMessage<T>(string messageType, T payload)
        {
            var rawMessage = JsonDataSerializer.Instance.SerializePayload(messageType, payload);
            Logger.LogInformation($"send: {rawMessage}");

            _writer.Write(rawMessage);
        }
    }
}
