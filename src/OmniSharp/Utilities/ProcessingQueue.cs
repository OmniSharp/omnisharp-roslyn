using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Microsoft.Framework.DesignTimeHost.Models;
using Microsoft.Framework.Logging;
using Newtonsoft.Json;

namespace OmniSharp
{
    public class ProcessingQueue
    {
        private readonly BinaryReader _reader;
        private readonly BinaryWriter _writer;

        public event Action<Message> OnReceive;

        public ILogger Logger { get; private set; }

        public ProcessingQueue(Stream stream, ILogger logger)
        {
            _reader = new BinaryReader(stream);
            _writer = new BinaryWriter(stream);
            Logger = logger;
        }

        public void Start()
        {
            Logger.WriteVerbose("[ProcessingQueue]: Start()");
            new Thread(ReceiveMessages) { IsBackground = true }.Start();
        }

        public void Post(Message message)
        {
            lock (_writer)
            {
                if (Logger.IsEnabled(LogLevel.Verbose))
                {
                    Logger.WriteVerbose(string.Format("[ProcessingQueue]: Post({0})", message));
                }

                _writer.Write(JsonConvert.SerializeObject(message));
            }
        }

        private void ReceiveMessages()
        {
            while (true)
            {
                try
                {
                    var message = JsonConvert.DeserializeObject<Message>(_reader.ReadString());

                    if (Logger.IsEnabled(LogLevel.Verbose))
                    {
                        Logger.WriteVerbose(string.Format("[ProcessingQueue]: Receive ({0})", message));
                    }

                    OnReceive(message);
                }
                catch (IOException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.WriteError("Error occured processing message", ex);
                }
            }
        }
    }
}
