namespace OmniSharp.Protocol
{
    public class EventPacket : Packet
    {
        public string Event { get; set; }

        public object Body { get; set; }

        public EventPacket() : base("event")
        {
            Seq = _seqPool++;
        }
    }
}
