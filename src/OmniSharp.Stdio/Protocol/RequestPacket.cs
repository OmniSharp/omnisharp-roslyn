using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OmniSharp.Stdio.Protocol
{
    public class RequestPacket : Packet
    {
        private readonly JObject obj;

        public string Command
        {
            get
            {
                return obj.GetValue("command", StringComparison.OrdinalIgnoreCase).Value<string>();
            }
        }
        
        public RequestPacket(string json) : base("request")
        {
            obj = JObject.Parse(json);
            Seq = obj.GetValue("seq", StringComparison.OrdinalIgnoreCase).Value<int>();
        }

        public object Arguments(Type type)
        {
            var token = obj.GetValue("arguments", StringComparison.OrdinalIgnoreCase);
            return token.HasValues ?
                JsonConvert.DeserializeObject(token.ToString(), type)
                : null;
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
