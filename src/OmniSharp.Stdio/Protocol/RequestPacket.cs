using System;

namespace OmniSharp.Stdio.Protocol
{
    public class RequestPacket : Packet 
    {
        public string Command { get; set; }

        public dynamic Arguments { get; set; }

        public T ArgumentsAs<T>()
        {
            return Convert.ChangeType(Arguments, typeof(T));
        }

        public RequestPacket() : base("request") { }

        public ResponsePacket Reply(object body)
        {
            return new ResponsePacket()
            {
                Request_seq = Seq,
                Body = body,
                Command = Command
            };
        }
    }
}