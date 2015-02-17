using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Text;

namespace OmniSharp.Stdio.Protocol
{
    public class RequestPacket : Packet
    {
        private readonly JObject _obj;

        public string Command { get; set; }

        public RequestPacket(string json) : base("request")
        {
            _obj = JObject.Parse(json);
            Seq = _obj.GetValue("seq", StringComparison.OrdinalIgnoreCase).Value<int>();
            Command = _obj.GetValue("command", StringComparison.OrdinalIgnoreCase).Value<string>();
        }

        public Stream ArgumentsAsStream()
        {
            JToken token;

            if (_obj.TryGetValue("arguments", StringComparison.OrdinalIgnoreCase, out token)) {
                return new MemoryStream(Encoding.UTF8.GetBytes(token.ToString()));
            }
            return new MemoryStream();
        }

        public ResponsePacket Reply()
        {
            return new ResponsePacket()
            {
                Request_seq = Seq,
                Success = true,
                Running = true,
                Command = Command
            };
        }
    }
}
