using System;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;

namespace OmniSharp.Protocol
{
    public class RequestPacket : Packet
    {
        public static RequestPacket Parse(string json)
        {
            var obj = JObject.Parse(json);
            var result = obj.ToObject<RequestPacket>();

            if (result.Seq <= 0)
            {
                throw new ArgumentException("invalid seq-value");
            }
            
            if (string.IsNullOrWhiteSpace(result.Command))
            {
                throw new ArgumentException("missing command");
            }
            
            JToken arguments;
            if (obj.TryGetValue("arguments", StringComparison.OrdinalIgnoreCase, out arguments))
            {
                result.ArgumentsStream = new MemoryStream(Encoding.UTF8.GetBytes(arguments.ToString()));
            }
            else
            {
                result.ArgumentsStream = Stream.Null;
            }
            return result;
        }

        public string Command { get; set; }

        public Stream ArgumentsStream { get; set; }

        public RequestPacket() : base("request") { }

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
