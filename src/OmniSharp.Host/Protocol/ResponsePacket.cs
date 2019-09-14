namespace OmniSharp.Protocol
{
    public class ResponsePacket : Packet
    {
        public int Request_seq { get; set; }

        public string Command { get; set; }

        public bool Running { get; set; }

        public bool Success { get; set; }

        public string Message { get; set; }

        public object Body { get; set; }

        public ResponsePacket() : base("response")
        {
            Seq = _seqPool++;
        }
    }
}
