using Newtonsoft.Json;

namespace OmniSharp.Protocol
{
    public class Packet
    {
        protected static int _seqPool = 1;

        private readonly string _type;

        public Packet(string type)
        {
            _type = type;
        }

        public int Seq { get; set; }

        public string Type { get { return _type; } }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
