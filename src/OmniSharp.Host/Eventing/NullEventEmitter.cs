namespace OmniSharp.Eventing
{
    internal class NullEventEmitter : IEventEmitter
    {
        public static IEventEmitter Instance { get; } = new NullEventEmitter();

        private NullEventEmitter() { }

        public void Emit(string kind, object args)
        {
            // nothing
        }
    }
}