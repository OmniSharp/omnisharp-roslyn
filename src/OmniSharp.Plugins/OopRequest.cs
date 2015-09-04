namespace OmniSharp.Plugins
{
    class OopRequest
    {
        protected static int _seqPool = 1;
        public OopRequest()
        {
            Seq = _seqPool++;
        }
        public int Seq { get; set; }
        public string Command { get; set; }
        public object Body { get; set; }
    }
}
