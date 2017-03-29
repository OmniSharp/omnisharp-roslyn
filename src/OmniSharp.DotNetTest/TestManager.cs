using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
using OmniSharp.Utilities;

namespace OmniSharp.DotNetTest
{
    public abstract class TestManager : DisposableObject
    {
        protected readonly string WorkingDirectory;
        private readonly Process _process;
        private readonly BinaryReader _reader;
        private readonly BinaryWriter _writer;
        private readonly ILogger _logger;

        protected TestManager(Process process, BinaryReader reader, BinaryWriter writer, string workingDirectory, ILogger logger)
        {
            _process = process ?? throw new ArgumentNullException(nameof(process));
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
            WorkingDirectory = workingDirectory ?? throw new ArgumentNullException(nameof(workingDirectory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override void DisposeCore(bool disposing)
        {
            if (!_process.HasExited)
            {
                _process.KillChildrenAndThis();
            }
        }

        protected Message ReadMessage()
        {
            var rawMessage = _reader.ReadString();
            _logger.LogInformation($"read: {rawMessage}");

            return JsonDataSerializer.Instance.DeserializeMessage(rawMessage);
        }

        protected void SendMessage(string messageType)
        {
            var rawMessage = JsonDataSerializer.Instance.SerializePayload(messageType, new object());
            _logger.LogInformation($"send: {rawMessage}");

            _writer.Write(rawMessage);
        }

        protected void SendMessage<T>(string messageType, T payload)
        {
            var rawMessage = JsonDataSerializer.Instance.SerializePayload(messageType, payload);
            _logger.LogInformation($"send: {rawMessage}");

            _writer.Write(rawMessage);
        }
    }
}
