namespace OmniSharp.Plugins
{
    class PluginRequest
    {
        protected static int _seqPool = 1;
        public PluginRequest()
        {
            Seq = _seqPool++;
        }
        public int Seq { get; set; }
        public string Command { get; set; }
        public object Arguments { get; set; }
    }
}
