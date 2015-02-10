namespace OmniSharp.Stdio.Protocol
{
    public class Packet
    {
        private static int _seqSource = 1;

        private readonly string _type;
        private readonly int _seq;

        public Packet(string type)
        {
            _type = type;
            _seq = _seqSource++;
        }

        public int Seq { get { return _seq; } }

        public string Type { get { return _type; } }
    }
}