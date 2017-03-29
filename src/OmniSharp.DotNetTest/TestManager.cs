using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OmniSharp.DotNetTest.Models.DotNetTest;
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

        protected Message<T> ReadMessage<T>()
        {
            var content = _reader.ReadString();
            _logger.LogInformation($"read: {content}");

            return JsonConvert.DeserializeObject<Message<T>>(content);
        }

        protected void SendMessage(object message)
        {
            var content = JsonConvert.SerializeObject(message);
            _logger.LogInformation($"send: {content}");

            _writer.Write(content);
        }
    }
}
