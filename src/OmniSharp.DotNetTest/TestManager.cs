using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using NuGet.Versioning;
using OmniSharp.DotNetTest.Models;
using OmniSharp.DotNetTest.Models.Events;
using OmniSharp.Eventing;
using OmniSharp.Services;
using OmniSharp.Utilities;

namespace OmniSharp.DotNetTest
{
    internal abstract class TestManager : DisposableObject
    {
        protected readonly Project Project;
        protected readonly IDotNetCliService DotNetCli;
        protected readonly SemanticVersion DotNetCliVersion;
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

        protected TestManager(Project project, string workingDirectory, IDotNetCliService dotNetCli, SemanticVersion dotNetCliVersion, IEventEmitter eventEmitter, ILogger logger)
        {
            Project = project ?? throw new ArgumentNullException(nameof(project));
            WorkingDirectory = workingDirectory ?? throw new ArgumentNullException(nameof(workingDirectory));
            DotNetCli = dotNetCli ?? throw new ArgumentNullException(nameof(dotNetCli));
            DotNetCliVersion = dotNetCliVersion ?? throw new ArgumentNullException(nameof(dotNetCliVersion));
            EventEmitter = eventEmitter ?? throw new ArgumentNullException(nameof(eventEmitter));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public static TestManager Start(Project project, IDotNetCliService dotNetCli, IEventEmitter eventEmitter, ILoggerFactory loggerFactory)
        {
            var manager = Create(project, dotNetCli, eventEmitter, loggerFactory);
            manager.Connect();
            return manager;
        }

        public static TestManager Create(Project project, IDotNetCliService dotNetCli, IEventEmitter eventEmitter, ILoggerFactory loggerFactory)
        {
            var workingDirectory = Path.GetDirectoryName(project.FilePath);

            var version = dotNetCli.GetVersion(workingDirectory);

            if (dotNetCli.IsLegacy(version))
            {
                throw new NotSupportedException("Legacy .NET SDK is not supported");
            }
            
            return (TestManager)new VSTestManager(project, workingDirectory, dotNetCli, version, eventEmitter, loggerFactory);
        }

        protected abstract string GetCliTestArguments(int port, int parentProcessId);
        protected abstract void VersionCheck();

        public abstract RunTestResponse RunTest(string methodName, string runSettings, string testFrameworkName, string targetFrameworkVersion);

        public virtual RunTestResponse RunTest(string[] methodNames, string runSettings, string testFrameworkName, string targetFrameworkVersion)
        { 
            throw new NotImplementedException();
        }

        public abstract GetTestStartInfoResponse GetTestStartInfo(string methodName, string runSettings, string testFrameworkName, string targetFrameworkVersion);

        public abstract Task<DebugTestGetStartInfoResponse> DebugGetStartInfoAsync(string methodName, string runSettings, string testFrameworkName, string targetFrameworkVersion, CancellationToken cancellationToken);

        public virtual Task<DebugTestGetStartInfoResponse> DebugGetStartInfoAsync(string[] methodNames, string runSettings, string testFrameworkName, string targetFrameworkVersion, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public abstract Task DebugLaunchAsync(CancellationToken cancellationToken);

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

        protected void EmitTestMessage(TestMessageLevel messageLevel, string message)
        {
            EventEmitter.Emit(TestMessageEvent.Id,
                new TestMessageEvent
                {
                    MessageLevel = messageLevel.ToString().ToLowerInvariant(),
                    Message = message
                });
        }

        protected void EmitTestMessage(TestMessagePayload testMessage)
        {
            EmitTestMessage(testMessage.MessageLevel, testMessage.Message);
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
            Logger.LogDebug($"send: {rawMessage}");

            _writer.Write(rawMessage);
        }

        protected void SendMessage<T>(string messageType, T payload)
        {
            var rawMessage = JsonDataSerializer.Instance.SerializePayload(messageType, payload);
            Logger.LogDebug($"send: {rawMessage}");

            _writer.Write(rawMessage);
        }

        protected async Task<(bool succeeded, Message message)> TryReadMessageAsync(CancellationToken cancellationToken)
        {
            var rawMessage = await Task.Run(() => ReadRawMessage(cancellationToken));

            if (rawMessage == null)
            {
                return (succeeded: false, message: null);
            }

            Logger.LogDebug($"read: {rawMessage}");

            return (succeeded: true, message: JsonDataSerializer.Instance.DeserializeMessage(rawMessage));
        }

        protected async Task<Message> ReadMessageAsync(CancellationToken cancellationToken)
        {
            var rawMessage = await Task.Run(() => ReadRawMessage(cancellationToken));

            cancellationToken.ThrowIfCancellationRequested();

            Logger.LogDebug($"read: {rawMessage}");

            return JsonDataSerializer.Instance.DeserializeMessage(rawMessage);
        }

        private string ReadRawMessage(CancellationToken cancellationToken)
        {
            const int Timeout = 1000 * 1000;

            string str = null;
            bool success = false;

            // We set a read timeout below to avoid blocking.
            while (!cancellationToken.IsCancellationRequested && !success && IsConnected && !IsDisposed)
            {
                try
                {
                    if (this._socket.Poll(Timeout, SelectMode.SelectRead))
                    {
                        str = _reader.ReadString();
                        success = true;
                    }
                }
                catch (IOException ex) when (ex.InnerException is SocketException se &&
                                             se.SocketErrorCode == SocketError.TimedOut)
                {
                    Logger.LogTrace(se, $"{nameof(ReadRawMessage)}: failed to receive message because it timed out.");
                }
                catch (Exception ex)
                {
                    Logger.LogTrace(ex, $"{nameof(ReadRawMessage)}: failed to receive message.");
                    break;
                }
            }

            return str;
        }
    }
}
