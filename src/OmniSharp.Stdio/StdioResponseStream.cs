using System;
using System.IO;
using System.Threading.Tasks;
using OmniSharp.Stdio.Protocol;
using System.Text;
using Newtonsoft.Json;
using OmniSharp.Stdio.Services;

namespace OmniSharp.Stdio
{
    internal class StdioResponseStream : Stream
    {
        private readonly ISharedTextWriter _sharedWriter;
        private readonly ResponsePacket _response;
        private readonly object _lock = new object();

        private TaskCompletionSource<bool> _taskCompletion;
        private TextWriter _writer;
        private JsonWriter _jsonWriter;
        private bool _disposed;

        public StdioResponseStream(ISharedTextWriter writer, ResponsePacket response)
        {
            _sharedWriter = writer;
            _response = response;
        }

        public override bool CanWrite
        {
            get
            {
                return true;
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            CheckDisposed();

            lock (_lock)
            {
                if (_taskCompletion == null)
                {
                    _taskCompletion = new TaskCompletionSource<bool>();
                    _sharedWriter.Use(writer =>
                    {
                        try
                        {
                            _writer = writer;
                            _jsonWriter = new JsonTextWriter(writer);

                            _jsonWriter.WriteStartObject();
                            _writer.Write(@"""body"":"); // WritePropertyName would mess up the JsonWriter state
                            _writer.Write(Encoding.UTF8.GetChars(buffer, offset, count));

                        }
                        catch (Exception e)
                        {
                            _taskCompletion.SetException(e);
                        }

                        // we use a task to tell the shared text writer when we are
                        // done using it
                        return _taskCompletion.Task;
                    });
                }
                else
                {
                    try
                    {
                        _writer.Write(Encoding.UTF8.GetChars(buffer, offset, count));
                    }
                    catch (Exception e)
                    {
                        _taskCompletion.SetException(e);
                    }
                }
            }
        }

        public override void Flush()
        {
            // noop
        }

        protected override void Dispose(bool disposing)
        {
            lock (_lock)
            {
                CheckDisposed();
                _disposed = true;
            }

            try
            {
                _writer.Write(",");
                _jsonWriter.WritePropertyName("seq");
                _jsonWriter.WriteValue(_response.Seq);
                _jsonWriter.WritePropertyName("request_seq");
                _jsonWriter.WriteValue(_response.Request_seq);
                _jsonWriter.WritePropertyName("type");
                _jsonWriter.WriteValue(_response.Type);
                _jsonWriter.WritePropertyName("running");
                _jsonWriter.WriteValue(_response.Running);
                _jsonWriter.WritePropertyName("success");
                _jsonWriter.WriteValue(_response.Success);
                _jsonWriter.WritePropertyName("message");
                _jsonWriter.WriteValue(_response.Message);
                _jsonWriter.WriteEndObject();
                _writer.Write(_writer.NewLine);

                _writer = null;
                _jsonWriter = null;
                _taskCompletion.TrySetResult(true); // done
            }
            catch (Exception e)
            {
                _taskCompletion.SetException(e);
            }
        }

        private void CheckDisposed()
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(StdioResponseStream));
                }
            }
        }

        public override bool CanRead
        {
            get
            {
                return false;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return false;
            }
        }

        public override long Length
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override long Position
        {
            get
            {
                throw new NotImplementedException();
            }

            set
            {
                throw new NotImplementedException();
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }
    }
}
