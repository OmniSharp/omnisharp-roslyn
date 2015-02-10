namespace OmniSharp.Stdio.Protocol
{
    public class RequestPacket : Packet 
    {
        public string Command { get; set; }

        public object Arguments { get; set; }

        public RequestPacket() : base("request") { }
    }
}